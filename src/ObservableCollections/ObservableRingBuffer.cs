using System.Collections;

namespace ObservableCollections
{
    public sealed partial class ObservableRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
    {
        readonly RingBuffer<T> buffer;

        public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        // TODO:SyncRoot

        public ObservableRingBuffer()
        {
            this.buffer = new RingBuffer<T>();
        }

        public ObservableRingBuffer(IEnumerable<T> collection)
        {
            this.buffer = new RingBuffer<T>(collection);
        }

        public bool IsReadOnly => false;

        public object SyncRoot { get; } = new object();

        public T this[int index]
        {
            // TODO:notify!

            get
            {
                return this.buffer[index];
            }
            set
            {
                this.buffer[index] = value;
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
                buffer.AddFirst(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, 0));
            }
        }

        public void AddLast(T item)
        {
            lock (SyncRoot)
            {
                buffer.AddLast(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, buffer.Count - 1));
            }
        }

        // AddFirstRange???

        public void AddLastRange(T[] items)
        {
            lock (SyncRoot)
            {
                foreach (var item in items)
                {
                    buffer.AddLast(item);
                }
            }
        }

        public int IndexOf(T item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        public void Add(T item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }


    // TODO:Is this?
    public sealed class ObservableFixedSizeRingBuffer<T>
    {
        RingBuffer<T> buffer = default!; // TODO:???

        int fixedSize;

        public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        // TODO:SyncRoot
        public bool IsReadOnly => false;

        public object SyncRoot { get; } = new object();

        public void AddLast(T value)
        {
            lock (SyncRoot)
            {
                if (buffer.Count == fixedSize)
                {
                    // Remove One.
                    var removed = buffer.RemoveFirst();
                    buffer.AddLast(value);

                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(removed, 0));
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(value, buffer.Count - 1));
                }
            }
        }

        public void AddLastRange(T[] values)
        {
            lock (SyncRoot)
            {
                if (buffer.Count + values.Length -1 == fixedSize)
                {
                    for (int i = 0; i < values.Length; i++)
                    {
                        buffer.RemoveFirst(); // removes...
                    }
                    for (int i = 0; i < values.Length; i++)
                    {
                        buffer.AddLast(values[i]);
                    }

                    // Remove...
                }

            }
        }
    }
}
