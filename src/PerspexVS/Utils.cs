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

namespace PerspexVS
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

        public static bool IsPerspexMarkup(ITextView textView)
        {
            var file = textView.GetFilePath()?.ToLower();
            bool edit = file?.EndsWith(".paml") == true;
            if (!edit && file?.EndsWith(".xaml") == true)
            {
                edit = Utils.CheckPerspexRoot(File.ReadAllText(file));
            }
            return edit;
        }

        public static bool CheckPerspexRoot(string content)
            => CheckPerspexRoot(new XmlTextReader(new StringReader(content)));

        public static bool CheckPerspexRoot(XmlReader reader)
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
                        return reader.Value.ToLower() == PerspexNamespace;
                    }

                } while (reader.MoveToNextAttribute());
                return false;
            }
            catch
            {
                return false;
            }
        }

        public const string PerspexNamespace = "https://github.com/perspex";

        public static Project GetContainingProject(this IWpfTextView textView)
        {
            var fileName = textView.GetFilePath();
            if (string.IsNullOrWhiteSpace(fileName)) return null;
            var dte2 = (DTE2)Package.GetGlobalService(typeof(SDTE));
            var projItem = dte2?.Solution.FindProjectItem(fileName);
            if (projItem != null)
            {
                var props = projItem.Properties.OfType<Property>().ToList();
                var names = props.Select(p => p.Name).ToList();
                Console.WriteLine();
            }
            return projItem?.ContainingProject;
        }

        /// <summary>
        /// Gets the full path of the <see cref="Project"/> configuration
        /// </summary>
        /// <param name="vsProject"></param>
        /// <returns>Target Exe path</returns>
        public static string GetAssemblyPath(this Project vsProject)
        {
            string fullPath = vsProject.Properties.Item("FullPath").Value.ToString();
            string outputPath = vsProject.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value.ToString();
            string outputDir = Path.Combine(fullPath, outputPath);
            string outputFileName = vsProject.Properties.Item("OutputFileName").Value.ToString();
            string assemblyPath = Path.Combine(outputDir, outputFileName);
            return assemblyPath;
        }
    }
}
