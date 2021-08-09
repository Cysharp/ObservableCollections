using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
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

        public void Dequeue()
        {
            // this.queue.



        }

        // TryDequeue

        // DequeueRange

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
