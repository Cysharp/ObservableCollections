using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections
{
    public sealed partial class ObservableStack<T> : IReadOnlyCollection<T>, IObservableCollection<T>
    {
        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            return new View<TView>(this, transform, reverse);
        }

        class View<TView> : ISynchronizedView<T, TView>
        {
            readonly ObservableStack<T> source;
            readonly Func<T, TView> selector;
            readonly bool reverse;
            protected readonly Stack<(T, TView)> stack;

            ISynchronizedViewFilter<T, TView> filter;

            public event NotifyCollectionChangedEventHandler<T> RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction> CollectionStateChanged;

            public object SyncRoot { get; }

            public View(ObservableStack<T> source, Func<T, TView> selector, bool reverse)
            {
                this.source = source;
                this.selector = selector;
                this.reverse = reverse;
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                this.SyncRoot = new object();
                lock (source.SyncRoot)
                {
                    this.stack = new Stack<(T, TView)>(source.stack.Select(x => (x, selector(x))));
                    this.source.CollectionChanged += SourceCollectionChanged;
                }
            }

            public int Count
            {
                get
                {
                    lock (SyncRoot)
                    {
                        return stack.Count;
                    }
                }
            }

            public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
            {
                lock (SyncRoot)
                {
                    this.filter = filter;
                    foreach (var (value, view) in stack)
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
                        foreach (var (item, view) in stack)
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
                        foreach (var item in stack)
                        {
                            if (filter.IsMatch(item.Item1, item.Item2))
                            {
                                yield return item;
                            }
                        }
                    }
                    else
                    {
                        foreach (var item in stack.AsEnumerable().Reverse())
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
                            // Add(Push, PushRange)
                            if (e.IsSingleItem)
                            {
                                var v = (e.NewItem, selector(e.NewItem));
                                stack.Push(v);
                                filter.InvokeOnAdd(v);
                            }
                            else
                            {
                                foreach (var item in e.NewItems)
                                {
                                    var v = (item, selector(item));
                                    stack.Push(v);
                                    filter.InvokeOnAdd(v);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            // Pop, PopRange
                            if (e.IsSingleItem)
                            {
                                var v = stack.Pop();
                                filter.InvokeOnRemove(v.Item1, v.Item2);
                            }
                            else
                            {
                                var len = e.OldItems.Length;
                                for (int i = 0; i < len; i++)
                                {
                                    var v = stack.Pop();
                                    filter.InvokeOnRemove(v.Item1, v.Item2);
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            if (!filter.IsNullFilter())
                            {
                                foreach (var item in stack)
                                {
                                    filter.InvokeOnRemove(item);
                                }
                            }
                            stack.Clear();
                            break;
                        case NotifyCollectionChangedAction.Replace:
                        case NotifyCollectionChangedAction.Move:
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
