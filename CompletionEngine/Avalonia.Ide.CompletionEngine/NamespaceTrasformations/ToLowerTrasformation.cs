using System.Collections.Generic;

namespace Avalonia.Ide.CompletionEngine.NamespaceTrasformations;

internal class ToLowerTrasformation : INamespaceTrasformation
{
    public IEnumerable<char> Apply(IEnumerable<char> input)
    {
        foreach (char c in input)
        {
            yield return char.ToLowerInvariant(c);
        }
    }
}

