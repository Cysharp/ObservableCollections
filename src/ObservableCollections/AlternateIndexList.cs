using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ObservableCollections;

public class AlternateIndexList<T> : IEnumerable<T>
{
    readonly IndexedValueList list; // alternate index is ordered

    public AlternateIndexList()
    {
        this.list = new();
    }

    public AlternateIndexList(IEnumerable<(int OrderedAlternateIndex, T Value)> values)
    {
        this.list = new();
        this.list.AddRange(values.Select(x => new IndexedValue(x.OrderedAlternateIndex, x.Value)));
    }

    public void UpdateAlternateIndex(int startIndex, int incr)
    {
        var span = list.AsSpan();
        for (int i = startIndex; i < span.Length; i++)
        {
            span[i].AlternateIndex += incr;
        }
    }

    public T this[int index]
    {
        get => list[index].Value;
        set => list[index].Value = value;
    }

    public int GetAlternateIndex(int index) => list[index].AlternateIndex;

    public int Count => list.Count;

    public int Insert(int alternateIndex, T value)
    {
        var index = list.BinarySearch(alternateIndex);
        if (index < 0)
        {
            index = ~index;
        }
        list.Insert(index, new(alternateIndex, value));
        UpdateAlternateIndex(index + 1, 1);
        return index;
    }

    public int InsertRange(int startingAlternateIndex, IEnumerable<T> values)
    {
        var index = list.BinarySearch(startingAlternateIndex);
        if (index < 0)
        {
            index = ~index;
        }

        using var iter = new InsertIterator(startingAlternateIndex, values);
        list.InsertRange(index, iter);
        UpdateAlternateIndex(index + iter.ConsumedCount, iter.ConsumedCount);
        return index;
    }

    public int Remove(T value)
    {
        var index = list.IndexOfValue(value);
        if (index != -1)
        {
            list.RemoveAt(index);
            UpdateAlternateIndex(index, -1);
        }
        return index;
    }

    public int RemoveAt(int alternateIndex)
    {
        var index = list.BinarySearch(alternateIndex);
        if (index >= 0)
        {
            list.RemoveAt(index);
            UpdateAlternateIndex(index, -1);
        }
        else
        {
            throw new InvalidOperationException("Index was not found. AlternateIndex:" + alternateIndex);
        }
        return index;
    }

    public int RemoveRange(int alternateIndex, int count)
    {
        var index = list.BinarySearch(alternateIndex);
        if (index < 0)
        {
            index = ~index;
        }

        list.RemoveRange(index, count);
        UpdateAlternateIndex(index, -count);
        return index;
    }

    public bool TryGetAtAlternateIndex(int alternateIndex, [MaybeNullWhen(true)] out T value)
    {
        var index = list.BinarySearch(alternateIndex);
        if (index < 0)
        {
            value = default!;
            return false;
        }
        value = list[index].Value!;
        return true;
    }

    public bool TrySetAtAlternateIndex(int alternateIndex, T value, out int setIndex)
    {
        setIndex = list.BinarySearch(alternateIndex);
        if (setIndex < 0)
        {
            return false;
        }
        list[setIndex].Value = value;
        return true;
    }

    /// <summary>NOTE: when replace successfully, list has been sorted.</summary>
    public bool TryReplaceAlternateIndex(int getAlternateIndex, int setAlternateIndex)
    {
        var index = list.BinarySearch(getAlternateIndex);
        if (index < 0)
        {
            return false;
        }

        list[index].AlternateIndex = setAlternateIndex;
        list.Sort(); // needs sort to keep order
        return true;
    }

    public bool TryReplaceByValue(T searchValue, T replaceValue, out int replacedIndex)
    {
        replacedIndex = list.IndexOfValue(searchValue);
        if (replacedIndex != -1)
        {
            list[replacedIndex].Value = replaceValue;
            return true;
        }
        return false;
    }

    public void Clear()
    {
        list.Clear();
    }

