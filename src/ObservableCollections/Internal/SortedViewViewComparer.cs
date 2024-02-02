using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal class SortedViewViewComparer<T, TKey, TView> : ISynchronizedView<T, TView>
        where TKey : notnull
    {
        readonly IObservableCollection<T> source;
        readonly Func<T, TView> transform;
        readonly Func<T, TKey> identitySelector;
        readonly Dictionary<TKey, TView> viewMap; // view-map needs to use in remove.
        readonly SortedList<(TView View, TKey Key), (T Value, TView View)> list;

        ISynchronizedViewFilter<T, TView> filter;

        public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public SortedViewViewComparer(IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> comparer)
        {
            this.source = source;
            this.identitySelector = identitySelector;
            this.transform = transform;
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            lock (source.SyncRoot)
            {
                var dict = new Dictionary<(TView, TKey), (T, TView)>(source.Count);
                this.viewMap = new Dictionary<TKey, TView>();
                foreach (var value in source)
                {
                    var view = transform(value);
                    var id = identitySelector(value);
                    dict.Add((view, id), (value, view));
                    viewMap.Add(id, view);
                }
                this.list = new SortedList<(TView View, TKey Key), (T Value, TView View)>(dict, new Comparer(comparer));
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

            lock (SyncRoot)
            {
                foreach (var item in list)
                {
                    if (filter.IsMatch(item.Value.Value, item.Value.View))
                    {
                        yield return item.Value;
                    }
                }
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
                    {
                        // Add, Insert
                        if (e.IsSingleItem)
                        {
                            var value = e.NewItem;
                            var view = transform(value);
                            var id = identitySelector(value);
                            list.Add((view, id), (value, view));
                            viewMap.Add(id, view);
                            var index = list.IndexOfKey((view, id));
                            filter.InvokeOnAdd(value, view, NotifyCollectionChangedEventArgs<T>.Add(value, index));
                        }
                        else
                        {
                            foreach (var value in e.NewItems)
                            {
                                var view = transform(value);
                                var id = identitySelector(value);
                                list.Add((view, id), (value, view));
                                viewMap.Add(id, view);
                                var index = list.IndexOfKey((view, id));                                            
                                filter.InvokeOnAdd(value, view, NotifyCollectionChangedEventArgs<T>.Add(value, index));
                            }
                        }
                        break;
                    }
                    case NotifyCollectionChangedAction.Remove:
                    {
                        if (e.IsSingleItem)
                        {
                            var value = e.OldItem;
                            var id = identitySelector(value);
                            if (viewMap.Remove(id, out var view))
                            {
                                var key = (view, id);
                                if (list.TryGetValue(key, out var v))
                                {
                                    var index = list.IndexOfKey(key);
                                    list.RemoveAt(index);
                                    filter.InvokeOnRemove(v, NotifyCollectionChangedEventArgs<T>.Remove(v.Value, index));
                                }
                            }
                        }
                        else
                        {
                            foreach (var value in e.OldItems)
                            {
                                var id = identitySelector(value);
                                if (viewMap.Remove(id, out var view))
                                {
                                    var key = (view, id);
                                    if (list.TryGetValue(key, out var v))
                                    {
                                        var index = list.IndexOfKey((view, id));
                                        list.RemoveAt(index);
                                        filter.InvokeOnRemove(v, NotifyCollectionChangedEventArgs<T>.Remove(v.Value, index));
                                    }
                                }
                            }
                        }
                        break;
                    }
                    case NotifyCollectionChangedAction.Replace:
                        // Replace is remove old item and insert new item.
                    {
                        var oldValue = e.OldItem;
                        var oldId = identitySelector(oldValue);
                        if (viewMap.Remove(oldId, out var oldView))
                        {
                            var oldKey = (oldView, oldId);
                            if (list.TryGetValue(oldKey, out var v))
                            {
                                var oldIndex = list.IndexOfKey(oldKey);
                                list.RemoveAt(oldIndex);
                                filter.InvokeOnRemove(oldValue, oldView, NotifyCollectionChangedEventArgs<T>.Remove(v.Value, oldIndex));
                            }
                        }

                        var value = e.NewItem;
                        var view = transform(value);
                        var id = identitySelector(value);
                        list.Add((view, id), (value, view));
                        viewMap.Add(id, view);

                        var index = list.IndexOfKey((view, id));
                        filter.InvokeOnAdd(value, view, NotifyCollectionChangedEventArgs<T>.Add(value, index));
                        break;
                    }
                    case NotifyCollectionChangedAction.Move:
                        // Move(index change) does not affect soreted dict.
                    {
                        var value = e.OldItem;
                        var key = identitySelector(value);
                        if (viewMap.TryGetValue(key, out var view))
                        {
                            filter.InvokeOnMove(value, view, e);
                        }
                        break;
                    }
                    case NotifyCollectionChangedAction.Reset:
                        if (!filter.IsNullFilter())
                        {
                            foreach (var item in list)
                            {
                                filter.InvokeOnRemove(item.Value, e);
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