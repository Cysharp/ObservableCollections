﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal class NotifyCollectionChangedSynchronizedView<T, TView> :
        INotifyCollectionChangedSynchronizedView<TView>,
        ISynchronizedViewFilter<T, TView>
    {
        static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
        static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

        readonly ISynchronizedView<T, TView> parent;
        readonly ISynchronizedViewFilter<T, TView> currentFilter;
        readonly ICollectionEventDispatcher eventDispatcher;

        public NotifyCollectionChangedSynchronizedView(ISynchronizedView<T, TView> parent, ICollectionEventDispatcher? eventDispatcher)
        {
            this.parent = parent;
            this.eventDispatcher = eventDispatcher ?? DirectCollectionEventDispatcher.Instance;
            currentFilter = parent.CurrentFilter;
            parent.AttachFilter(this);
        }

        public virtual int Count => parent.Count;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged
        {
            add { parent.CollectionStateChanged += value; }
            remove { parent.CollectionStateChanged -= value; }
        }

        public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged
        {
            add { parent.RoutingCollectionChanged += value; }
            remove { parent.RoutingCollectionChanged -= value; }
        }

        public void Dispose()
        {
            parent.Dispose();
        }

        public IEnumerator<TView> GetEnumerator()
        {
            foreach (var (value, view) in parent)
            {
                yield return view;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsMatch(T value, TView view) => currentFilter.IsMatch(value, view);
        public void WhenTrue(T value, TView view) => currentFilter.WhenTrue(value, view);
        public void WhenFalse(T value, TView view) => currentFilter.WhenFalse(value, view);

        public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> args)
        {
            currentFilter.OnCollectionChanged(args);

            if (CollectionChanged == null && PropertyChanged == null) return;

            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewView, args.NewViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                    break;
                case NotifyCollectionChangedAction.Remove:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldView, args.OldViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                    break;
                case NotifyCollectionChangedAction.Reset:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Reset)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                    break;
                case NotifyCollectionChangedAction.Replace:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, args.NewView, args.OldView, args.NewViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = false
                    });
                    break;
                case NotifyCollectionChangedAction.Move:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Move, args.NewView, args.NewViewIndex, args.OldViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = false
                    });
                    break;
            }
        }

        static void RaiseChangedEvent(NotifyCollectionChangedEventArgs e)
        {
            var e2 = (CollectionEventDispatcherEventArgs)e;
            var self = (NotifyCollectionChangedSynchronizedView<T, TView>)e2.Collection;

            if (e2.IsInvokeCollectionChanged)
            {
                self.CollectionChanged?.Invoke(self, e);
            }
            if (e2.IsInvokePropertyChanged)
            {
                self.PropertyChanged?.Invoke(self, CountPropertyChangedEventArgs);
            }
        }
    }

    internal class ListNotifyCollectionChangedSynchronizedView<T, TView>
        : NotifyCollectionChangedSynchronizedView<T, TView>
        , IList<TView>, IReadOnlyList<TView>
        , IList
    {
        readonly ObservableList<T>.View<TView> view;

        public ListNotifyCollectionChangedSynchronizedView(ObservableList<T>.View<TView> parent, ICollectionEventDispatcher? eventDispatcher)
            : base(parent, eventDispatcher)
        {
            this.view = parent;
        }

        public TView this[int index]
        {
            get
            {
                lock (view.SyncRoot)
                {
                    int currentIndex = 0;
                    foreach (var (value, itemView) in view.list)
                    {
                        if (view.CurrentFilter.IsMatch(value, itemView))
                        {
                            if (currentIndex == index)
                            {
                                return itemView;
                }
                            currentIndex++;
                        }
                    }
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
            }
            set => throw new NotSupportedException();
        }

        object? IList.this[int index]
        {
            get
            {
                lock (view.SyncRoot)
                {
                    int currentIndex = 0;
                    foreach (var (value, itemView) in view.list)
                    {
                        if (view.CurrentFilter.IsMatch(value, itemView))
                        {
                            if (currentIndex == index)
                            {
                                return itemView;
                            }
                            currentIndex++;
                        }
                    }
                    throw new ArgumentOutOfRangeException(nameof(index));
                }
            }
            set => throw new NotSupportedException();
        }

        public override int Count
        {
            get
            {
                lock (view.SyncRoot)
                {
                    return view.list.Count(item => view.CurrentFilter.IsMatch(item.Item1, item.Item2));
                }
            }
        }

        static bool IsCompatibleObject(object? value)
        {
            return (value is T) || (value is TView) || (value == null && default(T) == null);
        }

        public bool IsReadOnly => true;

        public bool IsFixedSize => false;

        public bool IsSynchronized => true;

        public object SyncRoot => view.SyncRoot;

        public void Add(TView item)
        {
            throw new NotSupportedException();
        }

        public int Add(object? value)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(TView item)
        {
            lock (view.SyncRoot)
            {
                foreach (var (value, itemView) in view.list)
                {
                    if (view.CurrentFilter.IsMatch(value, itemView))
                    {
                        if (EqualityComparer<TView>.Default.Equals(itemView, item))
                    {
                        return true;
                    }
                }
            }
            }
            return false;
        }

        public bool Contains(object? value)
        {
            if (IsCompatibleObject(value))
            {
                return Contains((TView)value!);
            }
            return false;
        }

        public void CopyTo(TView[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public int IndexOf(TView item)
        {
            lock (view.SyncRoot)
            {
                var index = 0;
                foreach (var (value, itemView) in view.list)
                {
                    if (view.CurrentFilter.IsMatch(value, itemView))
                {
                        if (EqualityComparer<TView>.Default.Equals(itemView, item))
                    {
                        return index;
                    }
                    index++;
                }
            }
            }
            return -1;
        }

        public int IndexOf(object? item)
        {
            if (IsCompatibleObject(item))
            {
                return IndexOf((TView)item!);
            }
            return -1;
        }

        public void Insert(int index, TView item)
        {
            throw new NotSupportedException();
        }

        public void Insert(int index, object? value)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TView item)
        {
            throw new NotSupportedException();
        }

        public void Remove(object? value)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }
    }
}