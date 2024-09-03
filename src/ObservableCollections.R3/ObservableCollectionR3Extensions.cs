using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using R3;

namespace ObservableCollections;

public readonly record struct CollectionAddEvent<T>(int Index, T Value);
public readonly record struct CollectionRemoveEvent<T>(int Index, T Value);
public readonly record struct CollectionReplaceEvent<T>(int Index, T OldValue, T NewValue);
public readonly record struct CollectionMoveEvent<T>(int OldIndex, int NewIndex, T Value);
public readonly record struct CollectionResetEvent<T>
{
    readonly SortOperation<T> sortOperation;

    public bool IsClear => sortOperation.IsClear;
    public bool IsSort => sortOperation.IsSort;
    public bool IsReverse => sortOperation.IsReverse;
    public int Index => sortOperation.Index;
    public int Count => sortOperation.Count;
    public IComparer<T>? Comparer => sortOperation.Comparer;

    public CollectionResetEvent(SortOperation<T> sortOperation)
    {
        this.sortOperation = sortOperation;
    }
}

public readonly record struct DictionaryAddEvent<TKey, TValue>(TKey Key, TValue Value);

public readonly record struct DictionaryRemoveEvent<TKey, TValue>(TKey Key, TValue Value);

public readonly record struct DictionaryReplaceEvent<TKey, TValue>(TKey Key, TValue OldValue, TValue NewValue);


public static class ObservableCollectionR3Extensions
{
    public static Observable<CollectionAddEvent<T>> ObserveAdd<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionAdd<T>(source, cancellationToken);
    }

    public static Observable<CollectionRemoveEvent<T>> ObserveRemove<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionRemove<T>(source, cancellationToken);
    }

    public static Observable<CollectionReplaceEvent<T>> ObserveReplace<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionReplace<T>(source, cancellationToken);
    }

    public static Observable<CollectionMoveEvent<T>> ObserveMove<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionMove<T>(source, cancellationToken);
    }

    public static Observable<CollectionResetEvent<T>> ObserveReset<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionReset<T>(source, cancellationToken);
    }

    public static Observable<Unit> ObserveClear<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionClear<T>(source, cancellationToken);
    }

    public static Observable<(int Index, int Count)> ObserveReverse<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionReverse<T>(source, cancellationToken);
    }

    public static Observable<(int Index, int Count, IComparer<T>? Comparer)> ObserveSort<T>(this IObservableCollection<T> source, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionSort<T>(source, cancellationToken);
    }

    public static Observable<int> ObserveCountChanged<T>(this IObservableCollection<T> source, bool notifyCurrentCount = false, CancellationToken cancellationToken = default)
    {
        return new ObservableCollectionCountChanged<T>(source, notifyCurrentCount, cancellationToken);
    }
}

public static class ObservableDictionaryR3Extensions
{
    public static Observable<DictionaryAddEvent<TKey, TValue>> ObserveDictionaryAdd<TKey, TValue>(this IReadOnlyObservableDictionary<TKey, TValue> source,
        CancellationToken cancellationToken = default)
    {
        return new ObservableDictionaryAdd<TKey, TValue>(source, cancellationToken);
    }

    public static Observable<DictionaryRemoveEvent<TKey, TValue>> ObserveDictionaryRemove<TKey, TValue>(this IReadOnlyObservableDictionary<TKey, TValue> source,
        CancellationToken cancellationToken = default)
    {
        return new ObservableDictionaryRemove<TKey, TValue>(source, cancellationToken);
    }
    public static Observable<DictionaryReplaceEvent<TKey, TValue>> ObserveDictionaryReplace<TKey, TValue>(this IReadOnlyObservableDictionary<TKey, TValue> source,
        CancellationToken cancellationToken = default)
    {
        return new ObservableDictionaryReplace<TKey, TValue>(source, cancellationToken);
    }
}

