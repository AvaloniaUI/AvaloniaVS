using System.Collections.Generic;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata
{
    public interface IAssemblyProvider
    {
        IEnumerable<string> GetAssemblies(string path);
    }
}
