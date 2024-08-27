using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections
{
    public sealed partial class ObservableList<T> : IList<T>, IReadOnlyObservableList<T>
    {
        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform)
        {
            return new View<TView>(this, transform);
        }

        public ISynchronizedViewList<T> ToViewList()
        {
            return CreateView(static x => x).ToViewList();
        }

        public INotifyCollectionChangedSynchronizedView<T> ToNotifyCollectionChanged()
        {
            return CreateView(static x => x).ToNotifyCollectionChanged();
        }

        public INotifyCollectionChangedSynchronizedView<T> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
        {
            return CreateView(static x => x).ToNotifyCollectionChanged(collectionEventDispatcher);
        }

        internal sealed class View<TView> : ISynchronizedView<T, TView>
        {
            public ISynchronizedViewFilter<T> Filter
            {
                get
                {
                    lock (SyncRoot) { return filter; }
                }
            }

            readonly ObservableList<T> source;
            readonly Func<T, TView> selector;
            internal readonly List<(T, TView)> list; // unsafe, be careful to use
            int filteredCount;

            ISynchronizedViewFilter<T> filter;

            public event NotifyViewChangedEventHandler<T, TView>? ViewChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; }

            public View(ObservableList<T> source, Func<T, TView> selector)
            {
                this.source = source;
                this.selector = selector;
                this.filter = SynchronizedViewFilter<T>.Null;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.list = source.list.Select(x => (x, selector(x))).ToList();
                    this.filteredCount = list.Count;
                    this.source.CollectionChanged += SourceCollectionChanged;
                }
            }

            public int Count
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return filteredCount;
                    }
                }
            }

            public int UnfilteredCount
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return list.Count;
                    }
                }
            }

            public void AttachFilter(ISynchronizedViewFilter<T> filter)
            {
                if (filter.IsNullFilter())
                {
                    ResetFilter();
                    return;
                }

                lock (SyncRoot)
                {
                    this.filter = filter;
                    this.filteredCount = 0;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (filter.IsMatch(list[i].Item1))
                        {
                            filteredCount++;
                        }
                    }

                    ViewChanged?.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Reset, true));
                }
            }

            public void ResetFilter()
            {
                lock (SyncRoot)
                {
                    this.filter = SynchronizedViewFilter<T>.Null;
                    this.filteredCount = list.Count;
                    ViewChanged?.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Reset, true));
                }
            }

            public ISynchronizedViewList<TView> ToViewList()
            {
                return new SynchronizedViewList<T, TView>(this);
            }

            public INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged()
            {
                return new NotifyCollectionChangedSynchronizedView<T, TView>(this, null);
            }

            public INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
            {
                return new NotifyCollectionChangedSynchronizedView<T, TView>(this, collectionEventDispatcher);
            }

            public IEnumerator<TView> GetEnumerator()
            {
                lock (SyncRoot)
                {
                    foreach (var item in list)
                    {
                        if (filter.IsMatch(item.Item1))
                        {
                            yield return item.Item2;
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public IEnumerable<(T Value, TView View)> Filtered
            {
                get
                {
                    lock (SyncRoot)
                    {
                        foreach (var item in list)
                        {
                            if (filter.IsMatch(item.Item1))
                            {
                                yield return item;
                            }
                        }
                    }
                }
            }

            public IEnumerable<(T Value, TView View)> Unfiltered
            {
                get
                {
                    lock (SyncRoot)
                    {
                        foreach (var item in list)
                        {
                            yield return item;
                        }
                    }
                }
            }

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
                                    this.InvokeOnAdd(ref filteredCount, ViewChanged, v, e.NewStartingIndex);
                                }
                                else
                                {
                                    var i = e.NewStartingIndex;

                                    using var array = new ResizableArray<(T, TView)>(e.NewItems.Length);
                                    foreach (var item in e.NewItems)
                                    {
                                        array.Add((item, selector(item)));
                                    }

                                    list.AddRange(array.Span);
                                    this.InvokeOnAdd(ref filteredCount, ViewChanged, v, i++);
                                }
                            }
                            // Insert
                            else
                            {
                                if (e.IsSingleItem)
                                {
                                    var v = (e.NewItem, selector(e.NewItem));
                                    list.Insert(e.NewStartingIndex, v);
                                    this.InvokeOnAdd(ref filteredCount, ViewChanged, v, e.NewStartingIndex);
                                }
                                else
                                {
                                    var span = e.NewItems;
                                    for (var i = 0; i < span.Length; i++)
                                    {
                                        var v = (span[i], selector(span[i]));
                                        list.Insert(e.NewStartingIndex + i, v); // should we use InsertRange?
                                        this.InvokeOnAdd(ref filteredCount, ViewChanged, v, e.NewStartingIndex + i);
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (e.IsSingleItem)
                            {
                                var v = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                this.InvokeOnRemove(ref filteredCount, ViewChanged, v, e.OldStartingIndex);
                            }
                            else
                            {
                                list.RemoveRange(e.OldStartingIndex, e.OldItems.Length); // TODO: no


                                // TODO: Range operation before remove...!

                                var len = e.OldStartingIndex + e.OldItems.Length;
                                for (var i = e.OldStartingIndex; i < len; i++)
                                {
                                    var v = list[i];
                                    this.InvokeOnRemove(ref filteredCount, ViewChanged, v, e.OldStartingIndex + i);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            // ObservableList does not support replace range
                            {
                                var v = (e.NewItem, selector(e.NewItem));
                                var ov = (e.OldItem, list[e.OldStartingIndex].Item2);
                                list[e.NewStartingIndex] = v;
                                this.InvokeOnReplace(ref filteredCount, ViewChanged, v, ov, e.NewStartingIndex);
                                break;
                            }
                        case NotifyCollectionChangedAction.Move:
                            {
                                var removeItem = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                list.Insert(e.NewStartingIndex, removeItem);

                                this.InvokeOnMove(ref filteredCount, ViewChanged, removeItem, e.NewStartingIndex, e.OldStartingIndex);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (e.SortOperation.IsNull)
                            {
                                // None(Clear)
                                list.Clear();
                                this.InvokeOnReset(ref filteredCount, ViewChanged);
                            }
                            else if (e.SortOperation.IsReverse)
                            {
                                // Reverse
                                list.Reverse(e.SortOperation.Index, e.SortOperation.Count);
                                // TODO:Invoke
                            }
                            else
                            {
                                // Sort
                                list.Sort(e.SortOperation.Index, e.SortOperation.Count, new IgnoreViewComparer(e.SortOperation.Comparer ?? Comparer<T>.Default));
                                // Span<T> d;

                            }
                            break;
                        default:
                            break;
                    }

                    CollectionStateChanged?.Invoke(e.Action);
                }
            }

            internal sealed class IgnoreViewComparer : IComparer<(T, TView)>
            {
                readonly IComparer<T> comparer;

                public IgnoreViewComparer(IComparer<T> comparer)
                {
                    this.comparer = comparer;
                }

                public int Compare((T, TView) x, (T, TView) y)
                {
                    return comparer.Compare(x.Item1, y.Item1);
                }
            }
        }
    }
}
