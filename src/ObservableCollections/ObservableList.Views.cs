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

        public ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
            where TKey : notnull
        {
            return new SortedView<TKey, TView>(this, identitySelector, transform, comparer);
        }

        public ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer)
            where TKey : notnull
        {
            return new ViewComparerSortedView<TKey, TView>(this, identitySelector, transform, viewComparer);
        }

        class View<TView> : ISynchronizedView<T, TView>
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;
            readonly bool reverse;
            protected readonly List<(T, TView)> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; }

            public View(ObservableList<T> source, Func<T, TView> selector, bool reverse)
            {
                this.source = source;
                this.selector = selector;
                this.reverse = reverse;
                this.filter = SynchronizedViewFilter<T, TView>.Null;
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
                        filter.InvokeOnAttach(value, view);
                    }
                }
            }

            public void ResetFilter(Action<T, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<T, TView>.Null;
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
                                    filter.InvokeOnAdd(v);
                                }
                                else
                                {
                                    foreach (var item in e.NewItems)
                                    {
                                        var v = (item, selector(item));
                                        list.Add(v);
                                        filter.InvokeOnAdd(v);
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
                                    filter.InvokeOnAdd(v);
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
                                        filter.InvokeOnAdd(v);
                                    }
                                    list.InsertRange(e.NewStartingIndex, newArray);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (e.IsSingleItem)
                            {
                                var v = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                filter.InvokeOnRemove(v.Item1, v.Item2);
                            }
                            else
                            {
                                if (!filter.IsNullFilter())
                                {
                                    var len = e.OldStartingIndex + e.OldItems.Length;
                                    for (int i = e.OldStartingIndex; i < len; i++)
                                    {
                                        var v = list[i];
                                        filter.InvokeOnRemove(v.Item1, v.Item2);
                                    }
                                }

                                list.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            // ObservableList does not support replace range
                            {
                                var v = (e.NewItem, selector(e.NewItem));

                                var oldItem = list[e.NewStartingIndex];
                                list[e.NewStartingIndex] = v;

                                filter.InvokeOnRemove(oldItem);
                                filter.InvokeOnAdd(v);
                                break;
                            }
                        case NotifyCollectionChangedAction.Move:
                            {
                                var removeItem = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                list.Insert(e.NewStartingIndex, removeItem);

                                // TODO:???
                                //filter.InvokeOnRemove(removeItem);
                                //filter.InvokeOnAdd(v);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (!filter.IsNullFilter())
                            {
                                foreach (var item in list)
                                {
                                    filter.InvokeOnRemove(item);
                                }
                            }
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

        class SortedView<TKey, TView> : ISynchronizedView<T, TView>
            where TKey : notnull
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> transform;
            readonly Func<T, TKey> identitySelector;
            readonly SortedDictionary<(T Value, TKey Key), (T Value, TView View)> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; } = new object();

            public SortedView(ObservableList<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
            {
                this.source = source;
                this.identitySelector = identitySelector;
                this.transform = transform;
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                lock (source.SyncRoot)
                {
                    var dict = new SortedDictionary<(T, TKey), (T, TView)>(new Comparer(comparer));
                    var count = source.list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var v = source.list[i];
                        dict.Add((v, identitySelector(v)), (v, transform(v)));
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
                    foreach (var (_, (value, view)) in list)
                    {
                        filter.InvokeOnAttach(value, view);
                    }
                }
            }

            public void ResetFilter(Action<T, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<T, TView>.Null;
                    if (resetAction != null)
                    {
                        foreach (var (_, (value, view)) in list)
                        {
                            resetAction(value, view);
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
                return new SynchronizedViewEnumerator<T, TView>(SyncRoot, list.Select(x => x.Value).GetEnumerator(), filter);
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
                            {
                                // Add, Insert
                                if (e.IsSingleItem)
                                {
                                    var value = e.NewItem;
                                    var view = transform(value);
                                    var id = identitySelector(value);
                                    list.Add((value, id), (value, view));
                                    filter.InvokeOnAdd(value, view);
                                }
                                else
                                {
                                    foreach (var value in e.NewItems)
                                    {
                                        var view = transform(value);
                                        var id = identitySelector(value);
                                        list.Add((value, id), (value, view));
                                        filter.InvokeOnAdd(value, view);
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            {
                                if (e.IsSingleItem)
                                {
                                    var value = e.OldItem;
                                    var id = identitySelector(value);
                                    list.Remove((value, id), out var v);
                                    filter.InvokeOnRemove(v.Value, v.View);
                                }
                                else
                                {
                                    foreach (var value in e.OldItems)
                                    {
                                        var id = identitySelector(value);
                                        list.Remove((value, id), out var v);
                                        filter.InvokeOnRemove(v.Value, v.View);
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                        case NotifyCollectionChangedAction.Move:
                            // ObservableList does not support replace range
                            // Replace is remove old item and insert new item(same index on replace, difference index on move).
                            {
                                var oldValue = e.OldItem;
                                list.Remove((oldValue, identitySelector(oldValue)), out var oldView);

                                var value = e.NewItem;
                                var view = transform(value);
                                var id = identitySelector(value);
                                list.Add((value, id), (value, view));

                                filter.InvokeOnRemove(oldView);
                                filter.InvokeOnAdd(value, view);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (!filter.IsNullFilter())
                            {
                                foreach (var item in list)
                                {
                                    filter.InvokeOnRemove(item.Value);
                                }
                            }
                            list.Clear();
                            break;
                        default:
                            break;
                    }

                    RoutingCollectionChanged?.Invoke(e);
                    CollectionStateChanged?.Invoke(e.Action);
                }
            }

            sealed class Comparer : IComparer<(T value, TKey id)>
            {
                readonly IComparer<T> comparer;

                public Comparer(IComparer<T> comparer)
                {
                    this.comparer = comparer;
                }

                public int Compare((T value, TKey id) x, (T value, TKey id) y)
                {
                    var compare = comparer.Compare(x.value, y.value);
                    if (compare == 0)
                    {
                        compare = Comparer<TKey>.Default.Compare(x.id, y.id);
                    }

                    return compare;
                }
            }
        }

        class ViewComparerSortedView<TKey, TView> : ISynchronizedView<T, TView>
            where TKey : notnull
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> transform;
            readonly Func<T, TKey> identitySelector;
            readonly Dictionary<TKey, TView> viewMap; // view-map needs to use in remove.
            readonly SortedDictionary<(TView View, TKey Key), (T Value, TView View)> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; } = new object();

            public ViewComparerSortedView(ObservableList<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> comparer)
            {
                this.source = source;
                this.identitySelector = identitySelector;
                this.transform = transform;
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                lock (source.SyncRoot)
                {
                    var dict = new SortedDictionary<(TView, TKey), (T, TView)>(new Comparer(comparer));
                    this.viewMap = new Dictionary<TKey, TView>(source.list.Count);
                    var count = source.list.Count;
                    for (int i = 0; i < count; i++)
                    {
                        var value = source.list[i];
                        var view = transform(value);
                        var id = identitySelector(value);
                        dict.Add((view, id), (value, view));
                        viewMap.Add(id, view);
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
                    foreach (var (_, (value, view)) in list)
                    {
                        filter.InvokeOnAttach(value, view);
                    }
                }
            }

            public void ResetFilter(Action<T, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<T, TView>.Null;
                    if (resetAction != null)
                    {
                        foreach (var (_, (value, view)) in list)
                        {
                            resetAction(value, view);
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
                return new SynchronizedViewEnumerator<T, TView>(SyncRoot, list.Select(x => x.Value).GetEnumerator(), filter);
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
                            {
                                // Add, Insert
                                if (e.IsSingleItem)
                                {
                                    var value = e.NewItem;
                                    var view = transform(value);
                                    var id = identitySelector(value);
                                    list.Add((view, id), (value, view));
                                    viewMap.Add(id, view);
                                    filter.InvokeOnAdd(value, view);
                                }
                                else
                                {
                                    foreach (var value in e.NewItems)
                                    {
                                        var view = transform(value);
                                        var id = identitySelector(value);
                                        list.Add((view, id), (value, view));
                                        viewMap.Add(id, view);
                                        filter.InvokeOnAdd(value, view);
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            {
                                if (e.IsSingleItem)
                                {
                                    var value = e.OldItem;
                                    var id = identitySelector(value);
                                    if (viewMap.Remove(id, out var view))
                                    {
                                        list.Remove((view, id), out var v);
                                        filter.InvokeOnRemove(v);
                                    }
                                }
                                else
                                {
                                    foreach (var value in e.OldItems)
                                    {
                                        var id = identitySelector(value);
                                        if (viewMap.Remove(id, out var view))
                                        {
                                            list.Remove((view, id), out var v);
                                            filter.InvokeOnRemove(v);
                                        }
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                        case NotifyCollectionChangedAction.Move:
                            // ObservableList does not support replace range
                            // Replace is remove old item and insert new item(same index on replace, difference index on move).
                            {
                                var oldValue = e.OldItem;
                                var oldKey = identitySelector(oldValue);
                                if (viewMap.Remove(oldKey, out var oldView))
                                {
                                    list.Remove((oldView, oldKey));
                                    filter.InvokeOnRemove(oldValue, oldView);
                                }

                                var value = e.NewItem;
                                var view = transform(value);
                                var id = identitySelector(value);
                                list.Add((view, id), (value, view));
                                viewMap.Add(id, view);

                                filter.InvokeOnAdd(value, view);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (!filter.IsNullFilter())
                            {
                                foreach (var item in list)
                                {
                                    filter.InvokeOnRemove(item.Value);
                                }
                            }
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

            sealed class Comparer : IComparer<(TView view, TKey id)>
            {
                readonly IComparer<TView> comparer;

                public Comparer(IComparer<TView> comparer)
                {
                    this.comparer = comparer;
                }

                public int Compare((TView view, TKey id) x, (TView view, TKey id) y)
                {
                    var compare = comparer.Compare(x.view, y.view);
                    if (compare == 0)
                    {
                        compare = Comparer<TKey>.Default.Compare(x.id, y.id);
                    }

                    return compare;
                }
            }
        }
    }
}