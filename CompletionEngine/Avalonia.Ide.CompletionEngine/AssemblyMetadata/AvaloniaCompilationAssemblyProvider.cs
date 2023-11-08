using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata
{
    public class AvaloniaCompilationAssemblyProvider : IAssemblyProvider
    {
        public IEnumerable<string> GetAssemblies(string path)
        {
            return File.ReadAllText(path).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
