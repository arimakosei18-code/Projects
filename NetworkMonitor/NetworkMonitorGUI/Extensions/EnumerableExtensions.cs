using System;
using System.Collections.Generic;
using System.Linq;

namespace NetworkMonitorGUI.Extensions
{
    public static class EnumerableExtensions
    {
        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            
            var list = source as IList<T> ?? source.ToList();
            count = Math.Min(count, list.Count);
            
            for (int i = list.Count - count; i < list.Count; i++)
            {
                yield return list[i];
            }
        }
        
        public static IEnumerable<T> SkipLast<T>(this IEnumerable<T> source, int count)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            
            var list = source as IList<T> ?? source.ToList();
            count = Math.Min(count, list.Count);
            
            for (int i = 0; i < list.Count - count; i++)
            {
                yield return list[i];
            }
        }
    }
}