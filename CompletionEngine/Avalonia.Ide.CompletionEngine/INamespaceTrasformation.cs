using System.Collections.Generic;

namespace Avalonia.Ide.CompletionEngine;

public interface INamespaceTrasformation
{
   public IEnumerable<char> Apply(IEnumerable<char> input);
}
