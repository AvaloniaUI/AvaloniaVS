using System;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace AvaloniaVS.Services
{
    internal class OutputPaneEventSink : ILogEventSink
    {
        private static readonly Guid paneGuid = new Guid("DC845612-459C-485C-8157-71BC39C9A044");
        private readonly IVsOutputWindowPane _pane;
        private readonly ITextFormatter _formatter;

        public OutputPaneEventSink(
            IVsOutputWindow output,
            string outputTemplate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _formatter = new MessageTemplateTextFormatter(outputTemplate, null);
            ErrorHandler.ThrowOnFailure(output.CreatePane(paneGuid, "Avalonia Diagnostics", 1, 1));
            output.GetPane(paneGuid, out _pane);
        }

        public void Emit(LogEvent logEvent)
        {
            var sw = new StringWriter();
            _formatter.Format(logEvent, sw);
            var message = sw.ToString();

            if (_pane is IVsOutputWindowPaneNoPump noPump)
            {
                noPump.OutputStringNoPump(message);
            }
            else
            {
                ErrorHandler.ThrowOnFailure(_pane.OutputStringThreadSafe(message));
            }
        }
    }
}
