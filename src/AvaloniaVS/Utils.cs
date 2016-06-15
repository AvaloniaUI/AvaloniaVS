using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using EnvDTE;
using EnvDTE80;
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

        public const string AvaloniaNamespace = "https://github.com/Avalonia";

        public static Project GetContainingProject(this IWpfTextView textView)
        {
            var fileName = textView.GetFilePath();
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var dte2 = (DTE2)Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            return projItem?.ContainingProject;
        }

        /// <summary>
        /// Gets the full path of the <see cref="Project"/> configuration
        /// </summary>
        /// <param name="vsProject"></param>
        /// <returns>Target Exe path</returns>
        public static string GetAssemblyPath(this Project vsProject)
        {
            string fullPath = vsProject?.Properties?.Item("FullPath")?.Value?.ToString();
            string outputPath = vsProject?.ConfigurationManager?.ActiveConfiguration?.Properties?.Item("OutputPath")?.Value?.ToString();
            if (fullPath == null || outputPath == null)
                return null;
            string outputDir = Path.Combine(fullPath, outputPath);
            string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
            string assemblyPath = Path.Combine(outputDir, outputFileName);
            return assemblyPath;
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
