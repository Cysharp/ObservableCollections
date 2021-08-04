using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal struct CopyedCollection<T> : IDisposable
    {
        T[]? array;
        int length;

        public ReadOnlySpan<T> Span => array.AsSpan(0, length);

        public IEnumerable<T> AsEnumerable() => new EnumerableCollection(array, length);

        public CopyedCollection(T item)
        {
            this.array = ArrayPool<T>.Shared.Rent(1);
            this.length = 1;
        }

        public CopyedCollection(IEnumerable<T> source)
        {
            if (Enumerable.TryGetNonEnumeratedCount(source, out var count))
            {
                var array = ArrayPool<T>.Shared.Rent(count);

                if (source is ICollection<T> c)
                {
                    c.CopyTo(array, 0);
                }
                else
                {
                    var i = 0;
                    foreach (var item in source)
                    {
                        array[i++] = item;
                    }
                }
                this.array = array;
                this.length = count;
            }
            else
            {
                var array = ArrayPool<T>.Shared.Rent(count);

                var i = 0;
                foreach (var item in source)
                {
                    TryEnsureCapacity(ref array, i);
                    array[i++] = item;
                }
                this.array = array;
                this.length = i;
            }
        }

        public CopyedCollection(ReadOnlySpan<T> source)
        {
            var array = ArrayPool<T>.Shared.Rent(source.Length);
            source.CopyTo(array);
            this.array = array;
            this.length = source.Length;
        }

        static void TryEnsureCapacity(ref T[] array, int index)
        {
            if (array.Length == index)
            {
                ArrayPool<T>.Shared.Return(array);
            }
            array = ArrayPool<T>.Shared.Rent(index * 2);
        }

        public void Dispose()
        {
            if (array != null)
            {
                ArrayPool<T>.Shared.Return(array);
                array = null;
            }
        }

        // Optimize to use Count and CopyTo
        class EnumerableCollection : ICollection<T>
        {
            readonly T[] array;
            readonly int count;

            public EnumerableCollection(T[]? array, int count)
            {
                if (array == null)
                {
                    this.array = Array.Empty<T>();
                    this.count = 0;
                }
                else
                {
                    this.array = array;
                    this.count = count;
                }
            }

            public int Count => count;

            public bool IsReadOnly => true;

            public void Add(T item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
            public bool Contains(T item) => throw new NotSupportedException();
            public void CopyTo(T[] dest, int destIndex) => array.CopyTo(dest, destIndex);

            public IEnumerator<T> GetEnumerator()
            {
                for (int i = 0; i < count; i++)
                {
                    yield return array[i];
                }
            }

            public bool Remove(T item) => throw new NotSupportedException();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}