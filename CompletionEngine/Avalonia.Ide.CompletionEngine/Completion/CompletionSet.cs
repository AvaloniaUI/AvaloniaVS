using System.Collections.Generic;

namespace Avalonia.Ide.CompletionEngine;

public class CompletionSet
{
    public int StartPosition { get; set; }

    public List<Completion> Completions { get; set; } = new();
}
