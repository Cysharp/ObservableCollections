using System;
using System.Collections.Generic;

namespace ObservableCollections.Internal
{
    internal class RemoveAllMatcher<T>
    {
        readonly HashSet<T> hashSet;

        public RemoveAllMatcher(ReadOnlySpan<T> source)
        {
#if !NETSTANDARD2_0
            var set = new HashSet<T>(capacity: source.Length);
#else
            var set = new HashSet<T>();
#endif
            foreach (var item in source)
            {
                set.Add(item);
            }

            this.hashSet = set;
        }

        public bool Predicate(T value)
        {
            return hashSet.Contains(value);
        }
    }
}