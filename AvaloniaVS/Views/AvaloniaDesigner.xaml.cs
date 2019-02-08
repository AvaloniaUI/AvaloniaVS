using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        private readonly Throttle<string> _throttle;
        private Project _project;
        private IWpfTextViewHost _editor;
        private string _xamlPath;
        private bool _isStarted;
        private bool _isPaused;

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
            previewer.Process = Process;
            ProjectInfoService.AddChangedHandler(ProjectInfoChanged);
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
                            Process.Stop();
                        }
                        else
                        {
                            StartAsync().FireAndForget();
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

            _isStarted = true;

            InitializeEditor();
            StartAsync().FireAndForget();

            Log.Logger.Verbose("Finished AvaloniaDesigner.Start()");
        }

        /// <summary>
        /// Disposes of the designer and all resources.
        /// </summary>
        public void Dispose()
        {
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

        private void InitializeEditor()
        {
            editorHost.Child = _editor.HostControl;

            _editor.TextView.TextBuffer.Properties.AddProperty(typeof(PreviewerProcess), Process);

            if (_editor.TextView.TextBuffer is ITextBuffer2 newBuffer)
            {
                newBuffer.ChangedOnBackground += TextChanged;
            }
        }

        private async Task StartAsync()
        {
            Log.Logger.Verbose("Started AvaloniaDesigner.StartAsync()");

            ShowPreview();
            LoadTargets();

            var executablePath = SelectedTarget?.TargetAssembly;

            if (executablePath != null)
            {
                var buffer = _editor.TextView.TextBuffer;
                var metadata = buffer.Properties.GetOrCreateSingletonProperty(
                    typeof(XamlBufferMetadata),
                    () => new XamlBufferMetadata());

                if (metadata.CompletionMetadata == null)
                {
                    CreateCompletionMetadataAsync(executablePath, metadata).FireAndForget();
                }

                try
                {
                    if (!IsPaused)
                    {
                        await Process.StartAsync(executablePath);
                        await Process.UpdateXamlAsync(await ReadAllTextAsync(_xamlPath));
                    }
                }
                catch (ApplicationException ex) when (IsPaused)
                {
                    Log.Logger.Debug(ex, "Process.StartAsync terminated due to pause");
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
            }
            else
            {
                Log.Logger.Error("No executable found");
                ShowError("No Executable", "Reference the library from an executable.");
            }

            Log.Logger.Verbose("Finished AvaloniaDesigner.StartEditorAsync()");
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

        private void LoadTargets()
        {
            var oldTargets = Targets ?? Array.Empty<DesignerRunTarget>();
            var newTargets = ProjectInfoService.Projects
                .Where(x => x.Project == _project || x.References.Contains(_project))
                .OrderBy(x => x.Project == _project)
                .ThenBy(x => x.Name)
                .SelectMany(x => x.RunnableOutputs
                    .Select(o => new DesignerRunTarget
                    {
                        Name = $"{x.Name} [{o.Key}]",
                        TargetAssembly = o.Value,
                        IsContainingProject = x.Project == _project,
                    })).ToList();

            Targets = oldTargets
                .Union(newTargets)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (SelectedTarget == null || !Targets.Contains(SelectedTarget))
            {
                SelectedTarget = Targets.FirstOrDefault();
            }
        }

        private async void ErrorChanged(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Process.Bitmap == null && Process.Error != null)
            {
                ShowError("Invalid Markup", "Check the Error List for more information");
            }
            else if (Process.Error == null)
            {
                ShowPreview();
            }
        }

        private async void FrameReceived(object sender, EventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (Process.Bitmap != null)
            {
                ShowPreview();
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

        private void ProjectInfoChanged(object sender, EventArgs e)
        {
            LoadTargets();
        }

        private void UpdateXaml(string xaml)
        {
            if (Process.IsReady)
            {
                Process.UpdateXamlAsync(xaml).FireAndForget();
            }
        }

        private void SelectedTargetChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            var oldValue = (DesignerRunTarget)e.OldValue;
            var newValue = (DesignerRunTarget)e.NewValue;

            Log.Logger.Debug(
                "AvaloniaDesigner.SelectedTarget changed from {OldTarget} to {NewTarget}",
                oldValue?.TargetAssembly,
                newValue?.TargetAssembly);

            if (oldValue?.TargetAssembly != newValue?.TargetAssembly)
            {
                Process.Stop();
                StartAsync().FireAndForget();
            }
        }

        private static void HandleSelectedTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            (d as AvaloniaDesigner)?.SelectedTargetChanged(d, e);
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
