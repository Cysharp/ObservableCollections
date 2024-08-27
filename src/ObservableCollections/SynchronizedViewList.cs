using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace ObservableCollections
{
    internal class SynchronizedViewList<T, TView> : ISynchronizedViewList<TView>
    {
        readonly ISynchronizedView<T, TView> parent;
        protected readonly List<TView> listView;
        protected readonly object gate = new object();

        public SynchronizedViewList(ISynchronizedView<T, TView> parent)
        {
            this.parent = parent;
            lock (parent.SyncRoot)
            {
                listView = parent.ToList();
                parent.ViewChanged += Parent_ViewChanged;
            }
        }

        private void Parent_ViewChanged(in SynchronizedViewChangedEventArgs<T, TView> e)
        {
            lock (gate)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add: // Add or Insert
                        if (e.IsSingleItem)
                        {
                            if (e.NewStartingIndex == -1)
                            {
                                listView.Add(e.NewItem.View);
                            }
                            else
                            {
                                listView.Insert(e.NewStartingIndex, e.NewItem.View);
                            }
                        }
                        else
                        {
                            if (e.NewStartingIndex == -1)
                            {
                                listView.AddRange(e.NewViews);
                            }
                            else
                            {
                                listView.InsertRange(e.NewStartingIndex, e.NewViews);
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove: // Remove
                        if (e.IsSingleItem)
                        {
                            if (e.OldStartingIndex == -1) // can't gurantee correct remove if index is not provided
                            {
                                listView.Remove(e.OldItem.View);
                            }
                            else
                            {
                                listView.RemoveAt(e.OldStartingIndex);
                            }
                        }
                        else
                        {
                            if (e.OldStartingIndex == -1)
                            {
                                // TODO:...
                                //listView.RemoveAll(

                                // e.OldItems
                            }
                            else
                            {
                                listView.RemoveRange(e.OldStartingIndex, e.OldItems.Length);
                            }
                        }


                        break;
                    case NotifyCollectionChangedAction.Replace: // Indexer
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
                    case NotifyCollectionChangedAction.Move: //Remove and Insert
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
                    case NotifyCollectionChangedAction.Reset: // Clear or drastic changes
                        if (e.SortOperation.IsNull)
                        {
                            listView.Clear();
                            foreach (var item in parent) // refresh
                            {
                                listView.Add(item);
                            }
                        }
                        else if (e.SortOperation.IsReverse)
                        {
                            listView.Reverse();
                        }
                        else
                        {
                            if (parent is ObservableList<T>.View<TView> observableListView)
                            {
#pragma warning disable CS0436
                                var comparer = new ObservableList<T>.View<TView>.IgnoreViewComparer(e.SortOperation.Comparer ?? Comparer<T>.Default);
                                var viewSpan = CollectionsMarshal.AsSpan(listView).Slice(e.SortOperation.Index, e.SortOperation.Count);
                                var sourceSpan = CollectionsMarshal.AsSpan(observableListView.list).Slice(e.SortOperation.Index, e.SortOperation.Count);
                                sourceSpan.Sort(viewSpan, comparer);
#pragma warning restore CS0436
                            }
                            else
                            {
                                // can not get source Span, do Clear and Refresh
                                listView.Clear();
                                foreach (var item in parent)
                                {
                                    listView.Add(item);
                                }
                            }
                        }
                        break;
                    default:
                        break;
                }

                OnCollectionChanged(e);
            }
        }

        protected virtual void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> args)
        {
        }

        public TView this[int index]
        {
            get
            {
                lock (gate)
                {
                    return listView[index];
                }
            }
        }

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return listView.Count;
                }
            }
        }

        public IEnumerator<TView> GetEnumerator()
        {
            lock (gate)
            {
                foreach (var item in listView)
                {
                    yield return item;
                }
            }
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
        SynchronizedViewList<T, TView>,
        INotifyCollectionChangedSynchronizedView<TView>,
        IList<TView>, IList
    {
        static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
        static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

        readonly ICollectionEventDispatcher eventDispatcher;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public NotifyCollectionChangedSynchronizedView(ISynchronizedView<T, TView> parent, ICollectionEventDispatcher? eventDispatcher)
            : base(parent)
        {
            this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
        }

        protected override void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> args)
        {
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

        // IList<T>, IList implementation

        TView IList<TView>.this[int index]
        {
            get => ((IReadOnlyList<TView>)this)[index];
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
            return value is T || value == null && default(T) == null;
        }

        public bool IsReadOnly => true;

        public bool IsFixedSize => false;

        public bool IsSynchronized => true;

        public object SyncRoot => gate;

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
            lock (gate)
            {
                foreach (var listItem in listView)
                {
                    if (EqualityComparer<TView>.Default.Equals(listItem, item))
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
            lock (gate)
            {
                var index = 0;
                foreach (var listItem in listView)
                {
                    if (EqualityComparer<TView>.Default.Equals(listItem, item))
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