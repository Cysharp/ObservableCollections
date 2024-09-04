﻿using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

namespace ObservableCollections
{
    public sealed partial class ObservableRingBuffer<T>
    {
        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform)
        {
            return new View<TView>(this, transform);
        }

        // used with ObservableFixedSizeRingBuffer
        internal sealed class View<TView> : ISynchronizedView<T, TView>
        {
            public ISynchronizedViewFilter<T> Filter
            {
                get { lock (SyncRoot) return filter; }
            }

            readonly IObservableCollection<T> source;
            readonly Func<T, TView> selector;
            readonly RingBuffer<(T, TView)> ringBuffer;
            int filteredCount;

            ISynchronizedViewFilter<T> filter;

            public event NotifyViewChangedEventHandler<T, TView>? ViewChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; }

            public View(IObservableCollection<T> source, Func<T, TView> selector)
            {
                this.source = source;
                this.selector = selector;
                this.filter = SynchronizedViewFilter<T>.Null;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.ringBuffer = new RingBuffer<(T, TView)>(source.Select(x => (x, selector(x))));
                    this.filteredCount = ringBuffer.Count;
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
                        return ringBuffer.Count;
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
                    for (var i = 0; i < ringBuffer.Count; i++)
                    {
                        var (value, view) = ringBuffer[i];
                        if (filter.IsMatch(value))
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
                    this.filteredCount = ringBuffer.Count;
                    ViewChanged?.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Reset, true));
                }
            }

            public void Refresh()
            {
                if (filter.IsNullFilter())
                {
                    return;
                }
                AttachFilter(filter);
            }

            public ISynchronizedViewList<TView> ToViewList()
            {
                return new FiltableSynchronizedViewList<T, TView>(this);
            }

            public INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged()
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedViewList<T, TView>(this, null);
                }
            }

            public INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher)
            {
                lock (SyncRoot)
                {
                    return new NotifyCollectionChangedSynchronizedViewList<T, TView>(this, collectionEventDispatcher);
                }
            }

            public IEnumerator<TView> GetEnumerator()
            {
                lock (SyncRoot)
                {
                    foreach (var item in ringBuffer)
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
                        foreach (var item in ringBuffer)
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
                        foreach (var item in ringBuffer)
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
                            // can not distinguish AddFirst and AddLast when collection count is 0.
                            // So, in that case, use AddLast.
                            // The internal structure may be different from the parent, but the result is same.
                            // RangeOperation is only exists AddLastRange because we can not distinguish FirstRange or LastRange.
                            if (e.NewStartingIndex == 0 && ringBuffer.Count != 0)
                            {
                                // AddFirst
                                if (e.IsSingleItem)
                                {
                                    var v = (e.NewItem, selector(e.NewItem));
                                    ringBuffer.AddFirst(v);
                                    this.InvokeOnAdd(ref filteredCount, ViewChanged, v, 0);
                                }
                                else
                                {
                                    foreach (var item in e.NewItems)
                                    {
                                        var v = (item, selector(item));
                                        ringBuffer.AddFirst(v);
                                        this.InvokeOnAdd(ref filteredCount, ViewChanged, v, 0);
                                    }
                                }
                            }
                            else
                            {
                                // AddLast
                                if (e.IsSingleItem)
                                {
                                    var v = (e.NewItem, selector(e.NewItem));
                                    ringBuffer.AddLast(v);
                                    this.InvokeOnAdd(ref filteredCount, ViewChanged, v, ringBuffer.Count - 1);
                                }
                                else
                                {
                                    foreach (var item in e.NewItems)
                                    {
                                        var v = (item, selector(item));
                                        ringBuffer.AddLast(v);
                                        this.InvokeOnAdd(ref filteredCount, ViewChanged, v, ringBuffer.Count - 1);
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            // starting from 0 is RemoveFirst
                            if (e.OldStartingIndex == 0)
                            {
                                // RemoveFirst
                                if (e.IsSingleItem)
                                {
                                    var v = ringBuffer.RemoveFirst();
                                    this.InvokeOnRemove(ref filteredCount, ViewChanged, v, 0);
                                }
                                else
                                {
                                    for (int i = 0; i < e.OldItems.Length; i++)
                                    {
                                        var v = ringBuffer.RemoveFirst();
                                        this.InvokeOnRemove(ref filteredCount, ViewChanged, v, 0);
                                    }
                                }
                            }
                            else
                            {
                                // RemoveLast
                                if (e.IsSingleItem)
                                {
                                    var index = ringBuffer.Count - 1;
                                    var v = ringBuffer.RemoveLast();
                                    this.InvokeOnRemove(ref filteredCount, ViewChanged, v, index);
                                }
                                else
                                {
                                    for (int i = 0; i < e.OldItems.Length; i++)
                                    {
                                        var index = ringBuffer.Count - 1;
                                        var v = ringBuffer.RemoveLast();
                                        this.InvokeOnRemove(ref filteredCount, ViewChanged, v, index);
                                    }
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            ringBuffer.Clear();
                            this.InvokeOnReset(ref filteredCount, ViewChanged);
                            break;
                        case NotifyCollectionChangedAction.Replace:
                            // range is not supported
                            {
                                var ov = ringBuffer[e.OldStartingIndex];
                                var v = (e.NewItem, selector(e.NewItem));
                                ringBuffer[e.NewStartingIndex] = v;
                                this.InvokeOnReplace(ref filteredCount, ViewChanged, v, ov, e.NewStartingIndex);
                                break;
                            }
                        case NotifyCollectionChangedAction.Move:
                        default:
                            break;
                    }

                    CollectionStateChanged?.Invoke(e.Action);
                }
            }
        }
    }
}