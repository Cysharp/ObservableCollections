using ObservableCollections.Internal;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace ObservableCollections;

internal sealed class FiltableSynchronizedViewList<T, TView> : NotifyCollectionChangedSynchronizedViewList<TView>
{
    static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
    static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

    const string PendingChangeHint = "Wait until the pending notifications are dispatched, or change the source collection directly.";

    readonly ISynchronizedView<T, TView> parent;
    readonly AlternateIndexList<TView> listView;
    readonly bool isSupportRangeFeature; // WPF, Avalonia etc does not support range notification

    readonly ICollectionEventDispatcher eventDispatcher;
    readonly DeferredViewList<TView>? deferred; // null = notification is not deferred, listView is directly visible
    readonly WritableViewChangedEventHandler<T, TView>? converter; // null = readonly

    NotifyCollectionChangedEventHandler? collectionChanged;
    PropertyChangedEventHandler? propertyChanged;

    // locked by gate so that "no subscriber" can be determined atomically with applying a change
    public override event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { lock (gate) { collectionChanged += value; } }
        remove { lock (gate) { collectionChanged -= value; } }
    }

    public override event PropertyChangedEventHandler? PropertyChanged
    {
        add { lock (gate) { propertyChanged += value; } }
        remove { lock (gate) { propertyChanged -= value; } }
    }

    public FiltableSynchronizedViewList(ISynchronizedView<T, TView> parent, bool isSupportRangeFeature, ICollectionEventDispatcher? eventDispatcher = null, WritableViewChangedEventHandler<T, TView>? converter = null)
    {
        this.parent = parent;
        this.isSupportRangeFeature = isSupportRangeFeature;
        this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
        this.converter = converter;
        lock (parent.SyncRoot)
        {
            listView = new AlternateIndexList<TView>(IterateFilteredIndexedViewsOfParent());
            // when a dispatcher defers the notification, defer the visible list too(see: issue #115)
            deferred = eventDispatcher == null ? null : new DeferredViewList<TView>(listView);
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
                if (filter.IsMatch(item))
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
                        if (isSupportRangeFeature)
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
                                if (index == -1) return; // not in the view, listView is not changed
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
                                var removedSpan = e.OldViews;
                                for (int i = 0; i < removedSpan.Length; i++) // index is unknown, can't do batching
                                {
                                    var removedIndex = listView.Remove(removedSpan[i]);
                                    if (removedIndex == -1) continue; // not in the view, listView is not changed
                                    var removedEv = new SynchronizedViewChangedEventArgs<T, TView>(e.Action, true, oldItem: (e.OldValues[i], removedSpan[i]), oldStartingIndex: removedIndex);
                                    OnCollectionChanged(removedEv);
                                }
                                return;
                            }
                            else
                            {
                                if (isSupportRangeFeature)
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
                        return;
                    }
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

    void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> args)
    {
        if (deferred == null && collectionChanged == null && propertyChanged == null) return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.IsSingleItem)
                {
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewItem.View, args.NewStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewViews.ToArray(), args.NewStartingIndex)
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
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldItem.View, args.OldStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldViews.ToArray(), args.OldStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Reset)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = true
                }, deferred == null ? null : listView.ToArray()); // Reset does not carry items
                break;
            case NotifyCollectionChangedAction.Replace:
                Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, args.NewItem.View, args.OldItem.View, args.NewStartingIndex)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = false
                });
                break;
            case NotifyCollectionChangedAction.Move:
                Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Move, args.NewItem.View, args.NewStartingIndex, args.OldStartingIndex)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = false
                });
                break;
        }
    }

    void Publish(CollectionEventDispatcherEventArgs ev, TView[]? resetSnapshot = null)
    {
        // called inside gate
        if (deferred == null)
        {
            eventDispatcher.Post(ev);
            return;
        }

        if (deferred.PendingCount == 0 && collectionChanged == null && propertyChanged == null)
        {
            // there is no notification to be consistent with, so apply it here
            deferred.ApplyWithoutNotification(ev, resetSnapshot);
            return;
        }

        deferred.Enqueue(ev, resetSnapshot);
        eventDispatcher.Post(ev);
    }

    static void RaiseChangedEvent(NotifyCollectionChangedEventArgs e)
    {
        var e2 = (CollectionEventDispatcherEventArgs)e;
        var self = (FiltableSynchronizedViewList<T, TView>)e2.Collection;
        self.InvokeChangedEvent(e2);
    }

    void InvokeChangedEvent(CollectionEventDispatcherEventArgs e)
    {
        if (deferred == null)
        {
            RaiseChangedEventCore(e);
            return;
        }

        List<Exception>? exceptions = null;

        while (true)
        {
            CollectionEventDispatcherEventArgs applied;
            lock (gate)
            {
                // apply the change to the visible list at the same time as raising the notification
                if (!deferred.TryApplyNext(e, out applied)) break; // already applied
            }

            try
            {
                // do not raise inside gate, a subscriber may touch the source collection
                RaiseChangedEventCore(applied);
            }
            catch (Exception ex)
            {
                // a subscriber must not stop the remaining changes from being applied and raised
                (exceptions ??= new()).Add(ex);
            }

            if (ReferenceEquals(applied, e)) break;
        }

        if (exceptions != null)
        {
            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            }

            throw new AggregateException(exceptions);
        }
    }

    void RaiseChangedEventCore(CollectionEventDispatcherEventArgs e)
    {
        if (e.IsInvokeCollectionChanged)
        {
            collectionChanged?.Invoke(this, e);
        }
        if (e.IsInvokePropertyChanged)
        {
            propertyChanged?.Invoke(this, CountPropertyChangedEventArgs);
        }
    }

    /// <summary>
    /// Validates an index of the visible list and translates it into an index of listView.
    /// Throws ArgumentOutOfRangeException when the index is out of the range of the visible list, and
    /// InvalidOperationException when a pending change makes it impossible to translate. An insertion point
    /// survives a pending remove of the element at that position, so only a pending reset fails it.
    /// </summary>
    int ToListViewIndex(int index, bool isInsertionPoint)
    {
        // called inside gate
        // validate before translating, an untrackable index is -1 and a caller may pass -1 too
        var count = deferred == null ? listView.Count : deferred.Count;
        var max = isInsertionPoint ? count : count - 1;
        if (index < 0 || index > max)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is out of the range of the collection. Count: " + count);
        }

        if (deferred == null) return index;

        var listViewIndex = deferred.ToWriterIndex(index, isInsertionPoint, out var reason);
        if (listViewIndex == DeferredViewList<TView>.UntrackableIndex)
        {
            throw new InvalidOperationException(reason == UntrackableReason.Reset
                ? "The collection has been reset and the notification is not dispatched yet, so no index can be resolved. " + PendingChangeHint
                : "The element at index " + index + " has been removed and the notification is not dispatched yet. " + PendingChangeHint);
        }
        return listViewIndex;
    }

    public override TView this[int index]
    {
        get
        {
            lock (gate)
            {
                return deferred == null ? listView[index] : deferred[index];
            }
        }
        set
        {
            if (IsReadOnly)
            {
                throw new NotSupportedException("This CollectionView does not support Set. If base type is ObservableList<T>, you can use CreateWritableView and ToWritableNotifyCollectionChanged.");
            }
            else
            {
                var writableView = parent as IWritableSynchronizedView<T, TView>;

                int listViewIndex;
                int originalIndex;
                lock (gate)
                {
                    listViewIndex = ToListViewIndex(index, isInsertionPoint: false);
                    originalIndex = listView.GetAlternateIndex(listViewIndex);
                }

                var (originalValue, _) = writableView!.GetAt(originalIndex);

                var setValue = true;
                var newOriginal = converter!(value, originalValue, ref setValue);

                // update view
                writableView.SetViewAt(originalIndex, value);

                if (setValue)
                {
                    // the Replace of the source updates listView and the visible list, with a notification.
                    // do not touch listView here, it would be an unnotified change if the source write fails
                    writableView.SetToSourceCollection(originalIndex, newOriginal);
                }
                else
                {
                    // the converter rejected the source write, so this is the only notification of the change
                    lock (gate)
                    {
                        var oldView = listView[listViewIndex];
                        listView[listViewIndex] = value;
                        Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, value, oldView, listViewIndex)
                        {
                            Collection = this,
                            Invoker = raiseChangedEventInvoke,
                            IsInvokeCollectionChanged = true,
                            IsInvokePropertyChanged = false
                        });
                    }
                }
            }
        }
    }

    public override int Count
    {
        get
        {
            lock (gate)
            {
                return deferred == null ? listView.Count : deferred.Count;
            }
        }
    }

    public override bool IsReadOnly => converter == null || parent is not IWritableSynchronizedView<T, TView>;

    public override IEnumerator<TView> GetEnumerator()
    {
        lock (gate)
        {
            foreach (var item in deferred == null ? listView : deferred.Items)
            {
                yield return item;
            }
        }
    }

    public override void Add(TView item)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Add. If base type is ObservableList<T>, you can use CreateWritableView and ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;
            if (typeof(T) == typeof(TView) && item is T tItem)
            {
                writableView!.AddToSourceCollection(tItem);
                return;
            }
            var setValue = false;
            var newOriginal = converter!(item, default!, ref setValue);

            // always add
            writableView!.AddToSourceCollection(newOriginal);
        }
    }

    public override void Insert(int index, TView item)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Insert. If base type is ObservableList<T>, you can use CreateWritableView and ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;

            int originalIndex;
            lock (gate)
            {
                var listViewIndex = ToListViewIndex(index, isInsertionPoint: true);

                // when the insertion point is the tail of the view, the source index is not determined, so append
                originalIndex = listViewIndex < listView.Count ? listView.GetAlternateIndex(listViewIndex) : -1;
            }

            if (typeof(T) == typeof(TView) && item is T tItem)
            {
                InsertIntoSourceCollection(writableView!, originalIndex, tItem);
                return;
            }
            var setValue = false;
            var newOriginal = converter!(item, default!, ref setValue);

            InsertIntoSourceCollection(writableView!, originalIndex, newOriginal);
        }
    }

    static void InsertIntoSourceCollection(IWritableSynchronizedView<T, TView> writableView, int originalIndex, T value)
    {
        if (originalIndex == -1)
        {
            writableView.AddToSourceCollection(value);
        }
        else
        {
            writableView.InsertIntoSourceCollection(originalIndex, value);
        }
    }

    public override bool Remove(TView item)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Remove. If base type is ObservableList<T>, you can use CreateWritableView and ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;
            if (typeof(T) == typeof(TView) && item is T tItem)
            {
                return writableView!.RemoveFromSourceCollection(tItem);
            }
            var setValue = false;
            var newOriginal = converter!(item, default!, ref setValue);

            // always add
            return writableView!.RemoveFromSourceCollection(newOriginal);
        }
    }

    public override void RemoveAt(int index)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support RemoveAt. If base type is ObservableList<T>, you can use CreateWritableView and ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;

            int originalIndex;
            lock (gate)
            {
                originalIndex = listView.GetAlternateIndex(ToListViewIndex(index, isInsertionPoint: false));
            }

            writableView!.RemoveAtSourceCollection(originalIndex);
        }
    }

    public override void Clear()
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Clear. If base type is ObservableList<T>, you can use CreateWritableView and ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;
            writableView!.ClearSourceCollection();
        }
    }

    public override bool Contains(TView item)
    {
        lock (gate)
        {
            if (deferred != null)
            {
                return deferred.Contains(item);
            }

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

    public override int IndexOf(TView item)
    {
        lock (gate)
        {
            if (deferred != null)
            {
                return deferred.IndexOf(item);
            }

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

    public override void Dispose()
    {
        parent.ViewChanged -= Parent_ViewChanged;
        parent.RejectedViewChanged -= Parent_RejectedViewChanged;
    }
}

internal sealed class NonFilteredSynchronizedViewList<T, TView> : NotifyCollectionChangedSynchronizedViewList<TView>
{
    static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
    static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

    const string PendingChangeHint = "Wait until the pending notifications are dispatched, or change the source collection directly.";

    readonly ISynchronizedView<T, TView> parent;
    readonly List<TView> listView; // no filter can be faster
    readonly bool isSupportRangeFeature; // WPF, Avalonia etc does not support range notification

    readonly ICollectionEventDispatcher eventDispatcher;
    readonly DeferredViewList<TView>? deferred; // null = notification is not deferred, listView is directly visible
    readonly WritableViewChangedEventHandler<T, TView>? converter; // null = readonly

    NotifyCollectionChangedEventHandler? collectionChanged;
    PropertyChangedEventHandler? propertyChanged;

    // locked by gate so that "no subscriber" can be determined atomically with applying a change
    public override event NotifyCollectionChangedEventHandler? CollectionChanged
    {
        add { lock (gate) { collectionChanged += value; } }
        remove { lock (gate) { collectionChanged -= value; } }
    }

    public override event PropertyChangedEventHandler? PropertyChanged
    {
        add { lock (gate) { propertyChanged += value; } }
        remove { lock (gate) { propertyChanged -= value; } }
    }

    public NonFilteredSynchronizedViewList(ISynchronizedView<T, TView> parent, bool isSupportRangeFeature, ICollectionEventDispatcher? eventDispatcher, WritableViewChangedEventHandler<T, TView>? converter)
    {
        this.parent = parent;
        this.isSupportRangeFeature = isSupportRangeFeature;
        this.eventDispatcher = eventDispatcher ?? InlineCollectionEventDispatcher.Instance;
        this.converter = converter;
        lock (parent.SyncRoot)
        {
            listView = parent.ToList(); // guranteed non-filtered
            // when a dispatcher defers the notification, defer the visible list too(see: issue #115)
            deferred = eventDispatcher == null ? null : new DeferredViewList<TView>(listView);
            parent.ViewChanged += Parent_ViewChanged;
            // no register RejectedViewChanged(beacuse non filtered)
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
                        if (isSupportRangeFeature)
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
                                if (index == -1) return; // not in the view, listView is not changed
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
                                var removedSpan = e.OldViews;
                                for (int i = 0; i < removedSpan.Length; i++) // index is unknown, can't do batching
                                {
                                    var removedIndex = listView.IndexOf(removedSpan[i]);
                                    if (removedIndex == -1) continue; // not in the view, listView is not changed
                                    listView.RemoveAt(removedIndex);
                                    var removedEv = new SynchronizedViewChangedEventArgs<T, TView>(e.Action, true, oldItem: (e.OldValues[i], removedSpan[i]), oldStartingIndex: removedIndex);
                                    OnCollectionChanged(removedEv);
                                }
                                return;
                            }
                            else
                            {
                                if (isSupportRangeFeature)
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

    void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> args)
    {
        if (deferred == null && collectionChanged == null && propertyChanged == null) return;

        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (args.IsSingleItem)
                {
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewItem.View, args.NewStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewViews.ToArray(), args.NewStartingIndex)
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
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldItem.View, args.OldStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                else
                {
                    Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldViews.ToArray(), args.OldStartingIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Reset)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = true
                }, deferred == null ? null : listView.ToArray()); // Reset does not carry items
                break;
            case NotifyCollectionChangedAction.Replace:
                Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, args.NewItem.View, args.OldItem.View, args.NewStartingIndex)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = false
                });
                break;
            case NotifyCollectionChangedAction.Move:
                Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Move, args.NewItem.View, args.NewStartingIndex, args.OldStartingIndex)
                {
                    Collection = this,
                    Invoker = raiseChangedEventInvoke,
                    IsInvokeCollectionChanged = true,
                    IsInvokePropertyChanged = false
                });
                break;
        }
    }

    void Publish(CollectionEventDispatcherEventArgs ev, TView[]? resetSnapshot = null)
    {
        // called inside gate
        if (deferred == null)
        {
            eventDispatcher.Post(ev);
            return;
        }

        if (deferred.PendingCount == 0 && collectionChanged == null && propertyChanged == null)
        {
            // there is no notification to be consistent with, so apply it here
            deferred.ApplyWithoutNotification(ev, resetSnapshot);
            return;
        }

        deferred.Enqueue(ev, resetSnapshot);
        eventDispatcher.Post(ev);
    }

    static void RaiseChangedEvent(NotifyCollectionChangedEventArgs e)
    {
        var e2 = (CollectionEventDispatcherEventArgs)e;
        var self = (NonFilteredSynchronizedViewList<T, TView>)e2.Collection;
        self.InvokeChangedEvent(e2);
    }

    void InvokeChangedEvent(CollectionEventDispatcherEventArgs e)
    {
        if (deferred == null)
        {
            RaiseChangedEventCore(e);
            return;
        }

        List<Exception>? exceptions = null;

        while (true)
        {
            CollectionEventDispatcherEventArgs applied;
            lock (gate)
            {
                // apply the change to the visible list at the same time as raising the notification
                if (!deferred.TryApplyNext(e, out applied)) break; // already applied
            }

            try
            {
                // do not raise inside gate, a subscriber may touch the source collection
                RaiseChangedEventCore(applied);
            }
            catch (Exception ex)
            {
                // a subscriber must not stop the remaining changes from being applied and raised
                (exceptions ??= new()).Add(ex);
            }

            if (ReferenceEquals(applied, e)) break;
        }

        if (exceptions != null)
        {
            if (exceptions.Count == 1)
            {
                ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
            }

            throw new AggregateException(exceptions);
        }
    }

    void RaiseChangedEventCore(CollectionEventDispatcherEventArgs e)
    {
        if (e.IsInvokeCollectionChanged)
        {
            collectionChanged?.Invoke(this, e);
        }
        if (e.IsInvokePropertyChanged)
        {
            propertyChanged?.Invoke(this, CountPropertyChangedEventArgs);
        }
    }

    /// <summary>
    /// Validates an index of the visible list and translates it into an index of listView(it is same as the source index).
    /// Throws ArgumentOutOfRangeException when the index is out of the range of the visible list, and
    /// InvalidOperationException when a pending change makes it impossible to translate. An insertion point
    /// survives a pending remove of the element at that position, so only a pending reset fails it.
    /// </summary>
    int ToListViewIndex(int index, bool isInsertionPoint)
    {
        // called inside gate
        // validate before translating, an untrackable index is -1 and a caller may pass -1 too
        var count = deferred == null ? listView.Count : deferred.Count;
        var max = isInsertionPoint ? count : count - 1;
        if (index < 0 || index > max)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The index is out of the range of the collection. Count: " + count);
        }

        if (deferred == null) return index;

        var listViewIndex = deferred.ToWriterIndex(index, isInsertionPoint, out var reason);
        if (listViewIndex == DeferredViewList<TView>.UntrackableIndex)
        {
            throw new InvalidOperationException(reason == UntrackableReason.Reset
                ? "The collection has been reset and the notification is not dispatched yet, so no index can be resolved. " + PendingChangeHint
                : "The element at index " + index + " has been removed and the notification is not dispatched yet. " + PendingChangeHint);
        }
        return listViewIndex;
    }

    public override TView this[int index]
    {
        get
        {
            lock (gate)
            {
                return deferred == null ? listView[index] : deferred[index];
            }
        }
        set
        {
            if (IsReadOnly)
            {
                throw new NotSupportedException("This CollectionView does not support set. If base type is ObservableList<T>, you can use ToWritableNotifyCollectionChanged.");
            }
            else
            {
                var writableView = parent as IWritableSynchronizedView<T, TView>;

                int listViewIndex;
                lock (gate)
                {
                    listViewIndex = ToListViewIndex(index, isInsertionPoint: false);
                }

                var (originalValue, _) = writableView!.GetAt(listViewIndex);

                var setValue = true;
                var newOriginal = converter!(value, originalValue, ref setValue);

                // update view
                writableView.SetViewAt(listViewIndex, value);

                if (setValue)
                {
                    // the Replace of the source updates listView and the visible list, with a notification.
                    // do not touch listView here, it would be an unnotified change if the source write fails
                    writableView.SetToSourceCollection(listViewIndex, newOriginal);
                }
                else
                {
                    // the converter rejected the source write, so this is the only notification of the change
                    lock (gate)
                    {
                        var oldView = listView[listViewIndex];
                        listView[listViewIndex] = value;
                        Publish(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, value, oldView, listViewIndex)
                        {
                            Collection = this,
                            Invoker = raiseChangedEventInvoke,
                            IsInvokeCollectionChanged = true,
                            IsInvokePropertyChanged = false
                        });
                    }
                }
            }
        }
    }

    public override int Count
    {
        get
        {
            lock (gate)
            {
                return deferred == null ? listView.Count : deferred.Count;
            }
        }
    }

    public override bool IsReadOnly => converter == null || parent is not IWritableSynchronizedView<T, TView>;

    public override IEnumerator<TView> GetEnumerator()
    {
        lock (gate)
        {
            foreach (var item in deferred == null ? listView : deferred.Items)
            {
                yield return item;
            }
        }
    }

    public override void Add(TView item)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Add. If base type is ObservableList<T>, you can use ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;
            if (typeof(T) == typeof(TView) && item is T tItem)
            {
                writableView!.AddToSourceCollection(tItem);
                return;
            }
            var setValue = false;
            var newOriginal = converter!(item, default!, ref setValue);

            // always add
            writableView!.AddToSourceCollection(newOriginal);
        }
    }

    public override void Insert(int index, TView item)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Insert. If base type is ObservableList<T>, you can use ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;

            int originalIndex;
            lock (gate)
            {
                originalIndex = ToListViewIndex(index, isInsertionPoint: true);
            }

            if (typeof(T) == typeof(TView) && item is T tItem)
            {
                writableView!.InsertIntoSourceCollection(originalIndex, tItem);
                return;
            }
            var setValue = false;
            var newOriginal = converter!(item, default!, ref setValue);

            writableView!.InsertIntoSourceCollection(originalIndex, newOriginal);
        }
    }

    public override bool Remove(TView item)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Remove. If base type is ObservableList<T>, you can use ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;
            if (typeof(T) == typeof(TView) && item is T tItem)
            {
                return writableView!.RemoveFromSourceCollection(tItem);
            }
            var setValue = false;
            var newOriginal = converter!(item, default!, ref setValue);

            // always add
            return writableView!.RemoveFromSourceCollection(newOriginal);
        }
    }

    public override void RemoveAt(int index)
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support RemoveAt. If base type is ObservableList<T>, you can use ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;

            int originalIndex;
            lock (gate)
            {
                originalIndex = ToListViewIndex(index, isInsertionPoint: false);
            }

            writableView!.RemoveAtSourceCollection(originalIndex);
        }
    }

    public override void Clear()
    {
        if (IsReadOnly)
        {
            throw new NotSupportedException("This CollectionView does not support Clear. If base type is ObservableList<T>, you can use ToWritableNotifyCollectionChanged.");
        }
        else
        {
            var writableView = parent as IWritableSynchronizedView<T, TView>;
            writableView!.ClearSourceCollection();
        }
    }

    public override bool Contains(TView item)
    {
        lock (gate)
        {
            if (deferred != null)
            {
                return deferred.Contains(item);
            }

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

    public override int IndexOf(TView item)
    {
        lock (gate)
        {
            if (deferred != null)
            {
                return deferred.IndexOf(item);
            }

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

    public override void Dispose()
    {
        parent.ViewChanged -= Parent_ViewChanged;
        parent.Dispose(); // Dispose parent
    }
}
