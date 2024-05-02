// This file is originally from dnlib. Find the original source here:
// https://github.com/yck1509/ConfuserEx/blob/3e3e4ae8ef01e3a169591e9b7803408e38cce7ca/Confuser.Core/DnlibUtils.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using dnlib.DotNet;

namespace Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;

internal static class DnlibExtension
{
    /// <summary>
    /// Determines whether the specified property is public.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <returns><c>true</c> if the specified property is public; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this PropertyDef property)
    {
        return property.GetMethod.IsPublic()
            || property.SetMethod.IsPublic()
            || property.OtherMethods?.Any(method => method.IsPublic()) == true;
    }

    /// <summary>
    /// Determines whether the specified property is internal.
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternal(this PropertyDef property)
    {
        return property.GetMethod is { IsPrivate: true, Access: MethodAttributes.Assembly }
            || property.SetMethod is { IsPrivate: true, Access: MethodAttributes.Assembly }
            || property.OtherMethods?.Any(method => method is { IsPrivate: true, Access: MethodAttributes.Assembly }) == true;
    }

    /// <summary>
    /// Determines whether the specified event is public.
    /// </summary>
    /// <param name="evt">The event.</param>
    /// <returns><c>true</c> if the specified event is public; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this EventDef evt)
    {
        return evt.AddMethod?.IsPublic == true
            || evt.RemoveMethod?.IsPublic == true
            || evt.InvokeMethod?.IsPublic == true
            || evt.OtherMethods?.Any(method => method.IsPublic) == true;
    }

