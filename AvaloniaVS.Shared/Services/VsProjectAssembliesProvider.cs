using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using Microsoft.VisualStudio.Shell;
using Serilog;
using VSLangProj;

namespace AvaloniaVS.Shared.Services
{
    // VS API requires this code to run on Main Thread, so we have to fetch that ahead.
    internal class VsProjectAssembliesProvider : IAssemblyProvider
    {
        private readonly List<string> _references;

        private VsProjectAssembliesProvider(List<string> references)
        {
            _references = references;
        }

        public static VsProjectAssembliesProvider TryCreate(EnvDTE.Project project, string xamlPrimaryAssemblyPath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                if (project.Object is VSProject vsProject)
                {
                    var references = new List<string>(200);
                    references.Add(xamlPrimaryAssemblyPath);

                    foreach (Reference reference in vsProject.References)
                    {
                        if (reference.Type == prjReferenceType.prjReferenceTypeAssembly
                            && reference.Path is not null)
                        {
                            references.Add(reference.Path);
                        }
                    }

                    // Not sure if it's possible, but never know what surprise VS has.
                    if (references.Count == 1)
                        return null;

                    return new VsProjectAssembliesProvider(references);
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "VsProjectAssembliesProvider.TryCreate failed with an exception.");
            }
            return null;
        }

        public IEnumerable<string> GetAssemblies() => _references;
    }
}
