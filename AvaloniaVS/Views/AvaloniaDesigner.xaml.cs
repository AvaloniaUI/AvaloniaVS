using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;
using AvaloniaVS.Models;
using AvaloniaVS.Services;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;
using Serilog;
using Task = System.Threading.Tasks.Task;

namespace AvaloniaVS.Views
{
    /// <summary>
    /// The Avalonia XAML designer control.
    /// </summary>
    internal partial class AvaloniaDesigner : UserControl, IDisposable
    {
        private static readonly DependencyPropertyKey TargetsPropertyKey =
            DependencyProperty.RegisterReadOnly(
                nameof(Targets),
                typeof(IReadOnlyList<DesignerRunTarget>),
                typeof(AvaloniaDesigner),
                new PropertyMetadata());

        public static readonly DependencyProperty SelectedTargetProperty =
            DependencyProperty.Register(
                nameof(SelectedTarget),
                typeof(DesignerRunTarget),
                typeof(AvaloniaDesigner),
                new PropertyMetadata(HandleSelectedTargetChanged));

        public static readonly DependencyProperty TargetsProperty =
            TargetsPropertyKey.DependencyProperty;

        public static readonly DependencyProperty IsPreviewEnabledProperty =
            DependencyProperty.Register(
                nameof(IsPreviewEnabled),
                typeof(bool),
                typeof(AvaloniaDesigner),
                new PropertyMetadata(true, HandleIsPreviewEnabledChanged));

