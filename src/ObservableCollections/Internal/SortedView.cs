using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Internal
{
    internal class SortedView<T, TKey, TView> : ISynchronizedView<T, TView>
        where TKey : notnull
    {
        public ISynchronizedViewFilter<T, TView> CurrentFilter
        {
            get { lock (SyncRoot) return filter; }
        }

        readonly IObservableCollection<T> source;
        readonly Func<T, TView> transform;
        readonly Func<T, TKey> identitySelector;
        readonly SortedList<(T Value, TKey Key), (T Value, TView View)> list;

        ISynchronizedViewFilter<T, TView> filter;

        public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public SortedView(IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
        {
            this.source = source;
            this.identitySelector = identitySelector;
            this.transform = transform;
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            lock (source.SyncRoot)
            {
                var dict = new Dictionary<(T, TKey), (T, TView)>(source.Count);
                foreach (var v in source)
                {
                    dict.Add((v, identitySelector(v)), (v, transform(v)));
                }

                this.list = new SortedList<(T Value, TKey Key), (T Value, TView View)>(dict, new Comparer(comparer));
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

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter, bool invokeAddEventForCurrentElements = false)
        {
            lock (SyncRoot)
            {
                this.filter = filter;
                var i = 0;
                foreach (var (_, (value, view)) in list)
                {
                    if (invokeAddEventForCurrentElements)
                    {
                        filter.InvokeOnAdd(value, view, i++);
                    }
                    else
                    {
                        filter.InvokeOnAttach(value, view);
                    }
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

        public INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged()
        {
            lock (SyncRoot)
            {
                return new NotifyCollectionChangedSynchronizedView<T, TView>(this, null);
            }
        }

        public INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
        {
            lock (SyncRoot)
            {
                return new NotifyCollectionChangedSynchronizedView<T, TView>(this, collectionEventDispatcher);
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
                            list.Add((value, id), (value, view));
                            var index = list.IndexOfKey((value, id));
                            filter.InvokeOnAdd(value, view, index);
                        }
                        else
                        {
                            foreach (var value in e.NewItems)
                            {
                                var view = transform(value);
                                var id = identitySelector(value);
                                list.Add((value, id), (value, view));
                                var index = list.IndexOfKey((value, id));
                                filter.InvokeOnAdd(value, view, index);
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
                            var key = (value, id);
                            if (list.TryGetValue(key, out var v))
                            {
                                var index = list.IndexOfKey(key);
                                list.RemoveAt(index);
                                filter.InvokeOnRemove(v.Value, v.View, index);
                            }
                        }
                        else
                        {
                            foreach (var value in e.OldItems)
                            {
                                var id = identitySelector(value);
                                var key = (value, id);
                                if (list.TryGetValue(key, out var v))
                                {
                                    var index = list.IndexOfKey((value, id));
                                    list.RemoveAt(index);
                                    filter.InvokeOnRemove(v.Value, v.View, index);
                                }
                            }
                        }
                    }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        // ReplaceRange is not supported in all ObservableCollections collections
                        // Replace is remove old item and insert new item.
                    {
                        var oldValue = e.OldItem;
                        var oldKey = (oldValue, identitySelector(oldValue));
                        var oldIndex = -1;
                        if (list.TryGetValue(oldKey, out var o))
                        {
                            oldIndex = list.IndexOfKey(oldKey);
                            list.RemoveAt(oldIndex);
                        }

                        var value = e.NewItem;
                        var view = transform(value);
                        var id = identitySelector(value);
                        list.Add((value, id), (value, view));
                        var newIndex = list.IndexOfKey((value, id));

                        filter.InvokeOnReplace((value, view), o, newIndex, oldIndex: oldIndex);
                    }
                        break;
                    case NotifyCollectionChangedAction.Move:
                    {
                        // Move(index change) does not affect sorted list.
                        var oldValue = e.OldItem;
                        var oldKey = (oldValue, identitySelector(oldValue));
                        if (list.TryGetValue(oldKey, out var v))
                        {
                            var index = list.IndexOfKey(oldKey);
                            filter.InvokeOnMove(v, index, index);
                        }
                    }
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        list.Clear();
                        filter.InvokeOnReset();
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
}