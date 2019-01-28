using System;
using System.Windows;
using System.Windows.Controls;
using AvaloniaVS.Services;

namespace AvaloniaVS.Views
{
    public partial class XamlEditorView : UserControl
    {
        public static readonly DependencyProperty ProcessProperty =
            DependencyProperty.Register(
                nameof(Process),
                typeof(PreviewerProcess),
                typeof(XamlEditorView),
                new PropertyMetadata(ProcessChanged));

        public XamlEditorView()
        {
            InitializeComponent();
        }

        public PreviewerProcess Process
        {
            get => (PreviewerProcess)GetValue(ProcessProperty);
            set => SetValue(ProcessProperty, value);
        }

        private void Subscribe(PreviewerProcess process)
        {
            previewer.Process = process;
        }

        private void Unsubscribe(PreviewerProcess process)
        {
            previewer.Process = null;
        }

        private static void ProcessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sender = (XamlEditorView)d;

            if (e.OldValue is PreviewerProcess oldProcess)
            {
                sender.Unsubscribe(oldProcess);
            }

            if (e.NewValue is PreviewerProcess newProcess)
            {
                sender.Subscribe(newProcess);
            }
        }
    }
}
