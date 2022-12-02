using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using AvMetadata = Avalonia.Ide.CompletionEngine.Metadata;

namespace AvaloniaVS.Shared.Completion.Metadata
{
    public class AvVSMetadataReader
    {
        private readonly AvVSDnLibMetadataProvider _provider;

        public AvVSMetadataReader()
        {
            _provider = new AvVSDnLibMetadataProvider();
        }

        IEnumerable<string> GetAssemblies(string path)
        {
            var depsPath = Path.Combine(Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path) + ".deps.json");
            if (File.Exists(depsPath))
                return DepsJsonAssemblyListLoader.ParseFile(depsPath);
            return Directory.GetFiles(Path.GetDirectoryName(path)).Where(f => f.EndsWith(".dll") || f.EndsWith(".exe"));
        }

        public AvMetadata GetForTargetAssembly(string path)
        {
            if (!File.Exists(path))
                return null;

            using (var session = _provider.GetMetadata(GetAssemblies(path)))
                return AvVSMetadataConverter.ConvertMetadata(session);
        }
    }
}