sealed class ObservableCollectionAdd<T>(IObservableCollection<T> collection, CancellationToken cancellationToken)
    : Observable<CollectionAddEvent<T>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionAddEvent<T>> observer)
    {
        return new _ObservableCollectionAdd(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionAdd(
        IObservableCollection<T> collection,
        Observer<CollectionAddEvent<T>> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, CollectionAddEvent<T>>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Add)
            {
                if (eventArgs.IsSingleItem)
                {
                    observer.OnNext(new CollectionAddEvent<T>(eventArgs.NewStartingIndex, eventArgs.NewItem));
                }
                else
                {
                    var i = eventArgs.NewStartingIndex;
                    foreach (var item in eventArgs.NewItems)
                    {
                        observer.OnNext(new CollectionAddEvent<T>(i++, item));
                    }
                }
            }
        }
    }
}

sealed class ObservableCollectionRemove<T>(IObservableCollection<T> collection, CancellationToken cancellationToken)
    : Observable<CollectionRemoveEvent<T>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionRemoveEvent<T>> observer)
    {
        return new _ObservableCollectionRemove(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionRemove(
        IObservableCollection<T> collection,
        Observer<CollectionRemoveEvent<T>> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, CollectionRemoveEvent<T>>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Remove)
            {
                if (eventArgs.IsSingleItem)
                {
                    observer.OnNext(new CollectionRemoveEvent<T>(eventArgs.OldStartingIndex, eventArgs.OldItem));
                }
                else
                {
                    var i = eventArgs.OldStartingIndex;
                    foreach (var item in eventArgs.OldItems)
                    {
                        observer.OnNext(new CollectionRemoveEvent<T>(i++, item));
                    }
                }
            }
        }
    }
}

sealed class ObservableCollectionReplace<T>(IObservableCollection<T> collection, CancellationToken cancellationToken)
    : Observable<CollectionReplaceEvent<T>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionReplaceEvent<T>> observer)
    {
        return new _ObservableCollectionReplace(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionReplace(
        IObservableCollection<T> collection,
        Observer<CollectionReplaceEvent<T>> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, CollectionReplaceEvent<T>>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Replace)
            {
                observer.OnNext(new CollectionReplaceEvent<T>(eventArgs.NewStartingIndex, eventArgs.OldItem, eventArgs.NewItem));
            }
        }
    }
}

sealed class ObservableCollectionMove<T>(IObservableCollection<T> collection, CancellationToken cancellationToken)
    : Observable<CollectionMoveEvent<T>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionMoveEvent<T>> observer)
    {
        return new _ObservableCollectionMove(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionMove(
        IObservableCollection<T> collection,
        Observer<CollectionMoveEvent<T>> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, CollectionMoveEvent<T>>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Move)
            {
                observer.OnNext(new CollectionMoveEvent<T>(eventArgs.OldStartingIndex, eventArgs.NewStartingIndex, eventArgs.NewItem));
            }
        }
    }
}
sealed class ObservableCollectionReset<T>(IObservableCollection<T> collection, CancellationToken cancellationToken)
    : Observable<CollectionResetEvent<T>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionResetEvent<T>> observer)
    {
        return new _ObservableCollectionReset(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionReset(
        IObservableCollection<T> collection,
        Observer<CollectionResetEvent<T>> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, CollectionResetEvent<T>>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset)
            {
                observer.OnNext(new CollectionResetEvent<T>(eventArgs.SortOperation));
            }
        }
    }
}

sealed class ObservableCollectionClear<T>(IObservableCollection<T> collection, CancellationToken cancellationToken)
    : Observable<Unit>
{
    protected override IDisposable SubscribeCore(Observer<Unit> observer)
    {
        return new _ObservableCollectionClear(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionClear(
        IObservableCollection<T> collection,
        Observer<Unit> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, Unit>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset && eventArgs.SortOperation.IsClear)
            {
                observer.OnNext(Unit.Default);
            }
        }
    }
}

