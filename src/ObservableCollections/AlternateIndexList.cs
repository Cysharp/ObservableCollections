using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ObservableCollections;

public class AlternateIndexList<T> : IEnumerable<T>
{
    IndexedValueList list; // alternate index is ordered

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

    // A growable list that owns its backing array, so that the elements can be modified in place
    // without relying on the private field layout of List<T>.
    // It follows the behavior of List<T> for the operations used above, except that modifying an element in place
    // does not invalidate enumeration, because it is not a structural change.
    sealed class IndexedValueList
    {
        const int DefaultCapacity = 4;

        IndexedValue[] items = Array.Empty<IndexedValue>();
        int count;
        int version;

        public int Count => count;

        // Returns a reference so that the caller can modify the element in place.
        public ref IndexedValue this[int index]
        {
            get
            {
                if ((uint)index >= (uint)count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
                return ref items[index];
            }
        }

        // The returned span aliases the backing array. It is invalidated by any structural change, so do not keep it across one.
        public Span<IndexedValue> AsSpan() => items.AsSpan(0, count);

        public int BinarySearch(IndexedValue item)
        {
            return Array.BinarySearch(items, 0, count, item, null);
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
            if (count == items.Length)
            {
                Grow(count + 1);
            }
            items[count++] = item;
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
            if ((uint)index > (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (count == items.Length)
            {
                Grow(count + 1);
            }
            if (index < count)
            {
                Array.Copy(items, index, items, index + 1, count - index);
            }
            items[index] = item;
            count++;
            version++;
        }

        // Like List<T>.InsertRange with a source that is not an ICollection<T>, the elements are inserted one by one,
        // because the number of elements is not known in advance.
        public void InsertRange(int index, IEnumerable<IndexedValue> source)
        {
            if ((uint)index > (uint)count)
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
            if ((uint)index >= (uint)count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            count--;
            if (index < count)
            {
                Array.Copy(items, index + 1, items, index, count - index);
            }
            items[count] = default; // do not keep the reference alive
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
            if (this.count - index < count)
            {
                throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");
            }

            if (count > 0)
            {
                this.count -= count;
                if (index < this.count)
                {
                    Array.Copy(items, index + count, items, index, this.count - index);
                }
                Array.Clear(items, this.count, count); // do not keep the references alive
                version++;
            }
        }

        public void Sort()
        {
            if (count > 1)
            {
                Array.Sort(items, 0, count, null);
            }
            version++;
        }

        public void Clear()
        {
            if (count > 0)
            {
                Array.Clear(items, 0, count); // do not keep the references alive
                count = 0;
            }
            version++;
        }

        // Like the enumerator of List<T>, this throws InvalidOperationException when the list is structurally changed during enumeration.
        public IEnumerator<IndexedValue> GetEnumerator()
        {
            var version = this.version;
            for (int i = 0; i < count; i++)
            {
                yield return items[i];

                if (version != this.version)
                {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }
            }
        }

        void Grow(int minimumCapacity)
        {
            var newCapacity = items.Length == 0 ? DefaultCapacity : items.Length * 2;
            if (newCapacity < minimumCapacity)
            {
                newCapacity = minimumCapacity;
            }
            Array.Resize(ref items, newCapacity);
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
