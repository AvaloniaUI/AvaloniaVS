using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.Text.Editor;
using Perspex.Designer;
using PerspexVS.Infrastructure;

namespace PerspexVS
{
    public partial class PerspexEditorMargin : UserControl, IWpfTextViewMargin
    {
        private readonly IWpfTextView _textView;
        private readonly PerspexDesigner _designer;

        public PerspexEditorMargin(IWpfTextView textView)
        {
            _textView = textView;
            _designer = new PerspexDesigner() {TargetExe = textView.GetContainingProject()?.GetAssemblyPath()};
            InitializeComponent();
            DesignerContainer.Content = _designer;
            Height = 200;
            _designer.Xaml = textView.TextBuffer.CurrentSnapshot.GetText();
            PerspexBuildEvents.Instance.BuildBegin += EventsOnBuildBegin;
            PerspexBuildEvents.Instance.BuildEnd += EventsOnBuildEnd;
            textView.TextBuffer.PostChanged += delegate
            {
                _designer.Xaml = textView.TextBuffer.CurrentSnapshot.GetText();
            };
            
        }

        public PerspexDesigner Designer => _designer;

        private void EventsOnBuildEnd()
        {
            _designer?.RestartProcess();
        }

        private void EventsOnBuildBegin()
        {
            _designer?.KillProcess();
        }

        public void Dispose()
        {
            PerspexBuildEvents.Instance.BuildBegin -= EventsOnBuildBegin;
            PerspexBuildEvents.Instance.BuildEnd -= EventsOnBuildEnd;
            _designer.KillProcess();
        }

        public ITextViewMargin GetTextViewMargin(string marginName)
        {
            return this;
        }

        public double MarginSize => Height;
        public bool Enabled { get; } = true;
        public FrameworkElement VisualElement => this;

        private void OnResizeDrag(object sender, DragDeltaEventArgs e)
        {
            var change =  e.VerticalChange;
            if (Height + change < 100)
                change = 0;
            if (_textView.ViewportHeight - change < 100)
                change = 0;
            Height += change;
        }
    }
}
