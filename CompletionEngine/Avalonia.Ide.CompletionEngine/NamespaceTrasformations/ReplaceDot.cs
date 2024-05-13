using System.Collections.Generic;

namespace Avalonia.Ide.CompletionEngine.NamespaceTrasformations;

internal class ReplaceDot : INamespaceTrasformation
{
    private readonly char _sobstituion;

    public ReplaceDot(char sobstituion)
    {
        _sobstituion = sobstituion;
    }

    public IEnumerable<char> Apply(IEnumerable<char> input)
    {
        foreach (char c in input)
        {
            if (c == '.')
                yield return _sobstituion;
            else
            {
                yield return c;
            }
        }
    }
}