sealed class ObservableCollectionReverse<T>(IObservableCollection<T> collection, CancellationToken cancellationToken) : Observable<(int Index, int Count)>
{
    protected override IDisposable SubscribeCore(Observer<(int Index, int Count)> observer)
    {
        return new _ObservableCollectionReverse(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionReverse(
        IObservableCollection<T> collection,
        Observer<(int Index, int Count)> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, (int Index, int Count)>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset && eventArgs.SortOperation.IsReverse)
            {
                observer.OnNext((eventArgs.SortOperation.Index, eventArgs.SortOperation.Count));
            }
        }
    }
}

sealed class ObservableCollectionSort<T>(IObservableCollection<T> collection, CancellationToken cancellationToken) : Observable<(int Index, int Count, IComparer<T>? Comparer)>
{
    protected override IDisposable SubscribeCore(Observer<(int Index, int Count, IComparer<T>? Comparer)> observer)
    {
        return new _ObservableCollectionSort(collection, observer, cancellationToken);
    }

    sealed class _ObservableCollectionSort(
        IObservableCollection<T> collection,
        Observer<(int Index, int Count, IComparer<T>? Comparer)> observer,
        CancellationToken cancellationToken)
        : ObservableCollectionObserverBase<T, (int Index, int Count, IComparer<T>? Comparer)>(collection, observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset && eventArgs.SortOperation.IsSort)
            {
                observer.OnNext(eventArgs.SortOperation.AsTuple());
            }
        }
    }
}

sealed class ObservableCollectionCountChanged<T>(IObservableCollection<T> collection, bool notifyCurrentCount, CancellationToken cancellationToken)
    : Observable<int>
{
    protected override IDisposable SubscribeCore(Observer<int> observer)
    {
        return new _ObservableCollectionCountChanged(collection, notifyCurrentCount, observer, cancellationToken);
    }

    sealed class _ObservableCollectionCountChanged : ObservableCollectionObserverBase<T, int>
    {
        int countPrev;

        public _ObservableCollectionCountChanged(
            IObservableCollection<T> collection,
            bool notifyCurrentCount,
            Observer<int> observer,
            CancellationToken cancellationToken) : base(collection, observer, cancellationToken)
        {
            this.countPrev = collection.Count;
            if (notifyCurrentCount)
            {
                observer.OnNext(collection.Count);
            }
        }

        protected override void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            switch (eventArgs.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Reset when countPrev != collection.Count:
                    observer.OnNext(collection.Count);
                    break;
            }
            countPrev = collection.Count;
        }
    }
}

sealed class ObservableDictionaryAdd<TKey, TValue>(
    IReadOnlyObservableDictionary<TKey, TValue> dictionary,
    CancellationToken cancellationToken) : Observable<DictionaryAddEvent<TKey, TValue>>
{
    protected override IDisposable SubscribeCore(Observer<DictionaryAddEvent<TKey, TValue>> observer)
    {
        return new _DictionaryCollectionAdd(dictionary, observer, cancellationToken);
    }

    sealed class _DictionaryCollectionAdd(
        IObservableCollection<KeyValuePair<TKey, TValue>> collection,
        Observer<DictionaryAddEvent<TKey, TValue>> observer,
        CancellationToken cancellationToken) :
        ObservableCollectionObserverBase<KeyValuePair<TKey, TValue>, DictionaryAddEvent<TKey, TValue>>(collection,
            observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<KeyValuePair<TKey, TValue>> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Add)
            {
                if (eventArgs.IsSingleItem)
                {
                    observer.OnNext(
                        new DictionaryAddEvent<TKey, TValue>(eventArgs.NewItem.Key, eventArgs.NewItem.Value));
                }
                else
                {
                    var i = eventArgs.NewStartingIndex;
                    foreach (var item in eventArgs.NewItems)
                    {
                        observer.OnNext(new DictionaryAddEvent<TKey, TValue>(item.Key, item.Value));
                    }
                }
            }
        }
    }
}

