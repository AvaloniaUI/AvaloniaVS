using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace AvaloniaVS
{
    static class Utils
    {
        public static string GetFilePath(this ITextView textView)
        {
            ITextDocument document;
            return !textView.TextBuffer.Properties.TryGetProperty(typeof (ITextDocument), out document)
                ? null
                : document.FilePath;
        }

        public static bool IsAvaloniaMarkup(ITextView textView)
        {
            var file = textView.GetFilePath()?.ToLower();
            bool edit = file?.EndsWith(".paml") == true;
            if (!edit && file?.EndsWith(".xaml") == true)
            {
                edit = Utils.CheckAvaloniaRoot(File.ReadAllText(file));
            }
            return edit;
        }

        public static bool CheckAvaloniaRoot(string content)
            => CheckAvaloniaRoot(new XmlTextReader(new StringReader(content)));

        public static bool CheckAvaloniaRoot(XmlReader reader)
        {
            try
            {
                while (!reader.IsStartElement())
                {
                    reader.Read();
                }
                if (!reader.MoveToFirstAttribute())
                    return false;
                do
                {
                    if (reader.Name == "xmlns")
                    {
                        reader.ReadAttributeValue();
                        return reader.Value.ToLower() == AvaloniaNamespace;
                    }

                } while (reader.MoveToNextAttribute());
                return false;
            }
            catch
            {
                return false;
            }
        }

        public const string AvaloniaNamespace = "https://github.com/avaloniaui";

        public static Project GetContainingProject(this IWpfTextView textView)
        {
            var fileName = textView.GetFilePath();
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var dte2 = (DTE2)Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        static string TryGetProperty(Properties props, string name)
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

        private static Regex DesktopFrameworkRegex = new Regex("^net[0-9]+$");

        /// <summary>
        /// Gets the full path of the <see cref="Project"/> configuration
        /// </summary>
        /// <param name="vsProject"></param>
        /// <returns>Target Exe path</returns>
        public static string GetAssemblyPath(this Project vsProject)
        {
            var alternatives = new Dictionary<string, string>();
            var ucproject = GetUnconfiguredProject(vsProject);
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
                            targetFw = "unknown";
                        alternatives[targetFw] = targetPath;
                    }
                }
            string fullPath = vsProject?.Properties?.Item("FullPath")?.Value?.ToString();

            string outputPath = vsProject?.ConfigurationManager?.ActiveConfiguration?.Properties?.Item("OutputPath")?.Value?.ToString();
            if (fullPath != null || outputPath != null)
            {
                string outputDir = Path.Combine(fullPath, outputPath);
                string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
                if (!string.IsNullOrWhiteSpace(outputFileName))
                {
                    string assemblyPath = Path.Combine(outputDir, outputFileName);
                    alternatives["classic"] = assemblyPath;
                }
            }
            return alternatives.OrderByDescending(x => x.Key == "classic"
                ? 10
                : DesktopFrameworkRegex.IsMatch(x.Key)
                    ? 9
                    : x.Key.StartsWith("netstandard") ? 8 : 0).FirstOrDefault().Value;
        }

        static UnconfiguredProject GetUnconfiguredProject(IVsProject project)
        {
            IVsBrowseObjectContext context = project as IVsBrowseObjectContext;
            if (context == null)
            { // VC implements this on their DTE.Project.Object
                IVsHierarchy hierarchy = project as IVsHierarchy;
                if (hierarchy != null)
                {
                    object extObject;
                    if (ErrorHandler.Succeeded(hierarchy.GetProperty((uint)VSConstants.VSITEMID.Root, (int)__VSHPROPID.VSHPROPID_ExtObject, out extObject)))
                    {
                        EnvDTE.Project dteProject = extObject as EnvDTE.Project;
                        if (dteProject != null)
                        {
                            context = dteProject.Object as IVsBrowseObjectContext;
                        }
                    }
                }
            }

            return context != null ? context.UnconfiguredProject : null;
        }


        static UnconfiguredProject GetUnconfiguredProject(EnvDTE.Project project)
        {
            IVsBrowseObjectContext context = project as IVsBrowseObjectContext;
            if (context == null && project != null)
            { // VC implements this on their DTE.Project.Object
                context = project.Object as IVsBrowseObjectContext;
            }

            return context != null ? context.UnconfiguredProject : null;
        }

        public static Project GetContainerProject(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName))
            {
                return null;
            }

            var dte2 = (DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key,
            Func<TKey, TValue> getter)
        {
            TValue rv;
            if (!dic.TryGetValue(key, out rv))
                dic[key] = rv = getter(key);
            return rv;
        }

        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key) where TValue :new()
        {
            TValue rv;
            if (!dic.TryGetValue(key, out rv))
                dic[key] = rv = new TValue();
            return rv;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key) 
        {
            TValue rv;
            if (!dic.TryGetValue(key, out rv))
                return default(TValue);
            return rv;
        }
    }
}
