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
    /// <summary>
    /// A serilog sink that outputs to the VS output window.
    /// </summary>
    internal class OutputPaneEventSink : ILogEventSink
    {
        private static readonly Guid paneGuid = new Guid("DC845612-459C-485C-8157-71BC39C9A044");
        private readonly IVsOutputWindowPane _pane;
        private readonly ITextFormatter _formatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputPaneEventSink"/> class.
        /// </summary>
        /// <param name="output">The VS output window.</param>
        /// <param name="outputTemplate">The serilog output template.</param>
        public OutputPaneEventSink(
            IVsOutputWindow output,
            string outputTemplate)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _formatter = new MessageTemplateTextFormatter(outputTemplate, null);
            ErrorHandler.ThrowOnFailure(output.CreatePane(paneGuid, "Avalonia Diagnostics", 1, 1));
            output.GetPane(paneGuid, out _pane);
        }

#pragma warning disable VSTHRD010
        /// <inheritdoc/>
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

            if (logEvent.Level == LogEventLevel.Error)
            {
                _pane.Activate();
            }
        }
#pragma warning restore VSTHRD010
    }
}