sealed class ObservableDictionaryRemove<TKey, TValue>(
    IReadOnlyObservableDictionary<TKey, TValue> dictionary,
    CancellationToken cancellationToken) : Observable<DictionaryRemoveEvent<TKey, TValue>>
{
    protected override IDisposable SubscribeCore(Observer<DictionaryRemoveEvent<TKey, TValue>> observer)
    {
        return new _DictionaryCollectionRemove(dictionary, observer, cancellationToken);
    }

    sealed class _DictionaryCollectionRemove(
        IObservableCollection<KeyValuePair<TKey, TValue>> collection,
        Observer<DictionaryRemoveEvent<TKey, TValue>> observer,
        CancellationToken cancellationToken) :
        ObservableCollectionObserverBase<KeyValuePair<TKey, TValue>, DictionaryRemoveEvent<TKey, TValue>>(collection,
            observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<KeyValuePair<TKey, TValue>> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Remove)
            {
                if (eventArgs.IsSingleItem)
                {
                    observer.OnNext(
                        new DictionaryRemoveEvent<TKey, TValue>(eventArgs.OldItem.Key, eventArgs.OldItem.Value));
                }
                else
                {
                    var i = eventArgs.NewStartingIndex;
                    foreach (var item in eventArgs.NewItems)
                    {
                        observer.OnNext(new DictionaryRemoveEvent<TKey, TValue>(item.Key, item.Value));
                    }
                }
            }
        }
    }
}

sealed class ObservableDictionaryReplace<TKey, TValue>(
    IReadOnlyObservableDictionary<TKey, TValue> dictionary,
    CancellationToken cancellationToken) : Observable<DictionaryReplaceEvent<TKey, TValue>>
{
    protected override IDisposable SubscribeCore(Observer<DictionaryReplaceEvent<TKey, TValue>> observer)
    {
        return new _DictionaryCollectionReplace(dictionary, observer, cancellationToken);
    }

    sealed class _DictionaryCollectionReplace(
        IObservableCollection<KeyValuePair<TKey, TValue>> collection,
        Observer<DictionaryReplaceEvent<TKey, TValue>> observer,
        CancellationToken cancellationToken) :
        ObservableCollectionObserverBase<KeyValuePair<TKey, TValue>, DictionaryReplaceEvent<TKey, TValue>>(collection,
            observer, cancellationToken)
    {
        protected override void Handler(in NotifyCollectionChangedEventArgs<KeyValuePair<TKey, TValue>> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Replace)
            {
                observer.OnNext(new DictionaryReplaceEvent<TKey, TValue>(
                    eventArgs.NewItem.Key,
                    eventArgs.OldItem.Value,
                    eventArgs.NewItem.Value));
            }
        }
    }
}

abstract class ObservableCollectionObserverBase<T, TEvent> : IDisposable
{
    protected readonly IObservableCollection<T> collection;
    protected readonly Observer<TEvent> observer;
    readonly CancellationTokenRegistration cancellationTokenRegistration;
    readonly NotifyCollectionChangedEventHandler<T> handlerDelegate;

    public ObservableCollectionObserverBase(IObservableCollection<T> collection, Observer<TEvent> observer, CancellationToken cancellationToken)
    {
        this.collection = collection;
        this.observer = observer;
        this.handlerDelegate = Handler;

        collection.CollectionChanged += handlerDelegate;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationTokenRegistration = cancellationToken.UnsafeRegister(static state =>
            {
                var s = (ObservableCollectionObserverBase<T, TEvent>)state!;
                s.observer.OnCompleted();
                s.Dispose();
            }, this);
        }
    }

    public void Dispose()
    {
        collection.CollectionChanged -= handlerDelegate;
        cancellationTokenRegistration.Dispose();
    }

    protected abstract void Handler(in NotifyCollectionChangedEventArgs<T> eventArgs);
}
