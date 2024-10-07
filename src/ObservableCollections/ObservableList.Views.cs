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

        public IWritableSynchronizedView<T, TView> CreateWritableView<TView>(Func<T, TView> transform)
        {
            return new View<TView>(this, transform);
        }

        public INotifyCollectionChangedSynchronizedViewList<T> ToWritableNotifyCollectionChanged()
        {
            return ToWritableNotifyCollectionChanged(null);
        }

        public INotifyCollectionChangedSynchronizedViewList<T> ToWritableNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
        {
            return ToWritableNotifyCollectionChanged(
                static x => x,
                static (T newView, T originalValue, ref bool setValue) =>
                {
                    setValue = true;
                    return newView;
                },
                collectionEventDispatcher);
        }

        public INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged<TView>(Func<T, TView> transform, WritableViewChangedEventHandler<T, TView>? converter)
        {
            return ToWritableNotifyCollectionChanged(transform, converter, null!);
        }

        public INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged<TView>(Func<T, TView> transform, WritableViewChangedEventHandler<T, TView>? converter, ICollectionEventDispatcher? collectionEventDispatcher)
        {
            return new NonFilteredNotifyCollectionChangedSynchronizedViewList<T, TView>(CreateView(transform), collectionEventDispatcher, converter);
        }

        internal sealed class View<TView> : ISynchronizedView<T, TView>, IWritableSynchronizedView<T, TView>
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
            public event Action<RejectedViewChangedAction, int, int>? RejectedViewChanged;
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
                return new FiltableSynchronizedViewList<T, TView>(this);
            }

            public INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged()
            {
                return new NotifyCollectionChangedSynchronizedViewList<T, TView>(this, null);
            }

            public INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
            {
                return new NotifyCollectionChangedSynchronizedViewList<T, TView>(this, collectionEventDispatcher);
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
                            // Add or Insert
                            if (e.IsSingleItem)
                            {
                                var v = (e.NewItem, selector(e.NewItem));
                                list.Insert(e.NewStartingIndex, v);
                                this.InvokeOnAdd(ref filteredCount, ViewChanged, RejectedViewChanged, v, e.NewStartingIndex);
                            }
                            else
                            {
                                var items = e.NewItems;
                                var length = items.Length;

                                using var valueViews = new FixedArray<(T, TView)>(length);
                                using var views = new FixedArray<TView>(length);
                                using var matches = new FixedBoolArray(length < FixedBoolArray.StackallocSize ? stackalloc bool[length] : default, length);
                                var isMatchAll = true;
                                for (int i = 0; i < items.Length; i++)
                                {
                                    var item = items[i];
                                    var view = selector(item);
                                    views.Span[i] = view;
                                    valueViews.Span[i] = (item, view);
                                    var isMatch = matches.Span[i] = Filter.IsMatch(item);
                                    if (isMatch)
                                    {
                                        filteredCount++; // increment in this process
                                    }
                                    else
                                    {
                                        isMatchAll = false;
                                    }
                                }

                                list.InsertRange(e.NewStartingIndex, valueViews.Span);
                                this.InvokeOnAddRange(ViewChanged, RejectedViewChanged, e.NewItems, views.Span, isMatchAll, matches.Span, e.NewStartingIndex);
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            if (e.IsSingleItem)
                            {
                                var v = list[e.OldStartingIndex];
                                list.RemoveAt(e.OldStartingIndex);
                                this.InvokeOnRemove(ref filteredCount, ViewChanged, RejectedViewChanged, v, e.OldStartingIndex);
                            }
                            else
                            {
                                var length = e.OldItems.Length;
                                using var values = new FixedArray<T>(length);
                                using var views = new FixedArray<TView>(length);
                                using var matches = new FixedBoolArray(length < FixedBoolArray.StackallocSize ? stackalloc bool[length] : default, length);
                                var isMatchAll = true;
                                var to = e.OldStartingIndex + length;
                                var j = 0;
                                for (int i = e.OldStartingIndex; i < to; i++)
                                {
                                    var item = list[i];
                                    values.Span[j] = item.Item1;
                                    views.Span[j] = item.Item2;
                                    var isMatch = matches.Span[j] = Filter.IsMatch(item.Item1);
                                    if (isMatch)
                                    {
                                        filteredCount--; // decrement in this process
                                    }
                                    else
                                    {
                                        isMatchAll = false;
                                    }
                                    j++;
                                }

                                list.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                                this.InvokeOnRemoveRange(ViewChanged, RejectedViewChanged, values.Span, views.Span, isMatchAll, matches.Span, e.OldStartingIndex);
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

                                this.InvokeOnMove(ref filteredCount, ViewChanged, RejectedViewChanged, removeItem, e.NewStartingIndex, e.OldStartingIndex);
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (e.SortOperation.IsClear)
                            {
                                // None(Clear)
                                list.Clear();
                                this.InvokeOnReset(ref filteredCount, ViewChanged);
                            }
                            else if (e.SortOperation.IsReverse)
                            {
                                // Reverse
                                list.Reverse(e.SortOperation.Index, e.SortOperation.Count);
                                this.InvokeOnReverseOrSort(ViewChanged, e.SortOperation);
                            }
                            else
                            {
                                // Sort
                                list.Sort(e.SortOperation.Index, e.SortOperation.Count, new IgnoreViewComparer(e.SortOperation.Comparer ?? Comparer<T>.Default));
                                this.InvokeOnReverseOrSort(ViewChanged, e.SortOperation);
                            }
                            break;
                        default:
                            break;
                    }

                    CollectionStateChanged?.Invoke(e.Action);
                }
            }

            #region Writable

            public (T Value, TView View) GetAt(int index)
            {
                lock (SyncRoot)
                {
                    return list[index];
                }
            }

            public void SetViewAt(int index, TView view)
            {
                lock (SyncRoot)
                {
                    var v = list[index];
                    list[index] = (v.Item1, view);
                }
            }

            public void SetToSourceCollection(int index, T value)
            {
                lock (SyncRoot)
                {
                    source[index] = value;
                }
            }

            public void AddToSourceCollection(T value)
            {
                lock (SyncRoot)
                {
                    source.Add(value);
                }
            }

            public IWritableSynchronizedViewList<TView> ToWritableViewList(WritableViewChangedEventHandler<T, TView> converter)
            {
                return new FiltableWritableSynchronizedViewList<T, TView>(this, converter);
            }

            public INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter)
            {
                return new NotifyCollectionChangedSynchronizedViewList<T, TView>(this, null, converter);
            }

            public INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
            {
                return new NotifyCollectionChangedSynchronizedViewList<T, TView>(this, collectionEventDispatcher,
                                                                                 static (TView newView, T originalValue, ref bool setValue) =>
                                                                                 {
                                                                                     setValue = true;
                                                                                     return originalValue;
                                                                                 });
            }

            public INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter, ICollectionEventDispatcher? collectionEventDispatcher)
            {
                return new NotifyCollectionChangedSynchronizedViewList<T, TView>(this, collectionEventDispatcher, converter);
            }

            #endregion

            sealed class IgnoreViewComparer : IComparer<(T, TView)>
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
