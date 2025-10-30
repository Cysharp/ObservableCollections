using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ObservableCollections
{
    // can not implements ISet<T> because set operation can not get added/removed values.
    public partial class ObservableSortedSet<T> : IReadOnlySet<T>, IReadOnlyCollection<T>, IObservableCollection<T>
        where T : notnull
    {
        readonly SortedSet<T> set;
        public object SyncRoot { get; } = new object();

        public ObservableSortedSet()
        {
            this.set = new SortedSet<T>();
        }

        public ObservableSortedSet(IComparer<T>? comparer)
        {
            this.set = new SortedSet<T>(comparer: comparer);
        }

        public ObservableSortedSet(IEnumerable<T> collection)
        {
            this.set = new SortedSet<T>(collection: collection);
        }

        public ObservableSortedSet(IEnumerable<T> collection, IComparer<T>? comparer)
        {
            this.set = new SortedSet<T>(collection: collection, comparer: comparer);
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

        public bool Add(T item)
        {
            lock (SyncRoot)
            {
                if (set.Add(item))
                {
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(item, -1));
                    return true;
                }

                return false;
            }
        }

        public void AddRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                if (!items.TryGetNonEnumeratedCount(out var capacity))
                {
                    capacity = 4;
                }

                using (var list = new ResizableArray<T>(capacity))
                {
                    foreach (var item in items)
                    {
                        if (set.Add(item))
                        {
                            list.Add(item);
                        }
                    }

                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(list.Span, -1));
                }
            }
        }

        public void AddRange(T[] items)
        {
            AddRange(items.AsSpan());
        }

        public void AddRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                using (var list = new ResizableArray<T>(items.Length))
                {
                    foreach (var item in items)
                    {
                        if (set.Add(item))
                        {
                            list.Add(item);
                        }
                    }

                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Add(list.Span, -1));
                }
            }
        }

        public bool Remove(T item)
        {
            lock (SyncRoot)
            {
                if (set.Remove(item))
                {
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(item, -1));
                    return true;
                }

                return false;
            }
        }

        public void RemoveRange(IEnumerable<T> items)
        {
            lock (SyncRoot)
            {
                if (!items.TryGetNonEnumeratedCount(out var capacity))
                {
                    capacity = 4;
                }

                using (var list = new ResizableArray<T>(capacity))
                {
                    foreach (var item in items)
                    {
                        if (set.Remove(item))
                        {
                            list.Add(item);
                        }
                    }

                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(list.Span, -1));
                }
            }
        }

        public void RemoveRange(T[] items)
        {
            RemoveRange(items.AsSpan());
        }

        public void RemoveRange(ReadOnlySpan<T> items)
        {
            lock (SyncRoot)
            {
                using (var list = new ResizableArray<T>(items.Length))
                {
                    foreach (var item in items)
                    {
                        if (set.Remove(item))
                        {
                            list.Add(item);
                        }
                    }

                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Remove(list.Span, -1));
                }
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                set.Clear();
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<T>.Reset());
            }
        }

#if !NETSTANDARD2_0 && !NET_STANDARD_2_0 && !NET_4_6

        public bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue)
        {
            lock (SyncRoot)
            {
                return set.TryGetValue(equalValue, out actualValue);
            }
        }

#endif

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
            lock (SyncRoot)
            {
                foreach (var item in set)
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IComparer<T> Comparer
        {
            get
            {
                lock (SyncRoot)
                {
                    return set.Comparer;
                }
            }
        }

        // SortedSet-specific properties
        public T? Min
        {
            get
            {
                lock (SyncRoot)
                {
                    return set.Count > 0 ? set.Min : default;
                }
            }
        }

        public T? Max
        {
            get
            {
                lock (SyncRoot)
                {
                    return set.Count > 0 ? set.Max : default;
                }
            }
        }

        // SortedSet-specific methods
        public IEnumerable<T> Reverse()
        {
            lock (SyncRoot)
            {
                return set.Reverse().ToArray();
            }
        }

        public IEnumerable<T> GetViewBetween(T lowerValue, T upperValue)
        {
            lock (SyncRoot)
            {
                return set.GetViewBetween(lowerValue, upperValue).ToArray();
            }
        }
    }
}
