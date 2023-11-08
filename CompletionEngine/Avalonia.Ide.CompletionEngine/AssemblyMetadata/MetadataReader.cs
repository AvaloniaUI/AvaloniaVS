namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata;

public class MetadataReader
{
    private readonly IMetadataProvider _provider;

    public MetadataReader(IMetadataProvider provider)
    {
        _provider = provider;
    }

    public Metadata? GetForTargetAssembly(IAssemblyProvider assemblyProvider)
    {
        using var session = _provider.GetMetadata(assemblyProvider.GetAssemblies());
        return MetadataConverter.ConvertMetadata(session);
    }
}
