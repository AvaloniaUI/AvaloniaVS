namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata;

public class MetadataReader
{
    private readonly IMetadataProvider _provider;
    private IMetadataReaderSession? _lastSession;

    public MetadataReader(IMetadataProvider provider)
    {
        _provider = provider;
    }

    public Metadata? GetForTargetAssembly(IAssemblyProvider assemblyProvider)
    {
        _lastSession?.Dispose();
        _lastSession = _provider.GetMetadata(assemblyProvider.GetAssemblies());
        return MetadataConverter.ConvertMetadata(_lastSession);
    }
}
