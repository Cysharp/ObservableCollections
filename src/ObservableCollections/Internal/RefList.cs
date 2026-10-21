using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ObservableCollections.Internal
{
    // A growable list that owns its backing array and exposes the elements by reference, so that elements of a value type
    // can be modified in place without relying on the private field layout of List<T>.
    //
    // Its operations follow List<T>, with one deliberate difference: modifying an element in place through the indexer
    // or AsSpan is not a structural change, so it does not invalidate enumeration.
    // AlternateIndexList<T> relies on this. FiltableSynchronizedViewList enumerates an AlternateIndexList<T> while holding
    // a reentrant lock, and the source collection can be changed on the same thread during that enumeration.
    // Inserting an element that the filter rejects only shifts the alternate indexes of the following elements,
    // and replacing an element with a value that the filter also accepts only replaces a value; neither must be allowed
    // to break the enumeration.
    internal sealed class RefList<T>
    {
        const int DefaultCapacity = 4;
        const int ArrayMaxLength = 0x7FFFFFC7; // Array.MaxLength, which is not available on netstandard

        T[] items = Array.Empty<T>();
        int size;
        int version;

        public int Count => size;

        // Returns a reference so that the caller can modify the element in place.
        // The reference points into the current backing array. Do not keep it across an operation that adds, removes or reorders elements:
        // the slot may then hold a different element, and growing the list replaces the backing array.
        public ref T this[int index]
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

        // The returned span aliases the backing array. Do not keep it across an operation that adds, removes or reorders elements.
        public Span<T> AsSpan() => items.AsSpan(0, size);

        public int BinarySearch(T item)
        {
            return Array.BinarySearch(items, 0, size, item, null);
        }

        public void Add(T item)
        {
            if (size == items.Length)
            {
                Grow(size + 1);
            }
            items[size++] = item;
            version++;
        }

        public void AddRange(IEnumerable<T> source)
        {
            foreach (var item in source)
            {
                Add(item);
            }
        }

        public void Insert(int index, T item)
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
        public void InsertRange(int index, IEnumerable<T> source)
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
            if (RuntimeHelpersEx.IsReferenceOrContainsReferences<T>())
            {
                items[size] = default!; // do not keep the reference alive
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
                if (RuntimeHelpersEx.IsReferenceOrContainsReferences<T>())
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
            if (RuntimeHelpersEx.IsReferenceOrContainsReferences<T>() && size > 0)
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

        // Like List<T>.Enumerator, this captures the version when it is created, and MoveNext throws InvalidOperationException
        // if the version has changed since then, even when the change happens after the last element has been returned.
        // Every operation changes the version except modifying an element in place through the indexer or AsSpan,
        // and adding or removing zero elements.
        public struct Enumerator
        {
            readonly RefList<T> list;
            readonly int version;
            int index;
            T current;

            internal Enumerator(RefList<T> list)
            {
                this.list = list;
                this.version = list.version;
                this.index = 0;
                this.current = default!;
            }

            public T Current => current;

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

                current = default!;
                return false;
            }
        }
    }
}
