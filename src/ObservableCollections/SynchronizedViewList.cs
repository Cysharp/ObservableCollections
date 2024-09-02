using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace ObservableCollections;

internal class SynchronizedViewList<T, TView> : ISynchronizedViewList<TView>
{
    readonly ISynchronizedView<T, TView> parent;
    protected readonly AlternateIndexList<TView> listView;
    protected readonly object gate = new object();

    public SynchronizedViewList(ISynchronizedView<T, TView> parent)
    {
        this.parent = parent;
        lock (parent.SyncRoot)
        {
            listView = new AlternateIndexList<TView>(IterateFilteredIndexedViewsOfParent());
            parent.ViewChanged += Parent_ViewChanged;
        }
    }

    IEnumerable<(int, TView)> IterateFilteredIndexedViewsOfParent()
    {
        var filter = parent.Filter;
        var index = 0;
        if (filter.IsNullFilter())
        {
            foreach (var item in parent.Unfiltered) // use Unfiltered
            {
                yield return (index, item.View);
                index++;
            }
        }
        else
        {
            foreach (var item in parent.Unfiltered) // use Unfiltered
            {
                if (filter.IsMatch(item.Value))
                {
                    yield return (index, item.View);
                }
                index++;
            }
        }
    }

    private void Parent_ViewChanged(in SynchronizedViewChangedEventArgs<T, TView> e)
    {
        // event is called inside parent lock
        lock (gate)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add: // Add or Insert
                    if (e.IsSingleItem)
                    {
                        listView.Insert(e.NewStartingIndex, e.NewItem.View);
                    }
                    else
                    {
                        using var array = new CloneCollection<TView>(e.NewViews);
                        listView.InsertRange(e.NewStartingIndex, array.AsEnumerable());
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
                            foreach (var view in e.OldViews) // index is unknown, can't do batching
                            {
                                listView.Remove(view);
                            }
                        }
                        else
                        {
                            listView.RemoveRange(e.OldStartingIndex, e.OldViews.Length);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Replace: // Indexer
                    if (e.NewStartingIndex == -1)
                    {
                        listView.TryReplaceByValue(e.OldItem.View, e.NewItem.View);
                    }
                    else
                    {
                        listView.TrySetAtAlternateIndex(e.NewStartingIndex, e.NewItem.View);
                    }

                    break;
                case NotifyCollectionChangedAction.Move: //Remove and Insert
                    if (e.NewStartingIndex == -1)
                    {
                        // do nothing
                    }
                    else
                    {
                        listView.RemoveAt(e.OldStartingIndex);
                        listView.Insert(e.NewStartingIndex, e.NewItem.View);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset: // Clear or drastic changes
                    listView.Clear(IterateFilteredIndexedViewsOfParent()); // clear and fill refresh
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
                if (args.IsSingleItem)
                {
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewItem.View, args.NewStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewViews.ToArray(), args.NewStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (args.IsSingleItem)
                {
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldItem.View, args.OldStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldViews.ToArray(), args.OldStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
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
                eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, args.NewItem.View, args.OldItem.View, args.NewStartingIndex)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = false
                });
                break;
            case NotifyCollectionChangedAction.Move:
                eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Move, args.NewItem.View, args.NewStartingIndex, args.OldStartingIndex)
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