    /// <summary>
    /// Determines whether the specified event is internal.
    /// </summary>
    /// <param name="evt">The event.</param>
    /// <returns><c>true</c> if the specified event is internal; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInternal(this EventDef evt)
    {
        return evt.AddMethod is { IsPrivate: true, Access: MethodAttributes.Assembly }
            || evt.RemoveMethod is { IsPrivate: true, Access: MethodAttributes.Assembly }
            || evt.InvokeMethod is { IsPrivate: true, Access: MethodAttributes.Assembly }
            || evt.OtherMethods?.Any(method => method is { IsPrivate: true, Access: MethodAttributes.Assembly }) == true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublic(this MethodDef methodDef)
                => methodDef?.IsPublic == true;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPublicOrInternal(this MethodDef methodDef)
        => methodDef?.IsPublic == true || methodDef?.Access == MethodAttributes.Assembly;

    public static IEnumerable<string> GetVisibleTo(this AssemblyDef assemblyDef)
    {
        var result = assemblyDef.CustomAttributes
                 .Where(att => att.TypeFullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute")
                 .Select(att => att.ConstructorArguments[0].Value.ToString())
                 .Where(val => val is not null)
                 .ToArray();
        return result!;
    }

    /// <summary>
    /// Is childTypeDef a subclass of parentTypeDef. Does not test interface inheritance
    /// </summary>
    /// <param name="childTypeDef"></param>
    /// <param name="parentTypeDef"></param>
    /// <returns></returns>
    public static bool IsSubclassOf(this TypeDef childTypeDef, TypeDef parentTypeDef) =>
       childTypeDef.MDToken != parentTypeDef.MDToken
       && childTypeDef.EnumerateBaseClasses().Any(b => Equals(b, parentTypeDef));

    /// <summary>
    /// Does childType inherit from parentInterface
    /// </summary>
    /// <param name="childType"></param>
    /// <param name="parentInterfaceDef"></param>
    /// <returns></returns>
    public static bool DoesAnySubTypeImplementInterface(this TypeDef childType, TypeDef parentInterfaceDef)
    {
        Debug.Assert(parentInterfaceDef.IsInterface);

        return
            childType
            .EnumerateBaseClasses()
            .Any(typeDefinition => typeDefinition?.DoesSpecificTypeImplementInterface(parentInterfaceDef) == true);
    }

    /// <summary>
    /// Does the childType directly inherit from parentInterface. Base
    /// classes of childType are not tested
    /// </summary>
    /// <param name="childTypeDef"></param>
    /// <param name="parentInterfaceDef"></param>
    /// <returns></returns>
    public static bool DoesSpecificTypeImplementInterface(this TypeDef childTypeDef, TypeDef parentInterfaceDef)
    {
        Debug.Assert(parentInterfaceDef.IsInterface);
        return childTypeDef
       .Interfaces
       .Any(ifaceDef => DoesSpecificInterfaceImplementInterface(ifaceDef.Interface.ResolveTypeDef(), parentInterfaceDef));
    }

    /// <summary>
    /// Does interface iface0 equal or implement interface iface1
    /// </summary>
    /// <param name="iface0"></param>
    /// <param name="iface1"></param>
    /// <returns></returns>
    public static bool DoesSpecificInterfaceImplementInterface(TypeDef iface0, TypeDef iface1)
    {
        if (iface0 is null || iface1 is null)
        {
            return false;
        }
        Debug.Assert(iface1?.IsInterface == true);
        Debug.Assert(iface0?.IsInterface == true);
        return Equals(iface0, iface1) || iface0?.DoesAnySubTypeImplementInterface(iface1) == true;
    }

    /// <summary>
    /// Is source type assignable to target type
    /// </summary>
    /// <param name="target"></param>
    /// <param name="source"></param>
    /// <returns></returns>
    public static bool IsAssignableFrom(this TypeDef target, TypeDef source)
        => target == source
      || Equals(target, source)
      || source.IsSubclassOf(target)
      || target.IsInterface && source.DoesAnySubTypeImplementInterface(target);

    /// <summary>
    /// Enumerate the current type, it's parent and all the way to the top type
    /// </summary>
    /// <param name="classType"></param>
    /// <returns></returns>
    public static IEnumerable<TypeDef> EnumerateBaseClasses(this TypeDef classType)
    {
        for (var typeDefinition = classType; typeDefinition != null; typeDefinition = typeDefinition.BaseType.ResolveTypeDef())
        {
            yield return typeDefinition;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static bool Equals(TypeDef a, TypeDef b)
    {
        return
            a.MDToken == b.MDToken
            && a.FullName == b.FullName;
    }

    /// <summary>
    /// Enumerate all method hierarchy of the specified <paramref name="typeDef"/> up to <see cref="Object" />
    /// </summary>
    /// <param name="typeDef"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<MethodDef> GetMethodsHierarchy(this TypeDef typeDef)
    {
        return GetMethodsHierarchy(typeDef, t => !string.Equals(t.Name, nameof(Object), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Enumerate all methods hierarchy of given <paramref name="typeDef"/> while the result of predicate <paramref name="while"/> is true
    /// </summary>
    /// <param name="typeDef">The <see cref="TypeDef>"/> whose methods to enumerate</param>
    /// <param name="while">the condition until enumerate the methods</param>
    /// <returns></returns>
    public static IEnumerable<MethodDef> GetMethodsHierarchy(this TypeDef typeDef,
        Predicate<TypeDef> @while)
    {
        var currentType = typeDef;
        GenericInstSig? last = default;
        while (currentType is not null && @while(currentType))
        {
            foreach (var m in currentType!.Methods)
            {
                var currentMethod = m;
                if (last is { GenericArguments.Count: > 0 })
                {
                    if (GenericArgumentResolver.Resolve(currentMethod.MethodSig, last.GenericArguments) is { } sign)
                    {
                        currentMethod = new MethodDefUser(new UTF8String(m.Name),
                            sign,
                            m.ImplAttributes,
                            m.Attributes);
                    }
                }
                yield return currentMethod;
            }
            var baseType = currentType?.BaseType;
            last = baseType?.TryGetGenericInstSig();
            currentType = baseType?.ResolveTypeDef();
        }
    }
}
