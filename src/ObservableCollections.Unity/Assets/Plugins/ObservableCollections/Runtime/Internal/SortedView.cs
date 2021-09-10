using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal class SortedView<T, TKey, TView> : ISynchronizedView<T, TView>
        
    {
        readonly IObservableCollection<T> source;
        readonly Func<T, TView> transform;
        readonly Func<T, TKey> identitySelector;
        readonly SortedDictionary<(T Value, TKey Key), (T Value, TView View)> dict;

        ISynchronizedViewFilter<T, TView> filter;

        public event NotifyCollectionChangedEventHandler<T> RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction> CollectionStateChanged;

        public object SyncRoot { get; } = new object();

        public SortedView(IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
        {
            this.source = source;
            this.identitySelector = identitySelector;
            this.transform = transform;
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            lock (source.SyncRoot)
            {
                var dict = new SortedDictionary<(T, TKey), (T, TView)>(new Comparer(comparer));
                foreach (var v in source)
                {
                    dict.Add((v, identitySelector(v)), (v, transform(v)));
                }

                this.dict = dict;
                this.source.CollectionChanged += SourceCollectionChanged;
            }
        }

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return dict.Count;
                }
            }
        }

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
        {
            lock (SyncRoot)
            {
                this.filter = filter;
                foreach (var (_, (value, view)) in dict)
                {
                    filter.InvokeOnAttach(value, view);
                }
            }
        }

        public void ResetFilter(Action<T, TView> resetAction)
        {
            lock (SyncRoot)
            {
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                if (resetAction != null)
                {
                    foreach (var (_, (value, view)) in dict)
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
                foreach (var item in dict)
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
                                dict.Add((value, id), (value, view));
                                filter.InvokeOnAdd(value, view);
                            }
                            else
                            {
                                foreach (var value in e.NewItems)
                                {
                                    var view = transform(value);
                                    var id = identitySelector(value);
                                    dict.Add((value, id), (value, view));
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
                                dict.Remove((value, id), out var v);
                                filter.InvokeOnRemove(v.Value, v.View);
                            }
                            else
                            {
                                foreach (var value in e.OldItems)
                                {
                                    var id = identitySelector(value);
                                    dict.Remove((value, id), out var v);
                                    filter.InvokeOnRemove(v.Value, v.View);
                                }
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        // ReplaceRange is not supported in all ObservableCollections collections
                        // Replace is remove old item and insert new item.
                        {
                            var oldValue = e.OldItem;
                            dict.Remove((oldValue, identitySelector(oldValue)), out var oldView);

                            var value = e.NewItem;
                            var view = transform(value);
                            var id = identitySelector(value);
                            dict.Add((value, id), (value, view));

                            filter.InvokeOnRemove(oldView);
                            filter.InvokeOnAdd(value, view);
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        {
                            // Move(index change) does not affect sorted list.
                            var oldValue = e.OldItem;
                            if (dict.TryGetValue((oldValue, identitySelector(oldValue)), out var view))
                            {
                                filter.InvokeOnMove(view);
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        if (!filter.IsNullFilter())
                        {
                            foreach (var item in dict)
                            {
                                filter.InvokeOnRemove(item.Value);
                            }
                        }
                        dict.Clear();
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
