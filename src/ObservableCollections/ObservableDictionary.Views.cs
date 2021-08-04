using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObservableCollections
{
    public sealed partial class ObservableDictionary<TKey, TValue>
    {
        public ISynchronizedView<KeyValuePair<TKey, TValue>, TView> CreateView<TView>(Func<KeyValuePair<TKey, TValue>, TView> transform, bool reverse = false)
        {
            // reverse is no used.
            throw new NotImplementedException();
        }

        public ISynchronizedView<KeyValuePair<TKey, TValue>, TView> CreateSortedView<TView>(Func<KeyValuePair<TKey, TValue>, TView> transform, IComparer<KeyValuePair<TKey, TValue>> comparer)
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<KeyValuePair<TKey, TValue>, TView> CreateSortedView<TView>(Func<KeyValuePair<TKey, TValue>, TView> transform, IComparer<TView> viewComparer)
        {
            throw new NotImplementedException();
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
                this.filter = SynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView>.AlwaysTrue;
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

            public void AttachFilter(ISynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView> filter)
            {
                lock (SyncRoot)
                {
                    this.filter = filter;
                    foreach (var v in dict)
                    {
                        filter.Invoke(new KeyValuePair<TKey, TValue>(v.Key, v.Value.Item1), v.Value.Item2);
                    }
                }
            }

            public void ResetFilter(Action<KeyValuePair<TKey, TValue>, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView>.AlwaysTrue;
                    if (resetAction != null)
                    {
                        foreach (var v in dict)
                        {
                            resetAction(new KeyValuePair<TKey, TValue>(v.Key, v.Value.Item1), v.Value.Item2);
                        }
                    }
                }
            }

            public INotifyCollectionChangedSynchronizedView<KeyValuePair<TKey, TValue>, TView> WithINotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedView<KeyValuePair<TKey, TValue>, TView>(this);
                }
            }

            public IEnumerator<(KeyValuePair<TKey, TValue>, TView)> GetEnumerator()
            {
                return new SynchronizedViewEnumerator<KeyValuePair<TKey, TValue>, TView>(SyncRoot,
                    dict.Select(x => (new KeyValuePair<TKey, TValue>(x.Key, x.Value.Item1), x.Value.Item2)).GetEnumerator(),
                    filter);
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
                                filter.Invoke(new KeyValuePair<TKey, TValue>(e.NewItem.Key, e.NewItem.Value), v);
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            {
                                dict.Remove(e.OldItem.Key);
                            }
                            break;
                        case NotifyCollectionChangedAction.Move:
                        case NotifyCollectionChangedAction.Replace:
                            {
                                dict.Remove(e.OldItem.Key);
                                var v = selector(e.NewItem);
                                dict.Add(e.NewItem.Key, (e.NewItem.Value, v));
                                filter.Invoke(new KeyValuePair<TKey, TValue>(e.NewItem.Key, e.NewItem.Value), v);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            {
                                dict.Clear();
                            }
                            break;
                        default:
                            break;
                    }

                    RoutingCollectionChanged?.Invoke(e);
                    CollectionStateChanged?.Invoke(e.Action);
                }
            }
        }

        class SortedView<TView> : ISynchronizedView<KeyValuePair<TKey, TValue>, TView>
        {
            readonly ObservableDictionary<TKey, TValue> source;
            readonly Func<KeyValuePair<TKey, TValue>, TView> selector;
            ISynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView> filter;
            readonly SortedDictionary<KeyValuePair<TKey, TValue>, TView> dict;

            public SortedView(ObservableDictionary<TKey, TValue> source, Func<KeyValuePair<TKey, TValue>, TView> selector, IComparer<KeyValuePair<TKey, TValue>> comparer)
            {
                this.source = source;
                this.selector = selector;
                this.filter = SynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView>.AlwaysTrue;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.dict = new SortedDictionary<KeyValuePair<TKey, TValue>, TView>(comparer);
                    foreach (var item in source.dictionary)
                    {
                        dict.Add(item, selector(item));
                    }
                    this.source.CollectionChanged += SourceCollectionChanged;
                }
            }

            public object SyncRoot { get; }
            public event NotifyCollectionChangedEventHandler<KeyValuePair<TKey, TValue>>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

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

            public void AttachFilter(ISynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView> filter)
            {
                lock (SyncRoot)
                {
                    this.filter = filter;
                    foreach (var v in dict)
                    {
                        filter.Invoke(new KeyValuePair<TKey, TValue>(v.Key.Key, v.Key.Value), v.Value);
                    }
                }
            }

            public void ResetFilter(Action<KeyValuePair<TKey, TValue>, TView>? resetAction)
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<KeyValuePair<TKey, TValue>, TView>.AlwaysTrue;
                    if (resetAction != null)
                    {
                        foreach (var v in dict)
                        {
                            resetAction(new KeyValuePair<TKey, TValue>(v.Key.Key, v.Key.Value), v.Value);
                        }
                    }
                }
            }

            public INotifyCollectionChangedSynchronizedView<KeyValuePair<TKey, TValue>, TView> WithINotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedView<KeyValuePair<TKey, TValue>, TView>(this);
                }
            }

            public IEnumerator<(KeyValuePair<TKey, TValue>, TView)> GetEnumerator()
            {
                return new SynchronizedViewEnumerator<KeyValuePair<TKey, TValue>, TView>(SyncRoot,
                    dict.Select(x => (new KeyValuePair<TKey, TValue>(x.Key.Key, x.Key.Value), x.Value)).GetEnumerator(),
                    filter);
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
                                var k = new KeyValuePair<TKey, TValue>(e.NewItem.Key, e.NewItem.Value);
                                dict.Add(k, v);
                                filter.Invoke(k, v);
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            {
                                dict.Remove(e.OldItem);
                            }
                            break;
                        case NotifyCollectionChangedAction.Move:
                        case NotifyCollectionChangedAction.Replace:
                            {
                                var k = new KeyValuePair<TKey, TValue>(e.OldItem.Key, e.OldItem.Value);
                                dict.Remove(k);
                                var v = selector(e.NewItem);
                                var nk = new KeyValuePair<TKey, TValue>(e.NewItem.Key, e.NewItem.Value);
                                dict.Add(nk, v);
                                filter.Invoke(nk, v);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            {
                                dict.Clear();
                            }
                            break;
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
