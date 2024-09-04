using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;

namespace ObservableCollections;

public sealed partial class ObservableList<T> : IList<T>, IReadOnlyObservableList<T>
{
    // override extension methods(IObservableCollection.cs ObservableCollectionExtensions)

    //public ISynchronizedViewList<T> ToViewList<T>(this IObservableCollection<T> collection)
    //{
    //    return ToViewList(collection, static x => x);
    //}

    //public static ISynchronizedViewList<TView> ToViewList<T, TView>(this IObservableCollection<T> collection, Func<T, TView> transform)
    //{
    //    return new NonFilteredSynchronizedViewList<T, TView>(collection.CreateView(transform));
    //}

    /// <summary>
    /// Create faster, compact INotifyCollectionChanged view, however it does not support ***Range.
    /// </summary>
    public INotifyCollectionChangedSynchronizedViewList<T> ToNotifyCollectionChangedSlim()
    {
        return new ObservableListSynchronizedViewList<T>(this, null);
    }

    /// <summary>
    /// Create faster, compact INotifyCollectionChanged view, however it does not support ***Range.
    /// </summary>
    public INotifyCollectionChangedSynchronizedViewList<T> ToNotifyCollectionChangedSlim(ICollectionEventDispatcher? collectionEventDispatcher)
    {
        return new ObservableListSynchronizedViewList<T>(this, collectionEventDispatcher);
    }

    //public static INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged<T, TView>(this IObservableCollection<T> collection, Func<T, TView> transform)
    //{
    //    return ToNotifyCollectionChanged(collection, transform, null!);
    //}

    //public static INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged<T, TView>(this IObservableCollection<T> collection, Func<T, TView> transform, ICollectionEventDispatcher? collectionEventDispatcher)
    //{
    //    return new NonFilteredNotifyCollectionChangedSynchronizedViewList<T, TView>(collection.CreateView(transform), collectionEventDispatcher);
    //}
}

internal sealed class ObservableListSynchronizedViewList<T> : INotifyCollectionChangedSynchronizedViewList<T>, IList<T>, IList
{
    static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
    static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

    readonly ObservableList<T> parent;
    readonly ICollectionEventDispatcher eventDispatcher;

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableListSynchronizedViewList(ObservableList<T> parent, ICollectionEventDispatcher? eventDispatcher)
    {
        this.parent = parent;
        this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
        parent.CollectionChanged += Parent_CollectionChanged;
    }

    private void Parent_CollectionChanged(in NotifyCollectionChangedEventArgs<T> args)
    {
        if (CollectionChanged == null && PropertyChanged == null) return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.IsSingleItem)
                {
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewItem, args.NewStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewItems.ToArray(), args.NewStartingIndex)
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
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldItem, args.OldStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldItems.ToArray(), args.OldStartingIndex)
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
                eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, args.NewItem, args.OldItem, args.NewStartingIndex)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = false
                });
                break;
            case NotifyCollectionChangedAction.Move:
                eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Move, args.NewItem, args.NewStartingIndex, args.OldStartingIndex)
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
        var self = (ObservableListSynchronizedViewList<T>)e2.Collection;

        if (e2.IsInvokeCollectionChanged)
        {
            self.CollectionChanged?.Invoke(self, e);
        }
        if (e2.IsInvokePropertyChanged)
        {
            self.PropertyChanged?.Invoke(self, CountPropertyChangedEventArgs);
        }
    }

    public T this[int index] => parent[index];

    public int Count => parent.Count;

    public IEnumerator<T> GetEnumerator()
    {
        return parent.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return parent.GetEnumerator();
    }

    public void Dispose()
    {
        parent.CollectionChanged -= Parent_CollectionChanged;
    }

    // IList<T>, IList implementation

    T IList<T>.this[int index]
    {
        get => ((IReadOnlyList<T>)this)[index];
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

    public object SyncRoot => parent.SyncRoot;

    public void Add(T item)
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

    public bool Contains(T item)
    {
        return parent.Contains(item);
    }

    public bool Contains(object? value)
    {
        if (IsCompatibleObject(value))
        {
            return Contains((T)value!);
        }
        return false;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        throw new NotSupportedException();
    }

    public void CopyTo(Array array, int index)
    {
        throw new NotImplementedException();
    }

    public int IndexOf(T item)
    {
        return parent.IndexOf(item);
    }

    public int IndexOf(object? item)
    {
        if (IsCompatibleObject(item))
        {
            return IndexOf((T)item!);
        }
        return -1;
    }

    public void Insert(int index, T item)
    {
        throw new NotSupportedException();
    }

    public void Insert(int index, object? value)
    {
        throw new NotImplementedException();
    }

    public bool Remove(T item)
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