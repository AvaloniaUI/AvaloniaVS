// Extract from https://github.com/yck1509/ConfuserEx/blob/3e3e4ae8ef01e3a169591e9b7803408e38cce7ca/Confuser.Core/DnlibUtils.cs
using System.Collections.Generic;
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
}
