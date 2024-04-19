using System;
using System.Collections.Generic;

namespace AvaloniaVS
{
    static class EnumerableExtesions
    {
        public static TSource FirstOrDefault<TSource, TArg>(this IEnumerable<TSource> source, Func<TSource, TArg, bool> predicate, TArg arg)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

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
}
