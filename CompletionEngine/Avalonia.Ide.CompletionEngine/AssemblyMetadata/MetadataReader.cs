using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata;

public class MetadataReader
{
    private readonly IMetadataProvider _provider;
    private readonly IAssemblyProvider _assemblyProvider;

    public MetadataReader(IMetadataProvider provider, IAssemblyProvider assemblyProvider)
    {
        _provider = provider;
        _assemblyProvider = assemblyProvider;
    }

    public Metadata? GetForTargetAssembly(string path)
    {
        if (!File.Exists(path))
            return null;

        using var session = _provider.GetMetadata(_assemblyProvider.GetAssemblies(path));
        return MetadataConverter.ConvertMetadata(session);
    }
}