    public void Clear(IEnumerable<(int OrderedAlternateIndex, T Value)> values)
    {
        list.Clear();
        list.AddRange(values.Select(x => new IndexedValue(x.OrderedAlternateIndex, x.Value)));
    }

    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in list)
        {
            yield return item.Value;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerable<(int AlternateIndex, T Value)> GetIndexedValues()
    {
        foreach (var item in list)
        {
            yield return (item.AlternateIndex, item.Value);
        }
    }

    // Assigns consecutive alternate indexes to the values while they are inserted.
    // It is both the sequence and its single enumerator, so it can be enumerated only once,
    // and ConsumedCount is the number of inserted values only after IndexedValueList.InsertRange has enumerated it to the end.
    class InsertIterator(int startingIndex, IEnumerable<T> values) : IEnumerable<IndexedValue>, IEnumerator<IndexedValue>
    {
        IEnumerator<T> iter = values.GetEnumerator();
        IndexedValue current;

        public int ConsumedCount { get; private set; }

        public IndexedValue Current => current;

        object IEnumerator.Current => Current;

        public void Dispose() => iter.Dispose();

        public bool MoveNext()
        {
            if (iter.MoveNext())
            {
                ConsumedCount++;
                current = new(startingIndex++, iter.Current);
                return true;
            }
            return false;
        }

        public void Reset() { }

        public IEnumerator<IndexedValue> GetEnumerator() => this;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    // A growable list of IndexedValue that owns its backing array, so that the elements can be modified in place
    // without relying on the private field layout of List<T>.
    //
    // Its operations follow List<T>, with one deliberate difference: modifying an element in place through the indexer
    // or AsSpan is not a structural change, so it does not invalidate enumeration. FiltableSynchronizedViewList enumerates
    // this list while holding a reentrant lock, and the source collection can be changed on the same thread during
    // that enumeration. Adding an element that the filter rejects only shifts alternate indexes, and replacing an element
    // only replaces a value; neither must break the enumeration. This is also how the list behaved when it was modified
    // through a span over the backing array of List<T>.
    sealed class IndexedValueList
    {
        const int DefaultCapacity = 4;
        const int ArrayMaxLength = 0x7FFFFFC7; // Array.MaxLength, which is not available on netstandard

        IndexedValue[] items = Array.Empty<IndexedValue>();
        int size;
        int version;

        public int Count => size;

        // Returns a reference so that the caller can modify the element in place.
        // The reference points into the current backing array, so do not keep it across an operation that may grow the list.
        public ref IndexedValue this[int index]
        {
            get
            {
                if ((uint)index >= (uint)size)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return ref items[index];
            }
        }

        // The returned span aliases the backing array. It is invalidated by any structural change, so do not keep it across one.
        public Span<IndexedValue> AsSpan() => items.AsSpan(0, size);

        public int BinarySearch(IndexedValue item)
        {
            return Array.BinarySearch(items, 0, size, item, null);
        }

        public int IndexOfValue(T value)
        {
            var span = AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                if (EqualityComparer<T>.Default.Equals(span[i].Value, value))
                {
                    return i;
                }
            }
            return -1;
        }

        public void Add(IndexedValue item)
        {
            if (size == items.Length)
            {
                Grow(size + 1);
            }
            items[size++] = item;
            version++;
        }

        public void AddRange(IEnumerable<IndexedValue> source)
        {
            foreach (var item in source)
            {
                Add(item);
            }
        }

        public void Insert(int index, IndexedValue item)
        {
            if ((uint)index > (uint)size)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (size == items.Length)
            {
                Grow(size + 1);
            }
            if (index < size)
            {
                Array.Copy(items, index, items, index + 1, size - index);
            }
            items[index] = item;
            size++;
            version++;
        }

        // Like List<T>.InsertRange with a source that is not an ICollection<T>, the elements are inserted one by one,
        // because the number of elements is not known in advance.
        public void InsertRange(int index, IEnumerable<IndexedValue> source)
        {
            if ((uint)index > (uint)size)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            foreach (var item in source)
            {
                Insert(index++, item);
            }
        }

        public void RemoveAt(int index)
        {
            if ((uint)index >= (uint)size)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            size--;
            if (index < size)
            {
                Array.Copy(items, index + 1, items, index, size - index);
            }
            if (RuntimeHelpersEx.IsReferenceOrContainsReferences<IndexedValue>())
            {
                items[size] = default; // do not keep the reference alive
            }
            version++;
        }

        public void RemoveRange(int index, int count)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (size - index < count)
            {
                throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            }

            if (count > 0)
            {
                size -= count;
                if (index < size)
                {
                    Array.Copy(items, index + count, items, index, size - index);
                }
                if (RuntimeHelpersEx.IsReferenceOrContainsReferences<IndexedValue>())
                {
                    Array.Clear(items, size, count); // do not keep the references alive
                }
                version++;
            }
        }

        // Like List<T>.Sort, this invalidates enumeration even when there is nothing to reorder.
        public void Sort()
        {
            if (size > 1)
            {
                Array.Sort(items, 0, size, null);
            }
            version++;
        }

        // Like List<T>.Clear, this invalidates enumeration even when the list is already empty.
        public void Clear()
        {
            if (RuntimeHelpersEx.IsReferenceOrContainsReferences<IndexedValue>() && size > 0)
            {
                Array.Clear(items, 0, size); // do not keep the references alive
            }
            size = 0;
            version++;
        }

        public Enumerator GetEnumerator() => new(this);

        void Grow(int minimumCapacity)
        {
            var newCapacity = items.Length == 0 ? DefaultCapacity : 2 * items.Length;
            if ((uint)newCapacity > ArrayMaxLength)
            {
                newCapacity = ArrayMaxLength;
            }
            if (newCapacity < minimumCapacity)
            {
                newCapacity = minimumCapacity;
            }
            Array.Resize(ref items, newCapacity);
        }

        // Like List<T>.Enumerator, this captures the version when it is created and throws InvalidOperationException
        // from MoveNext once the list has been structurally changed or sorted, including after the last element.
        public struct Enumerator
        {
            readonly IndexedValueList list;
            readonly int version;
            int index;
            IndexedValue current;

            internal Enumerator(IndexedValueList list)
            {
                this.list = list;
                this.version = list.version;
                this.index = 0;
                this.current = default;
            }

            public IndexedValue Current => current;

            public bool MoveNext()
            {
                if (version != list.version)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                if (index < list.size)
                {
                    current = list.items[index++];
                    return true;
                }

                current = default;
                return false;
            }
        }
    }

    struct IndexedValue : IComparable<IndexedValue>
    {
        public int AlternateIndex; // mutable
        public T Value; // mutable

        public IndexedValue(int alternateIndex, T value)
        {
            this.AlternateIndex = alternateIndex;
            this.Value = value;
        }

        public static implicit operator IndexedValue(int alternateIndex) // for query
        {
            return new IndexedValue(alternateIndex, default!);
        }

        public int CompareTo(IndexedValue other)
        {
            return AlternateIndex.CompareTo(other.AlternateIndex);
        }

        public override string ToString()
        {
            return (AlternateIndex, Value).ToString();
        }
    }
}
