using ObservableCollections.Internal;
using System.Collections;

namespace ObservableCollections
{
    public sealed partial class ObservableFixedSizeRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
    {
        readonly RingBuffer<T> buffer;
        readonly int capacity;

        public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        public ObservableFixedSizeRingBuffer(int capacity)
        {
            this.capacity = capacity;
            this.buffer = new RingBuffer<T>(capacity);
        }

        public ObservableFixedSizeRingBuffer(int capacity, IEnumerable<T> collection)
        {
            this.capacity = capacity;
            this.buffer = new RingBuffer<T>(capacity);
            foreach (var item in collection)
            {
                if (capacity == buffer.Count)
                {
                    buffer.RemoveFirst();
                }
                buffer.AddLast(item);
            }
        }

        public bool IsReadOnly => false;

        public object SyncRoot { get; } = new object();

        public T this[int index]
        {
            get
            {
                lock (SyncRoot)
                {
                    return this.buffer[index];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    var oldValue = buffer[index];
                    buffer[index] = value;
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Replace(value, oldValue, index));
                }
            }
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return buffer.Count;
                }
            }
        }

        public void AddFirst(T item)
        {
            lock (SyncRoot)
            {
                if (capacity == buffer.Count)
                {
                    buffer.RemoveLast();
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(item, capacity - 1));
                }

                buffer.AddFirst(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, 0));
            }
        }

        public void AddLast(T item)
        {
            lock (SyncRoot)
            {
                if (capacity == buffer.Count)
                {
                    buffer.RemoveLast();
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(item, capacity - 1));
                }

                buffer.AddLast(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, buffer.Count - 1));
            }
        }

        // AddFirstRange is not exists.

        public void AddLastRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                var index = buffer.Count;
                using (var xs = new CloneCollection<T>(items))
                {
                    foreach (var item in xs.Span)
                    {
                        buffer.AddLast(item);
                    }
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
                }
            }
        }

        public void AddLastRange(T[] items)
        {
            lock (SyncRoot)
            {
                var index = buffer.Count;
                foreach (var item in items)
                {
                    buffer.AddLast(item);
                }
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public void AddLastRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                var index = buffer.Count;
                foreach (var item in items)
                {
                    buffer.AddLast(item);
                }
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public int IndexOf(T item)
        {
            lock (SyncRoot)
            {
                return buffer.IndexOf(item);
            }
        }

        void IList<T>.Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Add(T item)
        {
            AddLast(item);
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                buffer.Clear();
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Reset());
            }
        }

        public bool Contains(T item)
        {
            lock (SyncRoot)
            {
                return buffer.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (SyncRoot)
            {
                buffer.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SynchronizedEnumerator<T>(SyncRoot, buffer.GetEnumerator());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // View

        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            // TODO:
            throw new NotImplementedException();
            // return new View<TView>(this, transform, reverse);
        }
    }
}
