using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata
{
    public class AvaloniaCompilationAssemblyProvider : IAssemblyProvider
    {
        private readonly string _path;

        public AvaloniaCompilationAssemblyProvider(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            _path = path;
        }

        public IEnumerable<string> GetAssemblies()
        {
            try
            {
                return File.ReadAllText(_path).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception ex) when
                (ex is DirectoryNotFoundException || ex is FileNotFoundException)
            {
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to read file '{_path}'.", ex);
            }
        }
    }
}