        private readonly Throttle<string> _throttle;
        private Project _project;
        private IWpfTextViewHost _editor;
        private string _xamlPath;
        private bool _loadingTargets;
        private bool _isStarted;
        private bool _isPaused;
        private SemaphoreSlim _startingProcess = new SemaphoreSlim(1, 1);
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="AvaloniaDesigner"/> class.
        /// </summary>
        public AvaloniaDesigner()
        {
            InitializeComponent();

            _throttle = new Throttle<string>(TimeSpan.FromMilliseconds(300), UpdateXaml);
            Process = new PreviewerProcess();
            Process.ErrorChanged += ErrorChanged;
            Process.FrameReceived += FrameReceived;
            Process.ProcessExited += ProcessExited;
            previewer.Process = Process;
            pausedMessage.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Gets or sets the paused state of the designer.
        /// </summary>
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused != value)
                {
                    Log.Logger.Debug("Setting pause state to {State}", value);

                    _isPaused = value;
                    IsEnabled = !value;

                    if (_isStarted)
                    {
                        if (value)
                        {
                            pausedMessage.Visibility = Visibility.Visible;
                            Process.Stop();
                        }
                        else
                        {
                            pausedMessage.Visibility = Visibility.Collapsed;
                            LoadTargetsAndStartProcessAsync().FireAndForget();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the previewer process used by the designer.
        /// </summary>
        public PreviewerProcess Process { get; }

        /// <summary>
        /// Gets the list of targets that the designer can use to preview the XAML.
        /// </summary>
        public IReadOnlyList<DesignerRunTarget> Targets
        {
            get => (IReadOnlyList<DesignerRunTarget>)GetValue(TargetsProperty);
            private set => SetValue(TargetsPropertyKey, value);
        }

        /// <summary>
        /// Gets or sets the selected target.
        /// </summary>
        public DesignerRunTarget SelectedTarget
        {
            get => (DesignerRunTarget)GetValue(SelectedTargetProperty);
            set => SetValue(SelectedTargetProperty, value);
        }

        /// <summary>
        /// Gets or sets preview is enabled
        /// </summary>
        public bool IsPreviewEnabled
        {
            get => (bool)GetValue(IsPreviewEnabledProperty);
            set => SetValue(IsPreviewEnabledProperty, value);
        }

        /// <summary>
        /// Starts the designer.
        /// </summary>
        /// <param name="project">The project containing the XAML file.</param>
        /// <param name="xamlPath">The path to the XAML file.</param>
        /// <param name="editor">The VS text editor control host.</param>
        public void Start(Project project, string xamlPath, IWpfTextViewHost editor)
        {
            Log.Logger.Verbose("Started AvaloniaDesigner.Start()");

            if (_isStarted)
            {
                throw new InvalidOperationException("The designer has already been started.");
            }

            _project = project ?? throw new ArgumentNullException(nameof(project));
            _xamlPath = xamlPath ?? throw new ArgumentNullException(nameof(xamlPath));
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));

            InitializeEditor();
            LoadTargetsAndStartProcessAsync().FireAndForget();

            Log.Logger.Verbose("Finished AvaloniaDesigner.Start()");
        }

        /// <summary>
        /// Invalidates the intellisense completion metadata.
        /// </summary>
        /// <remarks>
        /// Should be called when the designer is paused; when unpaused the completion metadata
        /// will be updated.
        /// </remarks>
        public void InvalidateCompletionMetadata()
        {
            var buffer = _editor.TextView.TextBuffer;

            if (buffer.Properties.TryGetProperty<XamlBufferMetadata>(
                    typeof(XamlBufferMetadata),
                    out var metadata))
            {
                metadata.CompletionMetadata = null;
            }
        }

        /// <summary>
        /// Disposes of the designer and all resources.
        /// </summary>
        public void Dispose()
        {
            var alreadyDisposed = _disposed;

            _disposed = true;

            if (_editor?.TextView.TextBuffer is ITextBuffer2 oldBuffer)
            {
                oldBuffer.ChangedOnBackground -= TextChanged;
            }

            if (_editor?.IsClosed == false)
            {
                _editor.Close();
            }

            var assemblyPath = SelectedTarget?.XamlAssembly;
            var executablePath = SelectedTarget?.ExecutableAssembly;

            Process.FrameReceived -= FrameReceived;

            _throttle.Dispose();
            previewer.Dispose();
            Process.Dispose();

            if (!alreadyDisposed && assemblyPath != null && executablePath != null)
            {
                _ = Task.Delay(100).ContinueWith(t => TryCleanDesignTempData(executablePath, assemblyPath));
            }
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            Process.SetScalingAsync(newDpi.DpiScaleX).FireAndForget();
        }

        private void InitializeEditor()
        {
            editorHost.Child = _editor.HostControl;

            _editor.TextView.TextBuffer.Properties.RemoveProperty(typeof(PreviewerProcess));
            _editor.TextView.TextBuffer.Properties.AddProperty(typeof(PreviewerProcess), Process);

            if (_editor.TextView.TextBuffer is ITextBuffer2 newBuffer)
            {
                newBuffer.ChangedOnBackground += TextChanged;
            }
        }

        private async Task LoadTargetsAndStartProcessAsync()
        {
            Log.Logger.Verbose("Started AvaloniaDesigner.LoadTargetsAndStartProcessAsync()");

            await LoadTargetsAsync();

            if (!_disposed)
            {
                _isStarted = true;
                await StartProcessAsync();
            }

            Log.Logger.Verbose("Finished AvaloniaDesigner.LoadTargetsAndStartProcessAsync()");
        }

        private async Task LoadTargetsAsync()
        {
            Log.Logger.Verbose("Started AvaloniaDesigner.LoadTargetsAsync()");

            _loadingTargets = true;

            try
            {
                var projects = await AvaloniaPackage.SolutionService.GetProjectsAsync();

                bool IsValidTarget(ProjectInfo project)
                {
                    return (project.Project == _project || project.ProjectReferences.Contains(_project)) &&
                        project.References.Contains("Avalonia.DesignerSupport");
                }

                bool IsValidOutput(ProjectOutputInfo output)
                {
                    return output.IsNetCore &&
                        (output.OutputTypeIsExecutable ||
                        output.TargetAssembly.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                }

                string GetXamlAssembly(ProjectOutputInfo output)
                {
                    var project = projects.FirstOrDefault(x => x.Project == _project);

                    // Ideally we'd have the path to the `project` assembly that `output` uses, but
                    // I'm not sure how to get that information, so instead look for a netcore output
                    // or failing that a netstandard output, and pray.
                    return project?.Outputs
                        .OrderBy(x => !x.IsNetCore)
                        .ThenBy(x => !x.IsNetStandard)
                        .FirstOrDefault()?
                        .TargetAssembly;
                }

                Targets = (from project in projects
                           where IsValidTarget(project)
                           orderby project.Project != project, !project.IsStartupProject, project.Name
                           from output in project.Outputs
                           where IsValidOutput(output)
                           select new DesignerRunTarget
                           {
                               Name = $"{project.Name} [{output.TargetFramework}]",
                               ExecutableAssembly = output.TargetAssembly,
                               XamlAssembly = GetXamlAssembly(output),
                           }).ToList();

                SelectedTarget = Targets.FirstOrDefault();
            }
            finally
            {
                _loadingTargets = false;
            }

            Log.Logger.Verbose("Finished AvaloniaDesigner.LoadTargetsAsync()");
        }

        static private (string executableDir, string assemblyDir) GetDesignTempDirs(string executablePath, string assemblyPath)
        {
            //let's try use very short folder names as limit for directory path is 248 chars
            var designerTempDir = Path.Combine(Directory.GetParent(Path.GetDirectoryName(executablePath)).FullName, "dttmp");

            var executableDirDesigner = Path.Combine(designerTempDir, "exe");
            var assemblyDirDesigner = Path.Combine(designerTempDir, "asm");

            return (executableDirDesigner, assemblyDirDesigner);
        }

        private (string executablePath, string assemblyPath) TryPrepareDesignTempData(string executablePath, string assemblyPath)
        {
            try
            {
                Log.Logger.Verbose("Started AvaloniaDesigner.TryPrepareDesignTempData()");

                void CopyFile(string src, string dst)
                {
                    var srcFile = new FileInfo(src);
                    var dstFile = new FileInfo(dst);

                    if (!Directory.Exists(dstFile.DirectoryName))
                        Directory.CreateDirectory(dstFile.DirectoryName);

                    if (srcFile.LastWriteTime > dstFile.LastWriteTime || !dstFile.Exists)
                        File.Copy(srcFile.FullName, dstFile.FullName, true);
                }

                void CopyFolder(string src, string dst, string mask = "*.*", bool recursive = false)
                {
                    var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

                    foreach (var sourceFile in Directory.GetFiles(src, mask, opt))
                    {
                        CopyFile(sourceFile, $"{dst}{sourceFile.Replace(src, "")}");
                    }
                }

                var executableDir = Path.GetDirectoryName(executablePath);
                var assemblyDir = Path.GetDirectoryName(assemblyPath);

                var tmpDirs = GetDesignTempDirs(executablePath, assemblyPath);

                var executableDirDesigner = tmpDirs.executableDir;
                var assemblyDirDesigner = tmpDirs.assemblyDir;

                CopyFolder(executableDir, executableDirDesigner, recursive: true);
                CopyFile(assemblyPath, Path.Combine(tmpDirs.assemblyDir, Path.GetFileName(assemblyPath)));

                Log.Logger.Verbose("Copied assemblies to temp folders:{ExecutableDirDesigner},{AssemblyDirDesigner}", executableDirDesigner, assemblyDirDesigner);

                var executablePathDesign = Path.Combine(executableDirDesigner, Path.GetFileName(executablePath));
                var assemblyPathDesign = Path.Combine(assemblyDirDesigner, Path.GetFileName(assemblyPath));

                Log.Logger.Verbose("Finished AvaloniaDesigner.TryPrepareDesignTempFolder()");

                return (executablePathDesign, assemblyPathDesign);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "AvaloniaDesigner.TryPrepareDesignTempData() failed!");
                ShowError("Prepare Design Time", $"Prepare Design Time Preview and Completion Failed:{ex.Message}");
                return default((string, string));
            }
        }

        static private void TryCleanDesignTempData(string executablePath, string assemblyPath)
        {
            try
            {
                var tmpDirs = GetDesignTempDirs(executablePath, assemblyPath);

                void deletefile(string path)
                {
                    Log.Information("Cleaning temp file {Path}", path);
                    File.Delete(path);
                }

                void cleandir(string path)
                {
                    Log.Information("Cleaning temp folder {Path}", path);
                    Directory.Delete(path, true);
                }

                deletefile(Path.Combine(tmpDirs.assemblyDir, Path.GetFileName(assemblyPath)));

                cleandir(tmpDirs.executableDir);
            }
            catch (Exception ex)
            {
                Log.Logger.Warning(ex, "AvaloniaDesigner.TryCleanDesignTempData() failed!");
            }
        }

        private async Task StartProcessAsync()
        {
            Log.Logger.Verbose("Started AvaloniaDesigner.StartProcessAsync()");

            ShowPreview();

            var assemblyPath = SelectedTarget?.XamlAssembly;
            var executablePath = SelectedTarget?.ExecutableAssembly;

            if (assemblyPath != null && executablePath != null)
            {
                var designAsm = TryPrepareDesignTempData(executablePath, assemblyPath);
                if (designAsm != default((string, string)))
                {
                    var buffer = _editor.TextView.TextBuffer;
                    var metadata = buffer.Properties.GetOrCreateSingletonProperty(
                        typeof(XamlBufferMetadata),
                        () => new XamlBufferMetadata());
                    buffer.Properties["AssemblyName"] = Path.GetFileNameWithoutExtension(assemblyPath);

                    if (metadata.CompletionMetadata == null)
                    {
                        CreateCompletionMetadataAsync(designAsm.executablePath, metadata).FireAndForget();
                    }

                    try
                    {
                        await _startingProcess.WaitAsync();

                        if (!IsPaused && IsPreviewEnabled)
                        {
                            await Process.SetScalingAsync(VisualTreeHelper.GetDpi(this).DpiScaleX);
                            await Process.StartAsync(designAsm.assemblyPath, designAsm.executablePath);
                            await Process.UpdateXamlAsync(await ReadAllTextAsync(_xamlPath));
                        }
                    }
                    catch (ApplicationException ex)
                    {
                        // Don't display an error here: ProcessExited should handle that.
                        Log.Logger.Debug(ex, "Process.StartAsync exited with error");
                    }
                    catch (FileNotFoundException ex)
                    {
                        ShowError("Build Required", ex.Message);
                        Log.Logger.Debug(ex, "StartAsync could not find executable");
                    }
                    catch (Exception ex)
                    {
                        ShowError("Error", ex.Message);
                        Log.Logger.Debug(ex, "StartAsync exception");
                    }
                    finally
                    {
                        _startingProcess.Release();
                    }
                }
            }
            else
            {
                Log.Logger.Error("No executable found");

                // This message is unfortunate but I can't work out how to tell when all references
                // have finished loading for all projects in the solution.
                ShowError(
                    "No Executable",
                    "Reference the library from an executable or wait for the solution to finish loading.");
            }

            Log.Logger.Verbose("Finished AvaloniaDesigner.StartProcessAsync()");
        }

        private static async Task CreateCompletionMetadataAsync(
            string executablePath,
            XamlBufferMetadata target)
        {
            await TaskScheduler.Default;

            Log.Logger.Verbose("Started AvaloniaDesigner.CreateCompletionMetadataAsync()");

            try
            {
                var metadataReader = new MetadataReader(new DnlibMetadataProvider());
                target.CompletionMetadata = metadataReader.GetForTargetAssembly(executablePath);
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error creating XAML completion metadata");
            }
            finally
            {
                Log.Logger.Verbose("Finished AvaloniaDesigner.CreateCompletionMetadataAsync()");
            }
        }

        private async void ErrorChanged(object sender, EventArgs e)
        {
            if (Process.Bitmap == null && Process.Error != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ShowError("Invalid Markup", "Check the Error List for more information.");
            }
            else if (Process.Error == null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ShowPreview();
            }
        }

        private async void FrameReceived(object sender, EventArgs e)
        {
            if (Process.Bitmap != null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ShowPreview();
            }
        }

        private async void ProcessExited(object sender, EventArgs e)
        {
            if (!IsPaused)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                ShowError(
                    "Process Exited",
                    "The previewer process exited unexpectedly. See the output window for more information.");
            }
        }

