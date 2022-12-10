using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata
{
    public class MetadataReader
    {
        private readonly IMetadataProvider _provider;

        public MetadataReader(IMetadataProvider provider)
        {
            _provider = provider;
        }



        IEnumerable<string> GetAssemblies(string path)
        {
            var depsPath = Path.Combine(Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path) + ".deps.json");
            if (File.Exists(depsPath))
                return DepsJsonAssemblyListLoader.ParseFile(depsPath);
            return Directory.GetFiles(Path.GetDirectoryName(path)).Where(f => f.EndsWith(".dll") || f.EndsWith(".exe"));
        }

        public Metadata GetForTargetAssembly(string path)
        {
            if (!File.Exists(path))
                return null;
            
            using (var session = _provider.GetMetadata(GetAssemblies(path)))
                return MetadataConverter.ConvertMetadata(session);
        }
        
    }
}
