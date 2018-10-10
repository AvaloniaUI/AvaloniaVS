using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using AvaloniaVS.Helpers;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ProjectSystem;
using Microsoft.VisualStudio.ProjectSystem.Properties;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Threading;

namespace AvaloniaVS
{
    internal static class Utils
    {
        public static string GetFilePath(this ITextView textView)
        {
            return !textView.TextBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument document)
                ? null
                : document.FilePath;
        }

        public static bool IsAvaloniaMarkup(ITextView textView)
        {
            var file = textView.GetFilePath()?.ToLower();
            var edit = file?.EndsWith(".paml") == true;
            if (!edit && file?.EndsWith(".xaml") == true) {
                edit = Utils.CheckAvaloniaRoot(File.ReadAllText(file));
            }
            return edit;
        }

        public static bool CheckAvaloniaRoot(string content)
            => CheckAvaloniaRoot(new XmlTextReader(new StringReader(content)));

        public static bool CheckAvaloniaRoot(XmlReader reader)
        {
            try {
                while (!reader.IsStartElement()) {
                    reader.Read();
                }
                if (!reader.MoveToFirstAttribute()) {
                    return false;
                }

                do {
                    if (reader.Name == "xmlns") {
                        reader.ReadAttributeValue();
                        return reader.Value.ToLower() == AvaloniaNamespace;
                    }
                } while (reader.MoveToNextAttribute());
                return false;
            } catch {
                return false;
            }
        }

        public const string AvaloniaNamespace = "https://github.com/avaloniaui";

        public static Project GetContainingProject(this IWpfTextView textView)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var fileName = textView.GetFilePath();
            if (string.IsNullOrWhiteSpace(fileName)) {
                return null;
            }

            var dte2 = (DTE2)Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        private static string TryGetProperty(Properties props, string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try {
                return props.Item(name).Value.ToString();
            } catch {
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
            ThreadHelper.ThrowIfNotOnUIThread();
            return GetProjectOutputInfo(vsProject)?.FirstOrDefault()?.TargetAssembly;
        }

        public static List<ProjectOutputInfo> GetProjectOutputInfo(this Project vsProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try {
                return GetProjectOutputInfoInternal(vsProject);
            } catch (COMException e) when ((uint)e.HResult == 0x80004005) {
                return null;
            }
        }

        public class ProjectOutputInfo
        {
            public string TargetAssembly { get; set; }

            public bool OutputTypeIsExecutable { get; set; }

            public string TargetFramework { get; set; }

            public bool IsFullDotNet { get; set; }

            public bool IsNetCore { get; set; }
        }

        private static List<ProjectOutputInfo> GetProjectOutputInfoInternal(this Project vsProject)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var alternatives = new Dictionary<string, string>();
            var ucproject = GetUnconfiguredProject(vsProject);
            if (ucproject != null) {
                foreach (var loaded in ucproject.LoadedConfiguredProjects) {
                    var task = loaded?.GetType()?
                    .GetProperty("MSBuildProject",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?
                    .GetMethod.Invoke(loaded, null) as Task<Microsoft.Build.Evaluation.Project>;
                    if (task?.IsCompleted != true) {
                        continue;
                    }

#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    var targetPath = task.Result.AllEvaluatedProperties.FirstOrDefault(p => p.Name == "TargetPath")?.EvaluatedValue;
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
                    if (!string.IsNullOrWhiteSpace(targetPath)) {
                        if (!loaded.ProjectConfiguration.Dimensions.TryGetValue("TargetFramework", out var targetFw)) {
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                            targetFw = task.Result.AllEvaluatedProperties.FirstOrDefault(p => p.Name == "TargetFramework")
#pragma warning restore VSTHRD002 // Avoid problematic synchronous waits
                                               ?.EvaluatedValue ?? "unknown";
                        }

                        alternatives[targetFw] = targetPath;
                    }
                }
            }

            var fullPath = TryGetProperty(vsProject?.Properties, "FullPath");

            var outputPath = vsProject?.ConfigurationManager?.ActiveConfiguration?.Properties?.Item("OutputPath")?.Value?.ToString();
            if (fullPath != null && outputPath != null) {
                var outputDir = Path.Combine(fullPath, outputPath);
                /*
                var dic = new Dictionary<string, string>();
                foreach(Property prop in vsProject.Properties)
                {
                    try
                    {
                        dic[prop.Name] = prop.Value?.ToString();
                    }
                    catch
                    {
                    }
                }*/
                var outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
                if (!string.IsNullOrWhiteSpace(outputFileName)) {
                    var fw = "net46";
                    var tfm = TryGetProperty(vsProject.Properties, "TargetFrameworkMoniker");
                    const string tfmPrefix = ".netframework,version=v";
                    if (tfm != null && tfm.ToLowerInvariant().StartsWith(tfmPrefix)) {
                        fw = "net" + tfm.Substring(tfmPrefix.Length).Replace(".", "");
                    }

                    var assemblyPath = Path.Combine(outputDir, outputFileName);
                    alternatives[fw] = assemblyPath;
                }
            }
            var outputType = TryGetProperty(vsProject?.Properties, "OutputType") ??
                             TryGetProperty(vsProject?.ConfigurationManager?.ActiveConfiguration?.Properties,
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
                            : 0)) {
                var nfo = new ProjectOutputInfo
                {
                    TargetAssembly = alternative.Value,
                    OutputTypeIsExecutable = outputTypeIsExecutable,
                    TargetFramework = alternative.Key == "classic" ? "net46" : alternative.Key
                };
                nfo.IsNetCore = nfo.TargetFramework.StartsWith("netcoreapp");
                nfo.IsFullDotNet = DesktopFrameworkRegex.IsMatch(nfo.TargetFramework);
                lst.Add(nfo);
            }
            return lst;
        }

        private static UnconfiguredProject GetUnconfiguredProject(EnvDTE.Project project)
        {
            return project.GetObjectSafe<IVsBrowseObjectContext>()?.UnconfiguredProject;
        }

        public static Project GetContainerProject(string fileName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (string.IsNullOrWhiteSpace(fileName) || !File.Exists(fileName)) {
                return null;
            }

            var dte2 = (DTE2)Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key,
            Func<TKey, TValue> getter)
        {
            if (!dic.TryGetValue(key, out var rv)) {
                dic[key] = rv = getter(key);
            }

            return rv;
        }

        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key) where TValue : new()
        {
            if (!dic.TryGetValue(key, out var rv)) {
                dic[key] = rv = new TValue();
            }

            return rv;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key)
        {
            if (!dic.TryGetValue(key, out var rv)) {
                return default;
            }

            return rv;
        }
    }
}
