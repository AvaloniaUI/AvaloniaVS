using System;
using System.Collections.Generic;
using System.IO;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata;

public class MetadataReader
{
    private readonly IMetadataProvider _provider;

    public MetadataReader(IMetadataProvider provider)
    {
        _provider = provider;
    }

    private static IEnumerable<string> GetAssemblies(string path)
    {
        return File.ReadAllText(path).Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
    }

    public Metadata? GetForTargetAssembly(string path)
    {
        if (!File.Exists(path))
            return null;

        using var session = _provider.GetMetadata(GetAssemblies(path));
        return MetadataConverter.ConvertMetadata(session);
    }
}
