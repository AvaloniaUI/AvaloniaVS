using System;
using System.Text;

namespace Avalonia.Ide.CompletionEngine.Parsing;

public record struct DevToolsSelectorInfo(Range ElementType, Range Namespace, Range AssemblyName = default)
{
    public static string GetFullName(char[] buffer, DevToolsSelectorInfo info)
    {
        var sb = new StringBuilder();
        if (info.Namespace.Start.Value < info.Namespace.End.Value)
        {
            sb.Append(buffer[info.Namespace]);
            sb.Append('.');
        }
        sb.Append(buffer[info.ElementType]);
        return sb.ToString();
    }
}
