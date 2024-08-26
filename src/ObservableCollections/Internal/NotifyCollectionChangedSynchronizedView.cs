using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ObservableCollections.Internal
{
    internal class SynchronizedViewList<T, TView> : ISynchronizedViewList<TView>
    {
        readonly ISynchronizedView<T, TView> parent;
        readonly List<TView> listView;

        public SynchronizedViewList(ISynchronizedView<T, TView> parent)
        {
            this.parent = parent;
            lock (parent.SyncRoot)
            {
                this.listView = parent.ToList();
                parent.ViewChanged += Parent_ViewChanged;
            }
        }

        private void Parent_ViewChanged(SynchronizedViewChangedEventArgs<T, TView> e)
        {
            // event is called inside lock(parent.SyncRoot)
            // TODO: invoke in ICollectionEventDispatcher?
            switch (e.Action)
            {
                case NotifyViewChangedAction.Add: // Add or Insert
                    if (e.NewViewIndex == -1)
                    {
                        listView.Add(e.NewView);
                    }
                    else
                    {
                        listView.Insert(e.NewViewIndex, e.NewView);
                    }
                    break;
                case NotifyViewChangedAction.Remove: // Remove
                    if (e.OldViewIndex == -1) // can't gurantee correct remove if index is not provided
                    {
                        listView.Remove(e.OldView);
                    }
                    else
                    {
                        listView.RemoveAt(e.OldViewIndex);
                    }
                    break;
                case NotifyViewChangedAction.Replace: // Indexer
                    if (e.NewViewIndex == -1)
                    {
                        var index = listView.IndexOf(e.OldView);
                        listView[index] = e.NewView;
                    }
                    else
                    {
                        listView[e.NewViewIndex] = e.NewView;
                    }

                    break;
                case NotifyViewChangedAction.Move: //Remove and Insert
                    if (e.NewViewIndex == -1)
                    {
                        // do nothing
                    }
                    else
                    {
                        listView.RemoveAt(e.OldViewIndex);
                        listView.Insert(e.NewViewIndex, e.NewView);
                    }
                    break;
                case NotifyViewChangedAction.Reset: // Clear
                    listView.Clear();
                    break;
                case NotifyViewChangedAction.FilterReset:
                    listView.Clear();
                    foreach (var item in parent)
                    {
                        listView.Add(item.View);
                    }
                    break;
                default:
                    break;
            }
        }


        public TView this[int index] => listView[index];

        public int Count => listView.Count;

        public IEnumerator<TView> GetEnumerator()
        {
            return listView.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return listView.GetEnumerator();
        }

        public void Dispose()
        {
            parent.ViewChanged -= Parent_ViewChanged;
        }
    }




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
            currentFilter = parent.Filter;
            parent.AttachFilter(this);
        }

        public int Count => parent.Count;

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
                    return view.list[index].Item2;
                }
            }
            set => throw new NotSupportedException();
        }

        object? IList.this[int index]
        {
            get
            {
                return this[index];
            }
            set => throw new NotSupportedException();
        }

        static bool IsCompatibleObject(object? value)
        {
            return (value is T) || (value == null && default(T) == null);
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
                foreach (var listItem in view.list)
                {
                    if (EqualityComparer<TView>.Default.Equals(listItem.Item2, item))
                    {
                        return true;
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
                foreach (var listItem in view.list)
                {
                    if (EqualityComparer<TView>.Default.Equals(listItem.Item2, item))
                    {
                        return index;
                    }
                    index++;
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