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

        sealed class View<TView> : ISynchronizedView<T, TView>
        {
            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;
            readonly bool reverse;
            readonly List<(T, TView)> list;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T> RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction> CollectionStateChanged;

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

            public void ResetFilter(Action<T, TView> resetAction)
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
                lock (SyncRoot)
                {
                    if (!reverse)
                    {
                        foreach (var item in list)
                        {
                            if (filter.IsMatch(item.Item1, item.Item2))
                            {
                                yield return item;
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in list.AsEnumerable().Reverse())
                        {
                            if (filter.IsMatch(item.Item1, item.Item2))
                            {
                                yield return item;
                            }
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
                                var len = e.OldStartingIndex + e.OldItems.Length;
                                for (int i = e.OldStartingIndex; i < len; i++)
                                {
                                    var v = list[i];
                                    filter.InvokeOnRemove(v.Item1, v.Item2);
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

                                filter.InvokeOnMove(removeItem);
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
    }
}