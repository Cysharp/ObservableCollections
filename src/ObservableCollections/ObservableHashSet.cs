using ObservableCollections.Internal;
using System.Collections;
using System.Collections.Generic;

namespace ObservableCollections
{
    // can not implements ISet<T> because set operation can not get added/removed values.
    public sealed partial class ObservableHashSet<T> : IReadOnlySet<T>, IReadOnlyCollection<T>, IObservableCollection<T>
    {
        readonly HashSet<T> set;
        public object SyncRoot { get; } = new object();

        public ObservableHashSet()
        {
            this.set = new HashSet<T>();
        }

        public ObservableHashSet(int capacity)
        {
            this.set = new HashSet<T>(capacity);
        }

        public ObservableHashSet(IEnumerable<T> collection)
        {
            this.set = new HashSet<T>(collection);
        }

        public event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return set.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        // TODO: Add, Remove, Set operations.


        public bool Contains(T item)
        {
            lock (SyncRoot)
            {
                return set.Contains(item);
            }
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            lock (SyncRoot)
            {
                return set.IsProperSubsetOf(other);
            }
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            lock (SyncRoot)
            {
                return set.IsProperSupersetOf(other);
            }
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            lock (SyncRoot)
            {
                return set.IsSubsetOf(other);
            }
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            lock (SyncRoot)
            {
                return set.IsSupersetOf(other);
            }
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            lock (SyncRoot)
            {
                return set.Overlaps(other);
            }
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            lock (SyncRoot)
            {
                return set.SetEquals(other);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new SynchronizedEnumerator<T>(SyncRoot, set.GetEnumerator());
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
