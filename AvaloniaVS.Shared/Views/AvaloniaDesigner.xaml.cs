using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Avalonia.Ide.CompletionEngine;
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
    public enum AvaloniaDesignerView
    {
        Split,
        Design,
        Source,
    }

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

        public static readonly DependencyProperty SplitOrientationProperty =
            DependencyProperty.Register(
                nameof(SplitOrientation),
                typeof(Orientation),
                typeof(AvaloniaDesigner),
                new PropertyMetadata(Orientation.Horizontal, HandleSplitOrientationChanged));

        public static readonly DependencyProperty ViewProperty =
            DependencyProperty.Register(
                nameof(View),
                typeof(AvaloniaDesignerView),
                typeof(AvaloniaDesigner),
                new PropertyMetadata(AvaloniaDesignerView.Split, HandleViewChanged));

        public static readonly DependencyProperty TargetsProperty =
            TargetsPropertyKey.DependencyProperty;

        public static readonly DependencyProperty ZoomLevelProperty =
            DependencyProperty.Register(
                nameof(ZoomLevel),
                typeof(string),
                typeof(AvaloniaDesigner),
                new PropertyMetadata("100%", HandleZoomLevelChanged));

        private static string FmtZoomLevel(double v) => $"{v.ToString(CultureInfo.InvariantCulture)}%";

        public static string[] ZoomLevels { get; } = new string[]
        {
            FmtZoomLevel(800), FmtZoomLevel(400), FmtZoomLevel(200), FmtZoomLevel(150), FmtZoomLevel(100),
            FmtZoomLevel(66.67), FmtZoomLevel(50), FmtZoomLevel(33.33), FmtZoomLevel(25), FmtZoomLevel(12.5),
            "Fit All"
        };


        private static readonly GridLength ZeroStar = new GridLength(0, GridUnitType.Star);
        private static readonly GridLength OneStar = new GridLength(1, GridUnitType.Star);
        private readonly Throttle<string> _throttle;
        private readonly ColumnDefinition _previewCol = new ColumnDefinition { Width = OneStar };
        private readonly ColumnDefinition _codeCol = new ColumnDefinition { Width = OneStar };
        private Project _project;
        private IWpfTextViewHost _editor;
        private string _xamlPath;
        private bool _loadingTargets;
        private bool _isStarted;
        private bool _isPaused;
        private SemaphoreSlim _startingProcess = new SemaphoreSlim(1, 1);
        private bool _disposed;
        private double _scaling = 1;

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
            UpdateLayoutForView();

            Loaded += (s, e) =>
            {
                StartStopProcessAsync().FireAndForget();
            };
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
                    StartStopProcessAsync().FireAndForget();
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
        /// Gets or sets the orientation of the split view.
        /// </summary>
        public Orientation SplitOrientation
        {
            get => (Orientation)GetValue(SplitOrientationProperty);
            set => SetValue(SplitOrientationProperty, value);
        }

        /// <summary>
        /// Gets or sets the type of view to display.
        /// </summary>
        public AvaloniaDesignerView View
        {
            get => (AvaloniaDesignerView)GetValue(ViewProperty);
            set => SetValue(ViewProperty, value);
        }

        /// <summary>
        /// Gets or sets the zoom level as a string.
        /// </summary>
        public string ZoomLevel
        {
            get => (string)GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, value);
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
                metadata.NeedInvalidation = true;
            }
        }

        /// <summary>
        /// Disposes of the designer and all resources.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;

            if (_editor?.TextView.TextBuffer is ITextBuffer2 oldBuffer)
            {
                oldBuffer.ChangedOnBackground -= TextChanged;
            }

            if (_editor?.IsClosed == false)
            {
                _editor.Close();
            }

            Process.FrameReceived -= FrameReceived;

            _throttle.Dispose();
            previewer.Dispose();
            Process.Dispose();
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            Process.SetScalingAsync(newDpi.DpiScaleX * _scaling).FireAndForget();
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
                await StartStopProcessAsync();
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
                        project.IsExecutable &&
                        project.References.Contains("Avalonia.DesignerSupport");
                }

                bool IsValidOutput(ProjectOutputInfo output)
                {
                    return output.IsNetCore;
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

                var oldSelectedTarget = SelectedTarget;

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
                               HostApp = output.HostApp,
                           }).ToList();

                SelectedTarget = Targets.FirstOrDefault(t => t.Name == oldSelectedTarget?.Name) ?? Targets.FirstOrDefault();
            }
            finally
            {
                _loadingTargets = false;
            }

            Log.Logger.Verbose("Finished AvaloniaDesigner.LoadTargetsAsync()");
        }

        private async Task StartStopProcessAsync()
        {
            if (!_isStarted)
            {
                return;
            }

            if (View != AvaloniaDesignerView.Source)
            {
                if (IsPaused)
                {
                    pausedMessage.Visibility = Visibility.Visible;
                    Process.Stop();
                }
                else if (!Process.IsRunning && IsLoaded)
                {
                    pausedMessage.Visibility = Visibility.Collapsed;

                    if (SelectedTarget == null)
                    {
                        await LoadTargetsAsync();
                    }

                    await StartProcessAsync();
                }
            }
            else if (!IsPaused && IsLoaded)
            {
                RebuildMetadata(null, null);
                Process.Stop();
            }
        }

        private bool TryProcessZoomLevelValue(out double scaling)
        {
            scaling = 1;

            if (string.IsNullOrEmpty(ZoomLevel))
                return false;

            if (ZoomLevel.Equals("Fit All", StringComparison.OrdinalIgnoreCase))
            {
                if (Process.IsReady && Process.Bitmap != null)
                {
                    double x = previewer.ActualWidth / (Process.Bitmap.Width / Process.Scaling);
                    double y = previewer.ActualHeight / (Process.Bitmap.Height / Process.Scaling);

                    ZoomLevel = string.Format(CultureInfo.InvariantCulture, "{0}%", Math.Round(Math.Min(x, y), 2, MidpointRounding.ToEven) * 100);
                }
                else
                {
                    ZoomLevel = "100%";
                }

                return false;
            }
            else if (double.TryParse(ZoomLevel.TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out double zoomPercent)
                     && zoomPercent > 0 && zoomPercent <= 1000)
            {
                scaling = zoomPercent / 100;
                return true;
            }

            return false;
        }

        private async Task StartProcessAsync()
        {
            Log.Logger.Verbose("Started AvaloniaDesigner.StartProcessAsync()");

            ShowPreview();

            var assemblyPath = SelectedTarget?.XamlAssembly;
            var executablePath = SelectedTarget?.ExecutableAssembly;
            var hostAppPath = SelectedTarget?.HostApp;

            if (assemblyPath != null && executablePath != null && hostAppPath != null)
            {
                RebuildMetadata(assemblyPath, executablePath);

                try
                {
                    await _startingProcess.WaitAsync();

                    if (!IsPaused)
                    {
                        await Process.SetScalingAsync(VisualTreeHelper.GetDpi(this).DpiScaleX * _scaling);
                        await Process.StartAsync(assemblyPath, executablePath, hostAppPath);
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

        private void RebuildMetadata(string assemblyPath, string executablePath)
        {
            assemblyPath = assemblyPath ?? SelectedTarget?.XamlAssembly;
            executablePath = executablePath ?? SelectedTarget?.ExecutableAssembly;

            if (assemblyPath != null && executablePath != null)
            {
                var buffer = _editor.TextView.TextBuffer;
                var metadata = buffer.Properties.GetOrCreateSingletonProperty(
                    typeof(XamlBufferMetadata),
                    () => new XamlBufferMetadata());
                buffer.Properties["AssemblyName"] = Path.GetFileNameWithoutExtension(assemblyPath);

                if (metadata.CompletionMetadata == null || metadata.NeedInvalidation)
                {
                    CreateCompletionMetadataAsync(executablePath, metadata).FireAndForget();
                }
            }
        }

        private static Dictionary<string, Task<Metadata>> _metadataCache;

        private static async Task CreateCompletionMetadataAsync(
            string executablePath,
            XamlBufferMetadata target)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_metadataCache == null)
            {
                _metadataCache = new Dictionary<string, Task<Metadata>>();
                var dte = (DTE)Package.GetGlobalService(typeof(DTE));

                dte.Events.BuildEvents.OnBuildBegin += (s, e) => _metadataCache.Clear();
            }

            Log.Logger.Verbose("Started AvaloniaDesigner.CreateCompletionMetadataAsync() for {ExecutablePath}", executablePath);

            try
            {
                var sw = Stopwatch.StartNew();

                Task<Metadata> metadataLoad;

                if (!_metadataCache.TryGetValue(executablePath, out metadataLoad))
                {
                    metadataLoad = Task.Run(() =>
                                    {
                                        var metadataReader = new MetadataReader(new DnlibMetadataProvider());
                                        return metadataReader.GetForTargetAssembly(executablePath);
                                    });
                    _metadataCache[executablePath] = metadataLoad;
                }

                target.CompletionMetadata = await metadataLoad;

                target.NeedInvalidation = false;

                sw.Stop();

                Log.Logger.Verbose("Finished AvaloniaDesigner.CreateCompletionMetadataAsync() took {Time} for {ExecutablePath}", sw.Elapsed, executablePath);
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

        private void UpdateLayoutForView()
        {
            void HorizontalGrid()
            {
                if (mainGrid.RowDefinitions.Count == 0)
                {
                    mainGrid.RowDefinitions.Add(previewRow);
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    mainGrid.RowDefinitions.Add(codeRow);
                    mainGrid.ColumnDefinitions.Clear();
                    splitter.Height = 5;
                    splitter.Width = double.NaN;
                }
            }

            void VerticalGrid()
            {
                if (mainGrid.ColumnDefinitions.Count == 0)
                {
                    mainGrid.ColumnDefinitions.Add(_previewCol);
                    mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    mainGrid.ColumnDefinitions.Add(_codeCol);
                    mainGrid.RowDefinitions.Clear();
                    splitter.Width = 5;
                    splitter.Height = double.NaN;
                }
            }

            if (View == AvaloniaDesignerView.Split)
            {
                previewRow.Height = OneStar;
                codeRow.Height = OneStar;

                if (SplitOrientation == Orientation.Horizontal)
                {
                    HorizontalGrid();
                }
                else
                {
                    VerticalGrid();
                }

                splitter.Visibility = Visibility.Visible;
            }
            else
            {
                HorizontalGrid();
                previewRow.Height = View == AvaloniaDesignerView.Design ? OneStar : ZeroStar;
                codeRow.Height = View == AvaloniaDesignerView.Source ? OneStar : ZeroStar;
                splitter.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateXaml(string xaml)
        {
            if (Process.IsReady)
            {
                Process.UpdateXamlAsync(xaml).FireAndForget();
            }
        }

        private void UpdateScaling(double scaling)
        {
            _scaling = scaling;

            if (Process.IsReady)
            {
                Process.SetScalingAsync(VisualTreeHelper.GetDpi(this).DpiScaleX * _scaling).FireAndForget();
            }
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
        }

        private static void HandleSelectedTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvaloniaDesigner designer && !designer._loadingTargets)
            {
                designer.SelectedTargetChangedAsync(d, e).FireAndForget();
            }
        }

        private static void HandleSplitOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvaloniaDesigner designer)
            {
                designer.UpdateLayoutForView();
            }
        }

        private static void HandleViewChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvaloniaDesigner designer)
            {
                designer.UpdateLayoutForView();
                designer.StartStopProcessAsync().FireAndForget();
            }
        }

        private static void HandleZoomLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AvaloniaDesigner designer && designer.TryProcessZoomLevelValue(out double scaling))
            {
                designer.UpdateScaling(scaling);
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
