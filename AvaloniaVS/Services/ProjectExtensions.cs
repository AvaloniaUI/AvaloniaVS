using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AvaloniaVS.Models;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace AvaloniaVS.Services
{
    public static class ProjectExtensions
    {
        private static readonly Regex DesktopFrameworkRegex = new Regex("^net[0-9]+$");

        public static string GetAssemblyPath(this Project project)
        {
            return GetProjectOutputInfo(project)?.FirstOrDefault()?.TargetAssembly;
        }

        public static ITextDocument GetDocument(this ITextBuffer buffer)
        {
            buffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out var document);
            return document;
        }

        public static Project GetProject(this IVsHierarchy hierarchy)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(
                VSConstants.VSITEMID_ROOT,
                (int)__VSHPROPID.VSHPROPID_ExtObject,
                out var objProj));
            return objProj as Project;
        }

        public static Project GetProject(this ITextDocument document)
        {
            return GetProjectForFile(document.FilePath);
        }

        public static IEnumerable<ProjectOutputInfo> GetProjectOutputInfo(this Project project)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var alternatives = new Dictionary<string, string>();
            var ucproject = GetUnconfiguredProject(project);
            if (ucproject != null)
                foreach (var loaded in ucproject.LoadedConfiguredProjects)
                {
                    var task = loaded.GetType()
                        .GetProperty("MSBuildProject",
                            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                        .GetMethod.Invoke(loaded, null) as Task<Microsoft.Build.Evaluation.Project>;
                    if (task?.IsCompleted != true)
                        continue;

                    var targetPath = task.Result.AllEvaluatedProperties.FirstOrDefault(p => p.Name == "TargetPath")?.EvaluatedValue;
                    if (!string.IsNullOrWhiteSpace(targetPath))
                    {
                        if (!loaded.ProjectConfiguration.Dimensions.TryGetValue("TargetFramework", out var targetFw))
                            targetFw = task.Result.AllEvaluatedProperties.FirstOrDefault(p => p.Name == "TargetFramework")
                                           ?.EvaluatedValue ?? "unknown";
                        alternatives[targetFw] = targetPath;
                    }
                }


            string fullPath = TryGetProperty(project?.Properties, "FullPath");

            string outputPath = project?.ConfigurationManager?.ActiveConfiguration?.Properties?.Item("OutputPath")?.Value?.ToString();
            if (fullPath != null && outputPath != null)
            {
                string outputDir = Path.Combine(fullPath, outputPath);
                string outputFileName = project.Properties.Item("OutputFileName").Value.ToString();
                if (!string.IsNullOrWhiteSpace(outputFileName))
                {
                    var fw = "net40";
                    var tfm = TryGetProperty(project.Properties, "TargetFrameworkMoniker");
                    const string tfmPrefix = ".netframework,version=v";
                    if (tfm != null && tfm.ToLowerInvariant().StartsWith(tfmPrefix))
                        fw = "net" + tfm.Substring(tfmPrefix.Length).Replace(".", "");

                    string assemblyPath = Path.Combine(outputDir, outputFileName);
                    alternatives[fw] = assemblyPath;
                }
            }
            var outputType = TryGetProperty(project?.Properties, "OutputType") ??
                             TryGetProperty(project?.ConfigurationManager?.ActiveConfiguration?.Properties,
                                 "OutputType");
            var outputTypeIsExecutable = outputType == "0" || outputType == "1"
                                         || outputType?.ToLowerInvariant() == "exe" ||
                                         outputType?.ToLowerInvariant() == "winexe";


            var lst = new List<ProjectOutputInfo>();
            foreach (var alternative in alternatives.OrderByDescending(x => x.Key == "classic"
                ? 10
                : DesktopFrameworkRegex.IsMatch(x.Key)
                    ? 9
                    : x.Key.StartsWith("netcoreapp")
                        ? 8
                        : x.Key.StartsWith("netstandard")
                            ? 7
                            : 0))
            {
                var nfo = new ProjectOutputInfo
                {
                    TargetAssembly = alternative.Value,
                    OutputTypeIsExecutable = outputTypeIsExecutable,
                    TargetFramework = alternative.Key == "classic" ? "net40" : alternative.Key
                };
                nfo.IsNetCore = nfo.TargetFramework.StartsWith("netcoreapp");
                nfo.IsFullDotNet = DesktopFrameworkRegex.IsMatch(nfo.TargetFramework);
                lst.Add(nfo);
            }
            return lst;
        }

        private static Project GetProjectForFile(string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                return null;
            }

            var dte2 = (DTE2)Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        private static UnconfiguredProject GetUnconfiguredProject(this Project project)
        {
            return project as UnconfiguredProject ?? project?.Object as UnconfiguredProject;
        }

        private static string TryGetProperty(Properties props, string name)
        {
            try
            {
                return props.Item(name).Value.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
