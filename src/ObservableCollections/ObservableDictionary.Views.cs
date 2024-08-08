using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections
{
    public sealed partial class ObservableDictionary<TKey, TValue>
    {
        public ISynchronizedView<KeyValuePair<TKey, TValue>, TView> CreateView<TView>(Func<KeyValuePair<TKey, TValue>, TView> transform, bool _ = false)
        {
            // reverse is no used.
            return new View<TView>(this, transform);
        }

        class View<TView> : ISynchronizedView<KeyValuePair<TKey, TValue>, TView>
        {
            readonly ObservableDictionary<TKey, TValue> source;
            readonly Func<KeyValuePair<TKey, TValue>, TView> selector;
            ISynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView> filter;
            readonly Dictionary<TKey, (TValue, TView)> dict;

            public View(ObservableDictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, TView> selector)
            {
                this.source = source;
                this.selector = selector;
                this.filter = SynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView>.Null;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.dict = source.dictionary.ToDictionary(x => x.Key, x => (x.Value, selector(x)));
                    this.source.CollectionChanged += SourceCollectionChanged;
                }
            }

            public object SyncRoot { get; }
            public event NotifyCollectionChangedEventHandler<KeyValuePair<TKey, TValue>>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public ISynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView> CurrentFilter
            {
                get { lock (SyncRoot) return filter; }
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

            public void Dispose()
            {
                this.source.CollectionChanged -= SourceCollectionChanged;
            }

            public void AttachFilter(ISynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView> filter, bool invokeAddEventForCurrentElements = false)
            {
                lock (SyncRoot)
                {
                    this.filter = filter;
                    foreach (var v in dict)
                    {
                        var value = new KeyValuePair<TKey, TValue>(v.Key, v.Value.Item1);
                        var view = v.Value.Item2;
                        if (invokeAddEventForCurrentElements)
                        {
                            filter.InvokeOnAdd(value, view, -1);
                        }
                        else
                        {
                            filter.InvokeOnAttach(value, view);
                        }
                    }
                }
            }

            public void ResetFilter(Action<KeyValuePair<TKey, TValue>, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView>.Null;
                    if (resetAction != null)
                    {
                        foreach (var v in dict)
                        {
                            resetAction(new KeyValuePair<TKey, TValue>(v.Key, v.Value.Item1), v.Value.Item2);
                        }
                    }
                }
            }

            public INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedView<KeyValuePair<TKey, TValue>, TView>(this, null);
                }
            }

            public INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedView<KeyValuePair<TKey, TValue>, TView>(this, collectionEventDispatcher);
                }
            }

            public IEnumerator<(KeyValuePair<TKey, TValue>, TView)> GetEnumerator()
            {
                lock (SyncRoot)
                {
                    foreach (var item in dict)
                    {
                        var v = (new KeyValuePair<TKey, TValue>(item.Key, item.Value.Item1), item.Value.Item2);
                        if (filter.IsMatch(v.Item1, v.Item2))
                        {
                            yield return v;
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private void SourceCollectionChanged(in NotifyCollectionChangedEventArgs<KeyValuePair<TKey, TValue>> e)
            {
                // ObservableDictionary only provides single item operation and does not use int index.
                lock (SyncRoot)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                        {
                            var v = selector(e.NewItem);
                            dict.Add(e.NewItem.Key, (e.NewItem.Value, v));
                            filter.InvokeOnAdd(e.NewItem, v, -1);
                        }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                        {
                            if (dict.Remove(e.OldItem.Key, out var v))
                            {
                                filter.InvokeOnRemove(e.OldItem, v.Item2, -1);
                            }
                        }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                        {
                            var v = selector(e.NewItem);
                            dict.Remove(e.OldItem.Key, out var ov);
                            dict[e.NewItem.Key] = (e.NewItem.Value, v);
                            
                            filter.InvokeOnReplace(e.NewItem, v, e.OldItem, ov.Item2, -1);
                        }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                        {
                            dict.Clear();
                            filter.InvokeOnReset();
                        }
                            break;
                        case NotifyCollectionChangedAction.Move: // ObservableDictionary have no Move operation.
                        default:
                            break;
                    }

                    RoutingCollectionChanged?.Invoke(e);
                    CollectionStateChanged?.Invoke(e.Action);
                }
            }
        }
    }
}