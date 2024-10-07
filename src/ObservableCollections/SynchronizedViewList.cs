using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ObservableCollections;

internal class FiltableSynchronizedViewList<T, TView> : ISynchronizedViewList<TView>
{
    protected readonly ISynchronizedView<T, TView> parent;
    protected readonly AlternateIndexList<TView> listView;
    protected readonly object gate = new object();

    protected virtual bool IsSupportRangeFeature => true;

    public FiltableSynchronizedViewList(ISynchronizedView<T, TView> parent)
    {
        this.parent = parent;
        lock (parent.SyncRoot)
        {
            listView = new AlternateIndexList<TView>(IterateFilteredIndexedViewsOfParent());
            parent.ViewChanged += Parent_ViewChanged;
            parent.RejectedViewChanged += Parent_RejectedViewChanged;
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
                        if (e.NewStartingIndex == -1)
                        {
                            // add operation
                            var index = listView.Count;
                            listView.Insert(index, e.NewItem.View);
                            OnCollectionChanged(e.WithNewStartingIndex(index));
                            return;
                        }
                        else
                        {
                            var index = listView.Insert(e.NewStartingIndex, e.NewItem.View);
                            OnCollectionChanged(e.WithNewStartingIndex(index));
                            return;
                        }
                    }
                    else
                    {
                        if (IsSupportRangeFeature)
                        {
                            using var array = new CloneCollection<TView>(e.NewViews);
                            var index = listView.InsertRange(e.NewStartingIndex, array.AsEnumerable());
                            OnCollectionChanged(e.WithNewStartingIndex(index));
                        }
                        else
                        {
                            var span = e.NewViews;
                            for (int i = 0; i < span.Length; i++)
                            {
                                var index = listView.Insert(e.NewStartingIndex + i, span[i]);
                                var ev = new SynchronizedViewChangedEventArgs<T, TView>(e.Action, true, newItem: (e.NewValues[i], span[i]), newStartingIndex: index);
                                OnCollectionChanged(ev);
                            }
                        }
                        return;
                    }
                case NotifyCollectionChangedAction.Remove: // Remove
                    {
                        int index = e.OldStartingIndex;
                        if (e.IsSingleItem)
                        {
                            if (e.OldStartingIndex == -1) // can't gurantee correct remove if index is not provided
                            {
                                index = listView.Remove(e.OldItem.View);
                            }
                            else
                            {
                                index = listView.RemoveAt(e.OldStartingIndex);
                            }
                        }
                        else
                        {
                            if (e.OldStartingIndex == -1)
                            {
                                foreach (var view in e.OldViews) // index is unknown, can't do batching
                                {
                                    listView.Remove(view);
                                    OnCollectionChanged(e.WithOldStartingIndex(index));
                                }
                                return;
                            }
                            else
                            {
                                if (IsSupportRangeFeature)
                                {
                                    index = listView.RemoveRange(e.OldStartingIndex, e.OldViews.Length);
                                }
                                else
                                {
                                    var span = e.OldViews;
                                    for (int i = 0; i < span.Length; i++)
                                    {
                                        index = listView.RemoveAt(e.OldStartingIndex); // when removed, next remove index is same.
                                        var ev = new SynchronizedViewChangedEventArgs<T, TView>(e.Action, true, oldItem: (e.OldValues[i], span[i]), oldStartingIndex: index);
                                        OnCollectionChanged(ev);
                                    }
                                    return;
                                }
                            }
                        }
                        OnCollectionChanged(e.WithOldStartingIndex(index));
                        return;
                    }
                case NotifyCollectionChangedAction.Replace: // Indexer
                    if (e.NewStartingIndex == -1)
                    {
                        if (listView.TryReplaceByValue(e.OldItem.View, e.NewItem.View, out var replacedIndex))
                        {
                            OnCollectionChanged(e.WithNewAndOldStartingIndex(replacedIndex, replacedIndex));
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        if (listView.TrySetAtAlternateIndex(e.NewStartingIndex, e.NewItem.View, out var setIndex))
                        {
                            OnCollectionChanged(e.WithNewAndOldStartingIndex(setIndex, setIndex));
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }
                case NotifyCollectionChangedAction.Move: //Remove and Insert
                    if (e.NewStartingIndex == -1)
                    {
                        return; // do nothing
                    }
                    else
                    {
                        var oldIndex = listView.RemoveAt(e.OldStartingIndex);
                        var newIndex = listView.Insert(e.NewStartingIndex, e.NewItem.View);
                        OnCollectionChanged(e.WithNewAndOldStartingIndex(newStartingIndex: newIndex, oldStartingIndex: oldIndex));
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

    private void Parent_RejectedViewChanged(RejectedViewChangedAction arg1, int index, int oldIndex)
    {
        if (index == -1) return;

        lock (gate)
        {
            switch (arg1)
            {
                case RejectedViewChangedAction.Add:
                    listView.UpdateAlternateIndex(index, 1);
                    break;
                case RejectedViewChangedAction.Remove:
                    listView.UpdateAlternateIndex(index, -1);
                    break;
                case RejectedViewChangedAction.Move:
                    if (oldIndex == -1) return;
                    if (listView.TryReplaceAlternateIndex(oldIndex, index))
                    {
                        // replace alternate-index changes order so needs Reset
                        OnCollectionChanged(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Reset, true));
                    }
                    break;
                default:
                    break;
            }
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
        return GetEnumerator();
    }

    public void Dispose()
    {
        parent.ViewChanged -= Parent_ViewChanged;
        parent.RejectedViewChanged -= Parent_RejectedViewChanged;
    }
}

internal class NonFilteredSynchronizedViewList<T, TView> : ISynchronizedViewList<TView>
{
    protected readonly ISynchronizedView<T, TView> parent;
    protected readonly List<TView> listView; // no filter can be faster
    protected readonly object gate = new object();

    protected virtual bool IsSupportRangeFeature => true;

    public NonFilteredSynchronizedViewList(ISynchronizedView<T, TView> parent)
    {
        this.parent = parent;
        lock (parent.SyncRoot)
        {
            listView = parent.ToList(); // iterate filtered
            parent.ViewChanged += Parent_ViewChanged;
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
                        if (e.NewStartingIndex == -1)
                        {
                            var index = listView.Count;
                            listView.Add(e.NewItem.View);
                            OnCollectionChanged(e.WithNewStartingIndex(index));
                            return;
                        }
                        else
                        {
                            listView.Insert(e.NewStartingIndex, e.NewItem.View);
                        }
                    }
                    else
                    {
                        if (IsSupportRangeFeature)
                        {
#if NET8_0_OR_GREATER
                            listView.InsertRange(e.NewStartingIndex, e.NewViews);
#else
                            using var array = new CloneCollection<TView>(e.NewViews);
                            listView.InsertRange(e.NewStartingIndex, array.AsEnumerable());
#endif
                        }
                        else
                        {
                            var span = e.NewViews;
                            for (int i = 0; i < span.Length; i++)
                            {
                                var index = e.NewStartingIndex + i;
                                listView.Insert(index, span[i]);
                                var ev = new SynchronizedViewChangedEventArgs<T, TView>(e.Action, true, newItem: (e.NewValues[i], span[i]), newStartingIndex: index);
                                OnCollectionChanged(ev);
                            }
                            return;
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Remove: // Remove
                    {
                        if (e.IsSingleItem)
                        {
                            if (e.OldStartingIndex == -1) // can't gurantee correct remove if index is not provided
                            {
                                var index = listView.IndexOf(e.OldItem.View);
                                listView.RemoveAt(index);
                                OnCollectionChanged(e.WithOldStartingIndex(index));
                                return;
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
                                    var index = listView.IndexOf(view);
                                    listView.RemoveAt(index);
                                    OnCollectionChanged(e.WithOldStartingIndex(index));
                                }
                                return;
                            }
                            else
                            {
                                if (IsSupportRangeFeature)
                                {
                                    listView.RemoveRange(e.OldStartingIndex, e.OldViews.Length);
                                }
                                else
                                {
                                    var span = e.OldViews;
                                    for (int i = 0; i < span.Length; i++)
                                    {
                                        listView.RemoveAt(e.OldStartingIndex); // when removed, next remove index is same.
                                        var ev = new SynchronizedViewChangedEventArgs<T, TView>(e.Action, true, oldItem: (e.OldValues[i], span[i]), oldStartingIndex: e.OldStartingIndex);
                                        OnCollectionChanged(ev);
                                    }
                                    return;
                                }
                            }
                        }
                        break;
                    }
                case NotifyCollectionChangedAction.Replace: // Indexer
                    if (e.NewStartingIndex == -1)
                    {
                        var index = listView.IndexOf(e.OldItem.View);
                        if (index != -1)
                        {
                            listView[index] = e.NewItem.View;
                            OnCollectionChanged(e.WithNewAndOldStartingIndex(index, index));
                            return;
                        }
                        else
                        {
                            return;
                        }
                    }
                    else
                    {
                        listView[e.NewStartingIndex] = e.NewItem.View;
                    }
                    break;
                case NotifyCollectionChangedAction.Move: //Remove and Insert
                    if (e.NewStartingIndex == -1)
                    {
                        return; // do nothing
                    }
                    else
                    {
                        listView.RemoveAt(e.OldStartingIndex);
                        listView.Insert(e.NewStartingIndex, e.NewItem.View);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset: // Clear or drastic changes
                    if (e.SortOperation.IsClear)
                    {
                        listView.Clear();
                        foreach (var item in parent.Unfiltered) // refresh
                        {
                            listView.Add(item.View);
                        }
                    }
                    else if (e.SortOperation.IsReverse)
                    {
                        listView.Reverse(e.SortOperation.Index, e.SortOperation.Count);
                    }
                    else
                    {
#if NET6_0_OR_GREATER
#pragma warning disable CS0436
                        if (parent is ObservableList<T>.View<TView> observableListView && typeof(T) == typeof(TView))
                        {
                            var comparer = new ViewComparer(e.SortOperation.Comparer ?? Comparer<T>.Default);
                            var viewSpan = CollectionsMarshal.AsSpan(listView).Slice(e.SortOperation.Index, e.SortOperation.Count);
                            viewSpan.Sort(comparer);
                        }
                        else
#pragma warning restore CS0436
#endif
                        {
                            // can not get source Span, do Clear and Refresh
                            listView.Clear();
                            foreach (var item in parent.Unfiltered)
                            {
                                listView.Add(item.View);
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

    sealed class ViewComparer : IComparer<TView>
    {
        readonly IComparer<T> comparer;

        public ViewComparer(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        public int Compare(TView? x, TView? y)
        {
            var t1 = Unsafe.As<TView, T>(ref x!);
            var t2 = Unsafe.As<TView, T>(ref y!);
            return comparer.Compare(t1, t2);
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
        return GetEnumerator();
    }

    public void Dispose()
    {
        parent.ViewChanged -= Parent_ViewChanged;
        parent.Dispose(); // Dispose parent
    }
}

internal class FiltableWritableSynchronizedViewList<T, TView> : FiltableSynchronizedViewList<T, TView>, IWritableSynchronizedViewList<TView>
{
    IWritableSynchronizedView<T, TView> writableView;
    WritableViewChangedEventHandler<T, TView> converter;

    public FiltableWritableSynchronizedViewList(IWritableSynchronizedView<T, TView> parent, WritableViewChangedEventHandler<T, TView> converter) : base(parent)
    {
        this.writableView = parent;
        this.converter = converter;
    }

    public new TView this[int index]
    {
        get => base[index];
        set
        {
            lock (gate)
            {
                var originalIndex = listView.GetAlternateIndex(index);
                var (originalValue, _) = writableView.GetAt(originalIndex);

                // update view
                writableView.SetViewAt(originalIndex, value);
                listView[index] = value;

                var setValue = true;
                var newOriginal = converter(value, originalValue, ref setValue);

                if (setValue)
                {
                    writableView.SetToSourceCollection(originalIndex, newOriginal);
                }
            }
        }
    }
}

internal class NotifyCollectionChangedSynchronizedViewList<T, TView> :
    FiltableSynchronizedViewList<T, TView>,
    INotifyCollectionChangedSynchronizedViewList<TView>,
    IList<TView>, IList
{
    static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
    static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

    readonly ICollectionEventDispatcher eventDispatcher;
    WritableViewChangedEventHandler<T, TView>? converter; // null = readonly

    protected override bool IsSupportRangeFeature => false; // WPF, Avalonia etc does not support range notification

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public NotifyCollectionChangedSynchronizedViewList(ISynchronizedView<T, TView> parent, ICollectionEventDispatcher? eventDispatcher)
        : base(parent)
    {
        this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
    }

    public NotifyCollectionChangedSynchronizedViewList(ISynchronizedView<T, TView> parent, ICollectionEventDispatcher? eventDispatcher, WritableViewChangedEventHandler<T, TView>? converter)
        : base(parent)
    {
        this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
        this.converter = converter;
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
        var self = (NotifyCollectionChangedSynchronizedViewList<T, TView>)e2.Collection;

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
        set
        {
            if (converter == null || parent is not IWritableSynchronizedView<T, TView> writableView)
            {
                throw new NotSupportedException("This CollectionView does not support set. If base type is ObservableList<T>, you can use ToWritableSynchronizedView and ToWritableNotifyCollectionChanged.");
            }
            else
            {
                var originalIndex = listView.GetAlternateIndex(index);
                var (originalValue, _) = writableView.GetAt(originalIndex);

                // update view
                writableView.SetViewAt(originalIndex, value);
                listView[index] = value;

                var setValue = true;
                var newOriginal = converter(value, originalValue, ref setValue);

                if (setValue)
                {
                    writableView.SetToSourceCollection(originalIndex, newOriginal);
                }
            }
        }
    }

    object? IList.this[int index]
    {
        get
        {
            return this[index];
        }
        set => ((IList<TView>)this)[index] = (TView)value!;
    }

    static bool IsCompatibleObject(object? value)
    {
        return value is TView || value == null && default(TView) == null;
    }

    public bool IsReadOnly => true;

    public bool IsFixedSize => false;

    public bool IsSynchronized => true;

    public object SyncRoot => gate;

    public void Add(TView item)
    {
        if (converter == null || parent is not IWritableSynchronizedView<T, TView> writableView)
        {
            throw new NotSupportedException("This CollectionView does not support Add. If base type is ObservableList<T>, you can use ToWritableSynchronizedView and ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var setValue = false;
            var newOriginal = converter(item, default!, ref setValue);

            // always add
            writableView.AddToSourceCollection(newOriginal);
        }
    }

    public int Add(object? value)
    {
        Add((TView)value!);
        return -1; // itself does not add in this collection
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

internal class NonFilteredNotifyCollectionChangedSynchronizedViewList<T, TView> :
    NonFilteredSynchronizedViewList<T, TView>,
    INotifyCollectionChangedSynchronizedViewList<TView>,
    IList<TView>, IList
{
    static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
    static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

    readonly ICollectionEventDispatcher eventDispatcher;
    readonly WritableViewChangedEventHandler<T, TView>? converter; // null = readonly

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected override bool IsSupportRangeFeature => false; // WPF, Avalonia etc does not support range notification

    public NonFilteredNotifyCollectionChangedSynchronizedViewList(ISynchronizedView<T, TView> parent, ICollectionEventDispatcher? eventDispatcher)
        : base(parent)
    {
        this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
    }

    public NonFilteredNotifyCollectionChangedSynchronizedViewList(ISynchronizedView<T, TView> parent, ICollectionEventDispatcher? eventDispatcher, WritableViewChangedEventHandler<T, TView>? converter)
        : base(parent)
    {
        this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
        this.converter = converter;
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
        var self = (NonFilteredNotifyCollectionChangedSynchronizedViewList<T, TView>)e2.Collection;

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
        set
        {
            if (converter == null || parent is not IWritableSynchronizedView<T, TView> writableView)
            {
                throw new NotSupportedException("This CollectionView does not support set. If base type is ObservableList<T>, you can use ToWritableSynchronizedView and ToWritableNotifyCollectionChanged.");
            }
            else
            {
                var (originalValue, _) = writableView.GetAt(index);

                // update view
                writableView.SetViewAt(index, value);
                listView[index] = value;

                var setValue = true;
                var newOriginal = converter(value, originalValue, ref setValue);

                if (setValue)
                {
                    writableView.SetToSourceCollection(index, newOriginal);
                }
            }
        }
    }

    object? IList.this[int index]
    {
        get
        {
            return this[index];
        }
        set => ((IList<TView>)this)[index] = (TView)value!;
    }

    static bool IsCompatibleObject(object? value)
    {
        return value is TView || value == null && default(TView) == null;
    }

    public bool IsReadOnly => true;

    public bool IsFixedSize => false;

    public bool IsSynchronized => true;

    public object SyncRoot => gate;

    public void Add(TView item)
    {
        if (converter == null || parent is not IWritableSynchronizedView<T, TView> writableView)
        {
            throw new NotSupportedException("This CollectionView does not support Add. If base type is ObservableList<T>, you can use ToWritableSynchronizedView and ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var setValue = false;
            var newOriginal = converter(item, default!, ref setValue);

            // always add
            writableView.AddToSourceCollection(newOriginal);
        }
    }

    public int Add(object? value)
    {
        Add((TView)value!);
        return -1; // itself does not add in this collection
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