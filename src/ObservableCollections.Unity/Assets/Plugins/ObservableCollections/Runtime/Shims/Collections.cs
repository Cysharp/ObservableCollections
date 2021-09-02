

using System;
using System.Collections.Generic;
using System.Text;

namespace System.Collections.Generic
{
    internal static class CollectionExtensions
    {
        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }

        public static bool Remove<TKey, TValue>(this SortedDictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out value))
            {
                return dict.Remove(key);
            }
            return false;
        }

        public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out value))
            {
                return dict.Remove(key);
            }
            return false;
        }

#if !NET6_0_OR_GREATER

        public static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T> source, out int count)
        {
            if (source is ICollection<T> collection)
            {
                count = collection.Count;
                return true;
            }
            if (source is IReadOnlyCollection<T> rCollection)
            {
                count = rCollection.Count;
                return true;
            }
            count = 0;
            return false;
        }

#endif
    }

#if !NET5_0_OR_GREATER

    internal interface IReadOnlySet<T> : System.Collections.Generic.IEnumerable<T>, System.Collections.Generic.IReadOnlyCollection<T>
    {
    }

#endif
}

