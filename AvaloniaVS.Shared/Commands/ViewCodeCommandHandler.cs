using System;
using System.ComponentModel.Composition;

using EnvDTE;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;

namespace AvaloniaVS.Shared.Commands
{
    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [Name("ViewCodeCommandHandler")]
    internal class ViewCodeCommandHandler : ICommandHandler<ViewCodeCommandArgs>
    {
        public string DisplayName => "Toogle Code/Designer";

        public bool ExecuteCommand(ViewCodeCommandArgs args, CommandExecutionContext executionContext)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = (DTE)AvaloniaPackage.GetGlobalService(typeof(DTE));

            if (args.TextView.Properties.ContainsProperty(typeof(AvaloniaVS.Views.AvaloniaDesigner)))
            {
                dte.ItemOperations.OpenFile(dte.ActiveDocument.FullName, Constants.vsViewKindTextView);
            }
            else
            {
                dte.ItemOperations.OpenFile(dte.ActiveDocument.FullName, Constants.vsViewKindDesigner);
            }

            return true;
        }

        public CommandState GetCommandState(ViewCodeCommandArgs args)
        {
            if (args.SubjectBuffer.Properties.TryGetProperty<Services.PreviewerProcess>(typeof(Services.PreviewerProcess), out var process))
            {
                return CommandState.Available;
            }
            else
            {
                return CommandState.Unavailable;
            }
        }
    }
}
