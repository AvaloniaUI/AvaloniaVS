using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using dnlib.DotNet;

namespace Avalonia.Ide.CompletionEngine.DnlibMetadataProvider
{
    public class DnlibMetadataProvider : IMetadataProvider
    {
        
        public IMetadataReaderSession GetMetadata(IEnumerable<string> paths)
        {
            return new DnlibMetadataProviderSession(paths.ToArray());
        }
    }

    class DnlibMetadataProviderSession :IMetadataReaderSession
    { 
        public IEnumerable<IAssemblyInformation> Assemblies { get; }
        public DnlibMetadataProviderSession(string[] directoryPath)
        {
            Assemblies = LoadAssemblies(directoryPath).Select(a => new AssemblyWrapper(a)).ToList();
        }

        static List<AssemblyDef> LoadAssemblies(string[] lst)
        {
            AssemblyResolver asmResolver = new AssemblyResolver();
            ModuleContext modCtx = new ModuleContext(asmResolver);
            asmResolver.DefaultModuleContext = modCtx;
            asmResolver.EnableTypeDefCache = true;

            foreach (var path in lst)
                asmResolver.PreSearchPaths.Add(path);

            List<AssemblyDef> assemblies = new List<AssemblyDef>();

            foreach (var asm in lst)
            {
                try
                {
                    var def = AssemblyDef.Load(File.ReadAllBytes(asm));
                    def.Modules[0].Context = modCtx;
                    asmResolver.AddToCache(def);
                    assemblies.Add(def);
                }
                catch
                {
                    //Ignore
                }
            }

            return assemblies;
        }

        public void Dispose()
        {
            //no-op
        }
    }
}
