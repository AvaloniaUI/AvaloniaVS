using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Avalonia.Ide.CompletionEngine
{
    static class Utils
    {
        private static readonly XmlReaderSettings XmlSettings = new XmlReaderSettings()
        {
            DtdProcessing = DtdProcessing.Ignore,
        };

        public static bool CheckAvaloniaRoot(string content)
        {
            return CheckAvaloniaRoot(XmlReader.Create(new StringReader(content), XmlSettings));
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
                        return reader.Value.ToLower() == AvaloniaNamespace;
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
            Func<TKey, TValue> getter)
        {
            TValue rv;
            if (!dic.TryGetValue(key, out rv))
                dic[key] = rv = getter(key);
            return rv;
        }

        public static TValue GetOrCreate<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key) where TValue : new()
        {
            TValue rv;
            if (!dic.TryGetValue(key, out rv))
                dic[key] = rv = new TValue();
            return rv;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, TKey key)
        {
            TValue rv;
            if (!dic.TryGetValue(key, out rv))
                return default(TValue);
            return rv;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dic, params TKey[] keys)
        {
            TValue rv = default;
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
                : default(TValue);
        }

        public static bool TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dic, Func<TKey, bool> keyMatch, out TValue value)
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
    }
}
