

using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ObservableCollections
{
    public sealed class FreezedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IFreezedCollection<KeyValuePair<TKey, TValue>>
        
    {
        readonly IReadOnlyDictionary<TKey, TValue> dictionary;

        public FreezedDictionary(IReadOnlyDictionary<TKey, TValue> dictionary)
        {
            this.dictionary = dictionary;
        }

        public TValue this[TKey key] => dictionary[key];

        public IEnumerable<TKey> Keys => dictionary.Keys;

        public IEnumerable<TValue> Values => dictionary.Values;

        public int Count => dictionary.Count;

        public bool ContainsKey(TKey key)
        {
            return dictionary.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            return dictionary.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)dictionary).GetEnumerator();
        }

        public ISynchronizedView<KeyValuePair<TKey, TValue>, TView> CreateView<TView>(Func<KeyValuePair<TKey, TValue>, TView> transform, bool reverse = false)
        {
            return new FreezedView<KeyValuePair<TKey, TValue>, TView>(dictionary, transform, reverse);
        }

        public ISortableSynchronizedView<KeyValuePair<TKey, TValue>, TView> CreateSortableView<TView>(Func<KeyValuePair<TKey, TValue>, TView> transform)
        {
            return new FreezedSortableView<KeyValuePair<TKey, TValue>, TView>(dictionary, transform);
        }
    }
}