using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Ide.CompletionEngine.AssemblyMetadata;
using dnlib.DotNet;

namespace Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;

public class DnlibMetadataProvider : IMetadataProvider
{
    public IMetadataReaderSession GetMetadata(IEnumerable<string> paths)
    {
        return new DnlibMetadataProviderSession(paths.ToArray());
    }
}

internal class DnlibMetadataProviderSession : IMetadataReaderSession
{
    private readonly ModuleContext _modCtx;
    private readonly Dictionary<ITypeDefOrRef, ITypeDefOrRef> _baseTypes = new Dictionary<ITypeDefOrRef, ITypeDefOrRef>();
    private readonly Dictionary<ITypeDefOrRef, TypeDef> _baseTypeDefs = new Dictionary<ITypeDefOrRef, TypeDef>();
    public string? TargetAssemblyName { get; private set; }
    public IReadOnlyCollection<IAssemblyInformation> Assemblies { get; }
    public DnlibMetadataProviderSession(string[] directoryPath)
    {
        var asmResolver = new AssemblyResolver()
        {
            EnableTypeDefCache = false,
            UseGAC = false
        };
        var resolver = new Resolver(asmResolver)
        {
            ProjectWinMDRefs = false
        };
        _modCtx = new ModuleContext(asmResolver, resolver);
        asmResolver.DefaultModuleContext = _modCtx;

        if (directoryPath == null || directoryPath.Length == 0)
        {
            TargetAssemblyName = null;
            Assemblies = Array.Empty<IAssemblyInformation>();
        }
        else
        {
            TargetAssemblyName = System.Reflection.AssemblyName.GetAssemblyName(directoryPath[0]).ToString();
            Assemblies = LoadAssemblies(_modCtx, directoryPath).Select(a => new AssemblyWrapper(a, this)).ToList();
        }
    }

    public TypeDef? GetTypeDef(ITypeDefOrRef type)
    {
        if (type == null)
        {
            return null;
        }

        if (type is TypeDef typeDef)
        {
            return typeDef;
        }

        if (_baseTypeDefs.TryGetValue(type, out var baseType))
        {
            return baseType;
        }
        else
        {
            return _baseTypeDefs[type] = type.ResolveTypeDef();
        }
    }

    public ITypeDefOrRef GetBaseType(ITypeDefOrRef type)
    {
        if (_baseTypes.TryGetValue(type, out var baseType))
        {
            return baseType;
        }
        else
        {
            return _baseTypes[type] = type.GetBaseType();
        }

    }

    private static List<AssemblyDef> LoadAssemblies(ModuleContext context, string[] lst)
    {
        var asmResovler = (AssemblyResolver)context.AssemblyResolver;

        foreach (var path in lst)
            asmResovler.PreSearchPaths.Add(path);

        List<AssemblyDef> assemblies = new List<AssemblyDef>();

        foreach (var asm in lst)
        {
            try
            {
                var creationOptions = new ModuleCreationOptions(context)
                {
                    TryToLoadPdbFromDisk = false
                };
                var def = AssemblyDef.Load(File.ReadAllBytes(asm), creationOptions);
                asmResovler.AddToCache(def);
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
        _baseTypes.Clear();
        _baseTypeDefs.Clear();
        ((AssemblyResolver)_modCtx.AssemblyResolver).Clear();
    }
}
