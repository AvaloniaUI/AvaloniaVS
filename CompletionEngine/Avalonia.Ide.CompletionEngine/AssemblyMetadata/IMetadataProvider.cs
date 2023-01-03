using System;
using System.Collections.Generic;
using System.Text;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata
{
    public interface IMetadataProvider
    {
        IMetadataReaderSession GetMetadata(IEnumerable<string> paths);
    }

    public interface IMetadataReaderSession : IDisposable
    {
        string TargetAssemblyName { get; }
        IEnumerable<IAssemblyInformation> Assemblies { get; }
    }
}
