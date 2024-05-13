using System;
using System.Collections.Generic;

namespace Avalonia.Ide.CompletionEngine.Parsing;

public class DevToolsSelectorParser
{
    private enum SelectorInfoPart
    {
        AssemblyName = 0,
        Namespace = 1,
        Element = 2,
    }

    public static IReadOnlyList<DevToolsSelectorInfo> Parse(string input)
        => Parse(input.ToCharArray());

    public static IReadOnlyList<DevToolsSelectorInfo> Parse(char[] chars)
    {
        Range[] parts = new Range[3];
        List<DevToolsSelectorInfo> selectorsInfo = new();
        var partStartIndex = -1;
        var partName = SelectorInfoPart.Namespace;

        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            switch (c)
            {
                case '{':
                    partName = SelectorInfoPart.AssemblyName;
                    partStartIndex = i + 1;
                    break;
                case '}' when partName == SelectorInfoPart.AssemblyName:
                    parts[(int)SelectorInfoPart.AssemblyName] = new(partStartIndex, i);
                    partStartIndex = -1;
                    partName = SelectorInfoPart.Namespace;
                    break;
                case '|' when partName == SelectorInfoPart.Namespace && partStartIndex > -1:
                    parts[(int)SelectorInfoPart.Namespace] = new(partStartIndex, i);
                    partName = SelectorInfoPart.Element;
                    partStartIndex = -1;
                    break;
                case '.' or '#' or ':' or ' ' when partName == SelectorInfoPart.Element:
                    parts[(int)SelectorInfoPart.Element] = new(partStartIndex, i);
                    selectorsInfo.Add(new(parts[2], parts[1], parts[0]));
                    parts[0] = default;
                    parts[1] = default;
                    parts[2] = default;
                    partName = SelectorInfoPart.Namespace;
                    break;
                default:
                    if (partName is SelectorInfoPart.Namespace or SelectorInfoPart.Element
                        && partStartIndex == -1
                        && !char.IsWhiteSpace(c))
                    {
                        partStartIndex = i;
                    }
                    break;
            }
        }
        if (partStartIndex > -1 && partName == SelectorInfoPart.Element)
        {
            parts[(int)SelectorInfoPart.Element] = new(partStartIndex, chars.Length);
            selectorsInfo.Add(new(parts[2], parts[1], parts[0]));
            parts[0] = default;
            parts[1] = default;
            parts[2] = default;
        }
        return selectorsInfo;
    }
}
