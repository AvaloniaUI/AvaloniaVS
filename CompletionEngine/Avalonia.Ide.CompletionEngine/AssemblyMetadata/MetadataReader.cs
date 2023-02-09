using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
        if (Path.GetDirectoryName(path) is not { } directory)
        {
            return Array.Empty<string>();
        }

        var depsPath = Path.Combine(directory,
            Path.GetFileNameWithoutExtension(path) + ".deps.json");
        if (File.Exists(depsPath))
            return DepsJsonAssemblyListLoader.ParseFile(depsPath);
        return Directory.GetFiles(directory).Where(f => f.EndsWith(".dll") || f.EndsWith(".exe"));
    }

    public Metadata? GetForTargetAssembly(string path)
    {
        if (!File.Exists(path))
            return null;

        using var session = _provider.GetMetadata(MetadataReader.GetAssemblies(path));
        return MetadataConverter.ConvertMetadata(session);
    }
}
