using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections
{
    public sealed class FreezedList<T> : IReadOnlyList<T>, IFreezedCollection<T>
    {
        readonly IReadOnlyList<T> list;

        public T this[int index]
        {
            get
            {
                return list[index];
            }
        }

        public int Count
        {
            get
            {
                return list.Count;
            }
        }

        public bool IsReadOnly => true;

        public FreezedList(IReadOnlyList<T> list)
        {
            this.list = list;
        }

        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            return new View<TView>(this, transform, reverse);
        }

        public ISortableSynchronizedView<T, TView> CreateSortableView<TView>(Func<T, TView> transform)
        {
            return new SortableView<TView>(this, transform);
        }

        public bool Contains(T item)
        {
            return list.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class View<TView> : ISynchronizedView<T, TView>
        {
            readonly bool reverse;
            readonly List<(T, TView)> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;
            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;

            public object SyncRoot { get; } = new object();

            public View(FreezedList<T> source, Func<T, TView> selector, bool reverse)
            {
                this.reverse = reverse;
                this.filter = SynchronizedViewFilter<T, TView>.AlwaysTrue;
                this.list = source.Select(x => (x, selector(x))).ToList();
            }

            public int Count
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return list.Count;
                    }
                }
            }

            public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
            {
                lock (SyncRoot)
                {
                    this.filter = filter;
                    foreach (var (value, view) in list)
                    {
                        filter.Invoke(value, view);
                    }
                }
            }

            public void ResetFilter(Action<T, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<T, TView>.AlwaysTrue;
                    if (resetAction != null)
                    {
                        foreach (var (item, view) in list)
                        {
                            resetAction(item, view);
                        }
                    }
                }
            }

            public IEnumerator<(T, TView)> GetEnumerator()
            {
                if (!reverse)
                {
                    return new SynchronizedViewEnumerator<T, TView>(SyncRoot, list.GetEnumerator(), filter);
                }
                else
                {
                    return new SynchronizedViewEnumerator<T, TView>(SyncRoot, list.AsEnumerable().Reverse().GetEnumerator(), filter);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public void Dispose()
            {

            }

            public INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged()
            {
                return new NotifyCollectionChangedSynchronizedView<T, TView>(this);
            }
        }

        class SortableView<TView> : ISortableSynchronizedView<T, TView>
        {
            readonly (T, TView)[] array;

            ISynchronizedViewFilter<T, TView> filter;

            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;
            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;

            public object SyncRoot { get; } = new object();

            public SortableView(FreezedList<T> source, Func<T, TView> selector)
            {
                this.filter = SynchronizedViewFilter<T, TView>.AlwaysTrue;
                this.array = source.Select(x => (x, selector(x))).ToArray();
            }

            public int Count
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return array.Length;
                    }
                }
            }

            public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
            {
                lock (SyncRoot)
                {
                    this.filter = filter;
                    foreach (var (value, view) in array)
                    {
                        filter.Invoke(value, view);
                    }
                }
            }

            public void ResetFilter(Action<T, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<T, TView>.AlwaysTrue;
                    if (resetAction != null)
                    {
                        foreach (var (item, view) in array)
                        {
                            resetAction(item, view);
                        }
                    }
                }
            }

            public IEnumerator<(T, TView)> GetEnumerator()
            {
                return new SynchronizedViewEnumerator<T, TView>(SyncRoot, array.AsEnumerable().GetEnumerator(), filter);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public void Dispose()
            {

            }

            public void Sort(IComparer<T> comparer)
            {
                Array.Sort(array, new TComparer(comparer));
            }

            public void Sort(IComparer<TView> viewComparer)
            {
                Array.Sort(array, new TViewComparer(viewComparer));
            }

            public INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged()
            {
                return new NotifyCollectionChangedSynchronizedView<T, TView>(this);
            }

            class TComparer : IComparer<(T, TView)>
            {
                readonly IComparer<T> comparer;

                public TComparer(IComparer<T> comparer)
                {
                    this.comparer = comparer;
                }

                public int Compare((T, TView) x, (T, TView) y)
                {
                    return comparer.Compare(x.Item1, y.Item1);
                }
            }

            class TViewComparer : IComparer<(T, TView)>
            {
                readonly IComparer<TView> comparer;

                public TViewComparer(IComparer<TView> comparer)
                {
                    this.comparer = comparer;
                }

                public int Compare((T, TView) x, (T, TView) y)
                {
                    return comparer.Compare(x.Item2, y.Item2);
                }
            }
        }
    }
}