using System;
using System.ComponentModel.Composition;

using EnvDTE;

using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Utilities;
using Serilog;

namespace AvaloniaVS.Shared.Commands
{
    [Export(typeof(ICommandHandler))]
    [ContentType("text")]
    [Name("ViewCodeCommandHandler")]
    internal class ViewCodeCommandHandler : ICommandHandler<ViewCodeCommandArgs>
    {
        private const string csExt = ".cs";
        private const string fsExt = ".fs";
        private const string vbExt = ".vb";

        public string DisplayName => "View Code";

        public bool ExecuteCommand(ViewCodeCommandArgs args, CommandExecutionContext executionContext)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = (DTE)AvaloniaPackage.GetGlobalService(typeof(DTE));

            var activeDocument = dte.ActiveDocument;

            // We only want to handle "View Code" if our designer pane is active
            if (!activeDocument.FullName.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase))
                return false;

            if (args.TextView.Properties.ContainsProperty(typeof(AvaloniaVS.Views.AvaloniaDesigner)))
            {
                var pi = dte.ActiveDocument.ProjectItem;

                var codeFile = FindCodeFileForXaml(pi, out var codeProjectItem);
                if (!codeFile)
                    return false;

                var wnd = codeProjectItem.Open(Constants.vsViewKindTextView);
                wnd.Activate();

                return true;
            }

            return false;
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

#pragma warning disable VSTHRD010 // Only called from ExecuteCommand, which does the thread check
        private bool FindCodeFileForXaml(ProjectItem projectItem, out ProjectItem codeProjectItem)
        {
            codeProjectItem = null;
            if (projectItem.ProjectItems.Count == 0)
                return false;


            var x = projectItem.Properties.Parent;
            // Search the project items under the current project item
            // "MainWindow.axaml" <-- projectItem
            //   "MainWindow.axaml.cs" <-- projectItem.ProjectItems
            foreach (ProjectItem pi in projectItem.ProjectItems)
            {
                if (IsCodeFile(pi.Name))
                {
                    codeProjectItem = pi;
                    return true;
                }
            }

            // TODO: If we reach here, it means VS isn't nesting the files like its suppposed
            // to:
            // Project
            //   MainWindow.axaml
            //   MainWindow.axaml.cs
            // We need to find the parent item, and search for it as a sibling
            // The parent item can be obtained through 'projectItem.Collection.Parent'
            // which will probably return either a ProjectItem or Project
            // and then run a search to see where it matches the name and ends in a lang extension
            // The issue here is how does this handle files with partial classes? Since I don't
            // have a way to test this (since VS seems to nest normally for me), marking as
            // a TODO and we'll return false to not handle it

            Log.Logger.Verbose("Attempted to view code for {Document}, but was unable to find nested code file", projectItem.Name);

            return false;
        }
#pragma warning restore

        private bool IsCodeFile(string name)
        {
            if (name.EndsWith(csExt, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(fsExt, StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(vbExt, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
