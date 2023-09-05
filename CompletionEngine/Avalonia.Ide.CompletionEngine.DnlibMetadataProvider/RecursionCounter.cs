// This file is originally from dnlib. Find the original source here:
// https://github.com/0xd4d/dnlib/blob/a75105a4600b5641e42e6ac36847661ae9383701/src/DotNet/RecursionCounter.cs
// Find the original license of this file here:
// https://github.com/0xd4d/dnlib/blob/a75105a4600b5641e42e6ac36847661ae9383701/LICENSE.txt

using System;

namespace Avalonia.Ide.CompletionEngine.DnlibMetadataProvider;

/// <summary>
/// Recursion counter
/// </summary>
struct RecursionCounter
{
    /// <summary>
    /// Max recursion count. If this is reached, we won't continue, and will use a default value.
    /// </summary>
    public const int MAX_RECURSION_COUNT = 100;
    int counter;

    /// <summary>
    /// Gets the recursion counter
    /// </summary>
    public int Counter => counter;

    /// <summary>
    /// Increments <see cref="counter"/> if it's not too high. <c>ALL</c> instance methods
    /// that can be called recursively must call this method and <see cref="Decrement"/>
    /// (if this method returns <c>true</c>)
    /// </summary>
    /// <returns><c>true</c> if it was incremented and caller can continue, <c>false</c> if
    /// it was <c>not</c> incremented and the caller must return to its caller.</returns>
    public bool Increment()
    {
        if (counter >= MAX_RECURSION_COUNT)
            return false;
        counter++;
        return true;
    }

    /// <summary>
    /// Must be called before returning to caller if <see cref="Increment"/>
    /// returned <c>true</c>.
    /// </summary>
    public void Decrement()
    {
#if DEBUG
        if (counter <= 0)
            throw new InvalidOperationException("recursionCounter <= 0");
#endif
        counter--;
    }

    /// <inheritdoc/>
    public override string ToString() => counter.ToString();
}