        private void ShowError(string heading, string message)
        {
            previewer.Visibility = Visibility.Collapsed;
            error.Visibility = Visibility.Visible;
            errorHeading.Text = heading;
            errorMessage.Text = message;
        }

        private void ShowPreview()
        {
            previewer.Visibility = Visibility.Visible;
            error.Visibility = Visibility.Collapsed;
        }

        private void TextChanged(object sender, TextContentChangedEventArgs e)
        {
            _throttle.Queue(e.After.GetText());
        }

        private void UpdateXaml(string xaml)
        {
            if (Process.IsReady)
            {
                Process.UpdateXamlAsync(xaml).FireAndForget();
            }
        }

        private async Task IsPreviewEnabledChangedAsync(object sender, DependencyPropertyChangedEventArgs e)
        {
            await TryRestartProcessAsync();
        }

        private async Task SelectedTargetChangedAsync(object sender, DependencyPropertyChangedEventArgs e)
        {
            var oldValue = (DesignerRunTarget)e.OldValue;
            var newValue = (DesignerRunTarget)e.NewValue;

            Log.Logger.Debug(
                "AvaloniaDesigner.SelectedTarget changed from {OldTarget} to {NewTarget}",
                oldValue?.ExecutableAssembly,
                newValue?.ExecutableAssembly);

            if (oldValue?.ExecutableAssembly != newValue?.ExecutableAssembly)
            {
                await TryRestartProcessAsync();
            }
        }

        private async Task TryRestartProcessAsync()
        {
            if (_isStarted)
            {
                try
                {
                    Log.Logger.Debug("Waiting for StartProcessAsync to finish");
                    await _startingProcess.WaitAsync();
                    Process.Stop();
                    StartProcessAsync().FireAndForget();
                }
                finally
                {
                    _startingProcess.Release();
                }
            }
        }

        private static void HandleSelectedTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvaloniaDesigner designer && !designer._loadingTargets)
            {
                designer.SelectedTargetChangedAsync(d, e).FireAndForget();
            }
        }

        private static void HandleIsPreviewEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvaloniaDesigner designer && !designer._loadingTargets)
            {
                designer.IsPreviewEnabledChangedAsync(d, e).FireAndForget();
            }
        }

        private static async Task<string> ReadAllTextAsync(string fileName)
        {
            using (var reader = File.OpenText(fileName))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
