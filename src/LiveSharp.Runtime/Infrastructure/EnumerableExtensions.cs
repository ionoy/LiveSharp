using System;
using System.Collections.Generic;

namespace LiveSharp.Runtime.Infrastructure
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<TV> SelectSmart<T, TV>(this IEnumerable<T> sequence, Func<T, (bool isFirst, bool isLast, int index), TV> mapper)
        {
            var counter = 0;
            T element = default; 
            
            foreach (var value in sequence) {
                if (counter > 0)
                    yield return mapper(element, (counter == 1, false, counter - 1));
                
                element = value;
                counter++;
            }
            
            if (counter > 0)
                yield return mapper(element, (counter == 1, true, counter - 1));
        }
    }
}