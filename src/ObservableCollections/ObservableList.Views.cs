using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections
{
    public sealed partial class ObservableList<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
    {
        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            return new View<TView>(this, transform, reverse);
        }

        public ISynchronizedView<T, TView> CreateSortedView<TView>(Func<T, TView> transform, IComparer<T> comparer)
        {
            return new SortedView<TView>(this, transform, comparer);
        }

        public ISynchronizedView<T, TView> CreateSortedView<TView>(Func<T, TView> transform, IComparer<TView> viewComparer)
        {
            return new ViewComparerSortedView<TView>(this, transform, viewComparer);
        }

        class View<TView> : ISynchronizedView<T, TView>
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;
            readonly bool reverse;
            readonly List<(T, TView)> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; }

            public View(ObservableList<T> source, Func<T, TView> selector, bool reverse)
            {
                this.source = source;
                this.selector = selector;
                this.reverse = reverse;
                this.filter = SynchronizedViewFilter<T, TView>.AlwaysTrue;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.list = source.list.Select(x => (x, selector(x))).ToList();
                    this.source.CollectionChanged += SourceCollectionChanged;
                }
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

            public INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedView<T, TView>(this);
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
                this.source.CollectionChanged -= SourceCollectionChanged;
            }

            private void SourceCollectionChanged(in NotifyCollectionChangedEventArgs<T> e)
            {
                lock (SyncRoot)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            list.EnsureCapacity(e.NewItems.Length);

                            // Add
                            if (e.NewStartingIndex == list.Count)
                            {
                                if (e.IsSingleItem)
                                {
                                    var v = (e.NewItem, selector(e.NewItem));
                                    list.Add(v);
                                    filter.Invoke(v);
                                }
                                else
                                {
                                    foreach (var item in e.NewItems)
                                    {
                                        var v = (item, selector(item));
                                        list.Add(v);
                                        filter.Invoke(v);
                                    }
                                }
                            }
                            // Insert
                            else
                            {
                                if (e.IsSingleItem)
                                {
                                    var v = (e.NewItem, selector(e.NewItem));
                                    list.Insert(e.NewStartingIndex, v);
                                    filter.Invoke(v);
                                }
                                else
                                {
                                    // inefficient copy, need refactoring
                                    var newArray = new (T, TView)[e.NewItems.Length];
                                    var span = e.NewItems;
                                    for (int i = 0; i < span.Length; i++)
                                    {
                                        var v = (span[i], selector(span[i]));
                                        newArray[i] = v;
                                        filter.Invoke(v);
                                    }
                                    list.InsertRange(e.NewStartingIndex, newArray);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (e.IsSingleItem)
                            {
                                list.RemoveAt(e.OldStartingIndex);
                            }
                            else
                            {
                                list.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            // ObservableList does not support replace range
                            {
                                var v = (e.NewItem, selector(e.NewItem));
                                list[e.NewStartingIndex] = v;
                                break;
                            }
                        case NotifyCollectionChangedAction.Move:
                            {
                                var v = (e.NewItem, selector(e.NewItem));
                                list.RemoveAt(e.OldStartingIndex);
                                list.Insert(e.NewStartingIndex, v);
                                filter.Invoke(v);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            list.Clear();
                            break;
                        default:
                            break;
                    }

                    RoutingCollectionChanged?.Invoke(e);
                    CollectionStateChanged?.Invoke(e.Action);
                }
            }
        }

        class SortedView<TView> : ISynchronizedView<T, TView>
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;

            // SortedList is array-based, SortedDictionary is red-black tree based.
            // key as with index to keep uniqueness
            readonly SortedDictionary<(T value, int index), TView> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; } = new object();

            public SortedView(ObservableList<T> source, Func<T, TView> selector, IComparer<T> comparer)
            {
                this.source = source;
                this.selector = selector;
                this.filter = SynchronizedViewFilter<T, TView>.AlwaysTrue;
                lock (source.SyncRoot)
                {
                    var dict = new SortedDictionary<(T, int), TView>(new WithIndexComparer(comparer));
                    var count = source.list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var v = source.list[i];
                        dict.Add((v, i), selector(v));
                    }

                    this.list = dict;
                    this.source.CollectionChanged += SourceCollectionChanged;
                }
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
                    foreach (var (item, view) in list)
                    {
                        filter.Invoke(item.value, view);
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
                            resetAction(item.value, view);
                        }
                    }
                }
            }

            public INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedView<T, TView>(this);
                }
            }

            public IEnumerator<(T, TView)> GetEnumerator()
            {
                return new SynchronizedViewEnumerator<T, TView>(SyncRoot, list.Select(x => (x.Key.value, x.Value)).GetEnumerator(), filter);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public void Dispose()
            {
                this.source.CollectionChanged -= SourceCollectionChanged;
            }

            private void SourceCollectionChanged(in NotifyCollectionChangedEventArgs<T> e)
            {
                lock (SyncRoot)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            // Add, Insert
                            {
                                if (e.IsSingleItem)
                                {
                                    var view = selector(e.NewItem);
                                    list.Add((e.NewItem, e.NewStartingIndex), view);
                                    filter.Invoke(e.NewItem, view);
                                }
                                else
                                {
                                    var index = e.NewStartingIndex;
                                    foreach (var item in e.NewItems)
                                    {
                                        var view = selector(item);
                                        list.Add((item, index++), view);
                                        filter.Invoke(item, view);
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            {
                                if (e.IsSingleItem)
                                {
                                    list.Remove((e.OldItem, e.OldStartingIndex));
                                }
                                else
                                {
                                    var index = e.OldStartingIndex;
                                    foreach (var item in e.OldItems)
                                    {
                                        list.Remove((item, index++));
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                        case NotifyCollectionChangedAction.Move:
                            // ObservableList does not support replace range
                            // Replace is remove old item and insert new item(same index on replace, difference index on move).
                            {
                                var view = selector(e.NewItem);
                                list.Remove((e.OldItem, e.OldStartingIndex));
                                list.Add((e.NewItem, e.NewStartingIndex), view);
                                filter.Invoke(e.NewItem, view);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            list.Clear();
                            break;
                        default:
                            break;
                    }

                    RoutingCollectionChanged?.Invoke(e);
                    CollectionStateChanged?.Invoke(e.Action);
                }
            }

            class WithIndexComparer : IComparer<(T value, int index)>
            {
                readonly IComparer<T> comparer;

                public WithIndexComparer(IComparer<T> comparer)
                {
                    this.comparer = comparer;
                }

                public int Compare((T value, int index) x, (T value, int index) y)
                {
                    var v = comparer.Compare(x.value, y.value);
                    if (v == 0)
                    {
                        v = x.index.CompareTo(y.index);
                    }
                    return v;
                }
            }
        }

        class ViewComparerSortedView<TView> : ISynchronizedView<T, TView>
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;
            readonly Dictionary<(T, int), TView> viewMap; // view-map needs to use in remove.
            readonly SortedDictionary<(TView view, int index), T> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; } = new object();

            public ViewComparerSortedView(ObservableList<T> source, Func<T, TView> selector, IComparer<TView> comparer)
            {
                this.source = source;
                this.selector = selector;
                this.viewMap = new Dictionary<(T, int), TView>(source.Count);
                this.filter = SynchronizedViewFilter<T, TView>.AlwaysTrue;
                lock (source.SyncRoot)
                {
                    var dict = new SortedDictionary<(TView, int), T>(new WithIndexComparer(comparer));
                    var count = source.list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var v = source.list[i];
                        var v2 = selector(v);

                        dict.Add((v2, i), v);
                        viewMap.Add((v, i), v2);
                    }

                    this.list = dict;
                    this.source.CollectionChanged += SourceCollectionChanged;
                }
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
                    foreach (var item in list)
                    {
                        filter.Invoke(item.Value, item.Key.view);
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
                        foreach (var item in list)
                        {
                            resetAction(item.Value, item.Key.view);
                        }
                    }
                }
            }

            public INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedView<T, TView>(this);
                }
            }

            public IEnumerator<(T, TView)> GetEnumerator()
            {
                return new SynchronizedViewEnumerator<T, TView>(SyncRoot, list.Select(x => (x.Value, x.Key.view)).GetEnumerator(), filter);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public void Dispose()
            {
                this.source.CollectionChanged -= SourceCollectionChanged;
            }

            private void SourceCollectionChanged(in NotifyCollectionChangedEventArgs<T> e)
            {
                lock (SyncRoot)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            // Add, Insert
                            {
                                if (e.IsSingleItem)
                                {
                                    var v = selector(e.NewItem);
                                    list.Add((v, e.NewStartingIndex), e.NewItem);
                                    viewMap.Add((e.NewItem, e.NewStartingIndex), v);
                                    filter.Invoke(e.NewItem, v);
                                }
                                else
                                {
                                    var index = e.NewStartingIndex;
                                    foreach (var item in e.NewItems)
                                    {
                                        var v = selector(item);
                                        list.Add((v, index), item);
                                        viewMap.Add((item, index), v);
                                        filter.Invoke(item, v);
                                        index++;
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            {
                                if (e.IsSingleItem)
                                {
                                    if (viewMap.Remove((e.OldItem, e.OldStartingIndex), out var view))
                                    {
                                        list.Remove((view, e.OldStartingIndex));
                                    }
                                }
                                else
                                {
                                    var index = e.OldStartingIndex;
                                    foreach (var item in e.OldItems)
                                    {
                                        if (viewMap.Remove((item, index), out var view))
                                        {
                                            list.Remove((view, index));
                                        }
                                        index++;
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                        case NotifyCollectionChangedAction.Move:
                            // ObservableList does not support replace range
                            // Replace is remove old item and insert new item(same index on replace, diffrence index on move).
                            {
                                if (viewMap.Remove((e.OldItem, e.OldStartingIndex), out var oldView))
                                {
                                    list.Remove((oldView, e.OldStartingIndex));

                                    var newView = selector(e.NewItem);
                                    list.Add((newView, e.NewStartingIndex), e.NewItem);
                                    viewMap.Add((e.NewItem, e.NewStartingIndex), newView);
                                    filter.Invoke(e.NewItem, newView);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            list.Clear();
                            viewMap.Clear();
                            break;
                        default:
                            break;
                    }

                    RoutingCollectionChanged?.Invoke(e);
                    CollectionStateChanged?.Invoke(e.Action);
                }
            }

            class WithIndexComparer : IComparer<(TView value, int index)>
            {
                readonly IComparer<TView> comparer;

                public WithIndexComparer(IComparer<TView> comparer)
                {
                    this.comparer = comparer;
                }

                public int Compare((TView value, int index) x, (TView value, int index) y)
                {
                    var v = comparer.Compare(x.value, y.value);
                    if (v == 0)
                    {
                        v = x.index.CompareTo(y.index);
                    }
                    return v;
                }
            }
        }
    
        
    }
}