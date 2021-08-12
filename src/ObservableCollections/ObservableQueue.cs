using ObservableCollections.Internal;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObservableCollections
{
    public sealed partial class ObservableQueue<T> : IReadOnlyCollection<T>, IObservableCollection<T>
    {
        readonly Queue<T> queue;
        public readonly object SyncRoot = new object();

        public ObservableQueue()
        {
            this.queue = new Queue<T>();
        }

        public ObservableQueue(int capacity)
        {
            this.queue = new Queue<T>(capacity);
        }

        public ObservableQueue(IEnumerable<T> collection)
        {
            this.queue = new Queue<T>(collection);
        }

        public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return queue.Count;
                }
            }
        }

        public void Enqueue(T item)
        {
            lock (SyncRoot)
            {
                var index = queue.Count;
                queue.Enqueue(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, index));
            }
        }

        public void EnqueueRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                var index = queue.Count;
                using (var xs = new CopyedCollection<T>(items))
                {
                    foreach (var item in xs.Span)
                    {
                        queue.Enqueue(item);
                    }
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(xs.Span, index));
                }
            }
        }

        public void EnqueueRange(T[] items)
        {
            lock (SyncRoot)
            {
                var index = queue.Count;
                foreach (var item in items)
                {
                    queue.Enqueue(item);
                }
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public void EnqueueRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                var index = queue.Count;
                foreach (var item in items)
                {
                    queue.Enqueue(item);
                }
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(items, index));
            }
        }

        public T Dequeue()
        {
            lock (SyncRoot)
            {
                var index = queue.Count - 1;
                var v = queue.Dequeue();
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(v, index));
                return v;
            }
        }

        public bool TryDequeue([MaybeNullWhen(false)] out T result)
        {
            lock (SyncRoot)
            {
                var index = queue.Count - 1;
                if (queue.TryDequeue(out result))
                {
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(result, index));
                    return true;
                }
                return false;
            }
        }

        public void DequeueRange(int count)
        {
            lock (SyncRoot)
            {
                var startIndex = queue.Count - count;

                var dest = ArrayPool<T>.Shared.Rent(count);
                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        dest[0] = queue.Dequeue();
                    }

                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(dest.AsSpan(0, count), startIndex));
                }
                finally
                {
                    ArrayPool<T>.Shared.Return(dest);
                }
            }
        }

        public void DequeueRange(Span<T> dest)
        {
            lock (SyncRoot)
            {
                var count = queue.Count;
                var destCount = dest.Length;
                for (int i = 0; i < dest.Length; i++)
                {
                    dest[0] = queue.Dequeue();
                }

                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(dest, count - queue.Count));
            }
        }

        // TODO:
        void Clear()
        {
        }

        //bool Contains(T item)
        //{
        //}

        //void CopyTo(T[] array, int arrayIndex);

        // Peek

        // ToArray

        // TrimExcess

        // EnsureCapacity



        // TryPeek


        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer) where TKey : notnull
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer) where TKey : notnull
        {
            throw new NotImplementedException();
        }
    }
}
