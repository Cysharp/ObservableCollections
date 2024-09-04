﻿using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections
{
    public sealed partial class ObservableStack<T> : IReadOnlyCollection<T>, IObservableCollection<T>
    {
        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform)
        {
            return new View<TView>(this, transform);
        }

        class View<TView> : ISynchronizedView<T, TView>
        {
            readonly ObservableStack<T> source;
            readonly Func<T, TView> selector;
            protected readonly Stack<(T, TView)> stack;
            int filteredCount;

            ISynchronizedViewFilter<T> filter;

            public event NotifyViewChangedEventHandler<T, TView>? ViewChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

            public object SyncRoot { get; }

            public ISynchronizedViewFilter<T> Filter
            {
                get { lock (SyncRoot) return filter; }
            }

            public View(ObservableStack<T> source, Func<T, TView> selector)
            {
                this.source = source;
                this.selector = selector;
                this.filter = SynchronizedViewFilter<T>.Null;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.stack = new Stack<(T, TView)>(source.stack.Select(x => (x, selector(x))));
                    this.filteredCount = stack.Count;
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
                        return stack.Count;
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
                    foreach (var (value, view) in stack)
                    {
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
                    this.filteredCount = stack.Count;
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
                    foreach (var item in stack)
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
                        foreach (var item in stack)
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
                        foreach (var item in stack)
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
                            // Add(Push, PushRange)
                            if (e.IsSingleItem)
                            {
                                var v = (e.NewItem, selector(e.NewItem));
                                stack.Push(v);
                                this.InvokeOnAdd(ref filteredCount, ViewChanged, v, 0);
                            }
                            else
                            {
                                foreach (var item in e.NewItems)
                                {
                                    var v = (item, selector(item));
                                    stack.Push(v);
                                    this.InvokeOnAdd(ref filteredCount, ViewChanged, v, 0);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            // Pop, PopRange
                            if (e.IsSingleItem)
                            {
                                var v = stack.Pop();
                                this.InvokeOnRemove(ref filteredCount, ViewChanged, v.Item1, v.Item2, 0);
                            }
                            else
                            {
                                var len = e.OldItems.Length;
                                for (int i = 0; i < len; i++)
                                {
                                    var v = stack.Pop();
                                    this.InvokeOnRemove(ref filteredCount, ViewChanged, v.Item1, v.Item2, 0);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            stack.Clear();
                            this.InvokeOnReset(ref filteredCount, ViewChanged);
                            break;
                        case NotifyCollectionChangedAction.Replace:
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
