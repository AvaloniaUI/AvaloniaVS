using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Xml;

namespace Avalonia.Ide.CompletionEngine;

internal static class Utils
{
    private static readonly XmlReaderSettings s_xmlSettings = new()
    {
        DtdProcessing = DtdProcessing.Ignore,
    };

    public static bool CheckAvaloniaRoot(string content)
    {
        return CheckAvaloniaRoot(XmlReader.Create(new StringReader(content), s_xmlSettings));
    }

    public static bool CheckAvaloniaRoot(XmlReader reader)
    {
        try
        {
            while (!reader.IsStartElement())
            {
                reader.Read();
            }
            if (!reader.MoveToFirstAttribute())
                return false;
            do
            {
                if (reader.Name == "xmlns")
                {
                    reader.ReadAttributeValue();
                    return reader.Value.ToLower(CultureInfo.InvariantCulture) == AvaloniaNamespace;
                }

            } while (reader.MoveToNextAttribute());
            return false;
        }
        catch
        {
            return false;
        }
    }

    public const string AvaloniaNamespace = "https://github.com/avaloniaui";
    public const string Xaml2006Namespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key,
        Func<TKey, TValue> getter) where TKey : notnull
    {
        if (!dic.TryGetValue(key, out var rv))
            dic[key] = rv = getter(key);
        return rv;
    }

    public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key) where TValue : new() where TKey : notnull
    {
        if (!dic.TryGetValue(key, out var rv))
            dic[key] = rv = new TValue();
        return rv;
    }

    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key) where TKey : notnull
    {
        if (!dic.TryGetValue(key, out var rv))
            return default;
        return rv;
    }

    public static TValue? GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, params TKey[] keys) where TKey : notnull
    {
        TValue? rv = default;
        var found = false;
        for (int i = 0; i < keys.Length; i++)
        {
            if (dic.TryGetValue(keys[i], out rv))
            {
                found = true;
                break;
            }
        }
        return found
            ? rv
            : default;
    }

    public static bool TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dic, Func<TKey, bool> keyMatch,
#if NET6_0_OR_GREATER
        [MaybeNullWhen(false)]
#endif
        out TValue? value)
    {
        foreach (var kv in dic)
        {
            if (keyMatch(kv.Key))
            {
                value = kv.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    public static T? FirstOrDefault<T>(this IEnumerable<T> source, Func<T, T, bool> predicate, T arg)
    {
        foreach (var item in source)
        {
            if (predicate(item, arg))
            {
                return item;
            }
        }
        return default;
    }
}
