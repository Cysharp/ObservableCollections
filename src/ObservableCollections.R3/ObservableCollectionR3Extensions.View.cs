using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;
using System.Threading;
using R3;

namespace ObservableCollections;

[StructLayout(LayoutKind.Auto)]
public readonly record struct ViewChangedEvent<T, TView>
{
    public readonly NotifyCollectionChangedAction Action;
    public readonly (T Value, TView View) NewItem;
    public readonly (T Value, TView View) OldItem;
    public readonly int NewStartingIndex;
    public readonly int OldStartingIndex;
    public readonly SortOperation<T> SortOperation;

    public ViewChangedEvent(NotifyCollectionChangedAction action, (T, TView) newItem, (T, TView) oldItem, int newStartingIndex, int oldStartingIndex, SortOperation<T> sortOperation)
    {
        Action = action;
        NewItem = newItem;
        OldItem = oldItem;
        NewStartingIndex = newStartingIndex;
        OldStartingIndex = oldStartingIndex;
        SortOperation = sortOperation;
    }
}

[StructLayout(LayoutKind.Auto)]
public readonly record struct RejectedViewChangedEvent
{
    public readonly RejectedViewChangedAction Action;
    public readonly int NewIndex;
    public readonly int OldIndex;

    public RejectedViewChangedEvent(RejectedViewChangedAction action, int newIndex, int oldIndex)
    {
        Action = action;
        NewIndex = newIndex;
        OldIndex = oldIndex;
    }
}

public static partial class ObservableCollectionR3Extensions
{
    public static Observable<RejectedViewChangedEvent> ObserveRejected<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewRejected<T, TView>(source, cancellationToken);
    }

    public static Observable<ViewChangedEvent<T, TView>> ObserveChanged<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewChanged<T, TView>(source, cancellationToken);
    }

    public static Observable<CollectionAddEvent<(T Value, TView View)>> ObserveAdd<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewAdd<T, TView>(source, cancellationToken);
    }

    public static Observable<CollectionRemoveEvent<(T Value, TView View)>> ObserveRemove<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewRemove<T, TView>(source, cancellationToken);
    }

    public static Observable<CollectionReplaceEvent<(T Value, TView View)>> ObserveReplace<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewReplace<T, TView>(source, cancellationToken);
    }

    public static Observable<CollectionMoveEvent<(T Value, TView View)>> ObserveMove<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewMove<T, TView>(source, cancellationToken);
    }

    public static Observable<CollectionResetEvent<T>> ObserveReset<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewReset<T, TView>(source, cancellationToken);
    }

    public static Observable<Unit> ObserveClear<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewClear<T, TView>(source, cancellationToken);
    }

    public static Observable<(int Index, int Count)> ObserveReverse<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewReverse<T, TView>(source, cancellationToken);
    }

    public static Observable<(int Index, int Count, IComparer<T>? Comparer)> ObserveSort<T, TView>(this ISynchronizedView<T, TView> source, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewSort<T, TView>(source, cancellationToken);
    }

    public static Observable<int> ObserveCountChanged<T, TView>(this ISynchronizedView<T, TView> source, bool notifyCurrentCount = false, CancellationToken cancellationToken = default)
    {
        return new SynchronizedViewCountChanged<T, TView>(source, notifyCurrentCount, cancellationToken);
    }
}

sealed class SynchronizedViewChanged<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<ViewChangedEvent<T, TView>>
{
    protected override IDisposable SubscribeCore(Observer<ViewChangedEvent<T, TView>> observer)
    {
        return new _SynchronizedViewChanged(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewChanged(
        ISynchronizedView<T, TView> source,
        Observer<ViewChangedEvent<T, TView>> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, ViewChangedEvent<T, TView>>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.IsSingleItem)
            {
                var newArgs = new ViewChangedEvent<T, TView>(
                    eventArgs.Action,
                    eventArgs.NewItem,
                    eventArgs.OldItem,
                    eventArgs.NewStartingIndex,
                    eventArgs.OldStartingIndex,
                    eventArgs.SortOperation);

                observer.OnNext(newArgs);
            }
            else
            {
                if (eventArgs.Action == NotifyCollectionChangedAction.Add)
                {
                    var index = eventArgs.NewStartingIndex;
                    for (int i = 0; i < eventArgs.NewValues.Length; i++)
                    {
                        var newItem = (eventArgs.NewValues[i], eventArgs.NewViews[i]);
                        var newArgs = new ViewChangedEvent<T, TView>(
                            eventArgs.Action,
                            newItem,
                            default,
                            index++,
                            eventArgs.OldStartingIndex,
                            eventArgs.SortOperation);

                        observer.OnNext(newArgs);
                    }
                }
                else if (eventArgs.Action == NotifyCollectionChangedAction.Remove)
                {

                    for (int i = 0; i < eventArgs.OldValues.Length; i++)
                    {
                        var oldItem = (eventArgs.OldValues[i], eventArgs.OldViews[i]);
                        var newArgs = new ViewChangedEvent<T, TView>(
                            eventArgs.Action,
                            default,
                            oldItem,
                            eventArgs.NewStartingIndex,
                            eventArgs.OldStartingIndex, // removed, uses same index
                            eventArgs.SortOperation);

                        observer.OnNext(newArgs);
                    }
                }
            }
        }
    }
}

sealed class SynchronizedViewAdd<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<CollectionAddEvent<(T, TView)>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionAddEvent<(T, TView)>> observer)
    {
        return new _SynchronizedViewAdd(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewAdd(
        ISynchronizedView<T, TView> source,
        Observer<CollectionAddEvent<(T, TView)>> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, CollectionAddEvent<(T, TView)>>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Add)
            {
                if (eventArgs.IsSingleItem)
                {
                    observer.OnNext(new CollectionAddEvent<(T, TView)>(eventArgs.NewStartingIndex, eventArgs.NewItem));
                }
                else
                {
                    var index = eventArgs.NewStartingIndex;
                    for (int i = 0; i < eventArgs.NewValues.Length; i++)
                    {
                        observer.OnNext(new CollectionAddEvent<(T, TView)>(index++, (eventArgs.NewValues[i], eventArgs.NewViews[i])));
                    }
                }
            }
        }
    }
}

sealed class SynchronizedViewRemove<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<CollectionRemoveEvent<(T, TView)>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionRemoveEvent<(T, TView)>> observer)
    {
        return new _SynchronizedViewRemove(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewRemove(
        ISynchronizedView<T, TView> source,
        Observer<CollectionRemoveEvent<(T, TView)>> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, CollectionRemoveEvent<(T, TView)>>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Remove)
            {
                if (eventArgs.IsSingleItem)
                {
                    observer.OnNext(new CollectionRemoveEvent<(T, TView)>(eventArgs.OldStartingIndex, eventArgs.OldItem));
                }
                else
                {
                    for (int i = 0; i < eventArgs.OldValues.Length; i++)
                    {
                        observer.OnNext(new CollectionRemoveEvent<(T, TView)>(eventArgs.OldStartingIndex, (eventArgs.OldValues[i], eventArgs.OldViews[i])));
                    }
                }
            }
        }
    }
}

sealed class SynchronizedViewReplace<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<CollectionReplaceEvent<(T, TView)>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionReplaceEvent<(T, TView)>> observer)
    {
        return new _SynchronizedViewReplace(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewReplace(
        ISynchronizedView<T, TView> source,
        Observer<CollectionReplaceEvent<(T, TView)>> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, CollectionReplaceEvent<(T, TView)>>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Replace)
            {
                observer.OnNext(new CollectionReplaceEvent<(T, TView)>(eventArgs.NewStartingIndex, eventArgs.OldItem, eventArgs.NewItem));
            }
        }
    }
}

sealed class SynchronizedViewMove<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<CollectionMoveEvent<(T, TView)>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionMoveEvent<(T, TView)>> observer)
    {
        return new _SynchronizedViewMove(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewMove(
        ISynchronizedView<T, TView> source,
        Observer<CollectionMoveEvent<(T, TView)>> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, CollectionMoveEvent<(T, TView)>>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Move)
            {
                observer.OnNext(new CollectionMoveEvent<(T, TView)>(eventArgs.OldStartingIndex, eventArgs.NewStartingIndex, eventArgs.NewItem));
            }
        }
    }
}

sealed class SynchronizedViewReset<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<CollectionResetEvent<T>>
{
    protected override IDisposable SubscribeCore(Observer<CollectionResetEvent<T>> observer)
    {
        return new _SynchronizedViewReset(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewReset(
        ISynchronizedView<T, TView> source,
        Observer<CollectionResetEvent<T>> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, CollectionResetEvent<T>>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset)
            {
                observer.OnNext(new CollectionResetEvent<T>(eventArgs.SortOperation));
            }
        }
    }
}

sealed class SynchronizedViewClear<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<Unit>
{
    protected override IDisposable SubscribeCore(Observer<Unit> observer)
    {
        return new _SynchronizedViewClear(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewClear(
        ISynchronizedView<T, TView> source,
        Observer<Unit> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, Unit>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset && eventArgs.SortOperation.IsClear)
            {
                observer.OnNext(Unit.Default);
            }
        }
    }
}

sealed class SynchronizedViewReverse<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<(int Index, int Count)>
{
    protected override IDisposable SubscribeCore(Observer<(int Index, int Count)> observer)
    {
        return new _SynchronizedViewReverse(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewReverse(
        ISynchronizedView<T, TView> source,
        Observer<(int Index, int Count)> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, (int Index, int Count)>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset && eventArgs.SortOperation.IsReverse)
            {
                observer.OnNext((eventArgs.SortOperation.Index, eventArgs.SortOperation.Count));
            }
        }
    }
}

sealed class SynchronizedViewSort<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<(int Index, int Count, IComparer<T>? Comparer)>
{
    protected override IDisposable SubscribeCore(Observer<(int Index, int Count, IComparer<T>? Comparer)> observer)
    {
        return new _SynchronizedViewSort(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewSort(
        ISynchronizedView<T, TView> source,
        Observer<(int Index, int Count, IComparer<T>? Comparer)> observer,
        CancellationToken cancellationToken)
        : SynchronizedViewObserverBase<T, TView, (int Index, int Count, IComparer<T>? Comparer)>(source, observer, cancellationToken)
    {
        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset && eventArgs.SortOperation.IsSort)
            {
                observer.OnNext(eventArgs.SortOperation.AsTuple());
            }
        }
    }
}

sealed class SynchronizedViewCountChanged<T, TView>(ISynchronizedView<T, TView> source, bool notifyCurrentCount, CancellationToken cancellationToken)
    : Observable<int>
{
    protected override IDisposable SubscribeCore(Observer<int> observer)
    {
        return new _SynchronizedViewCountChanged(source, notifyCurrentCount, observer, cancellationToken);
    }

    sealed class _SynchronizedViewCountChanged : SynchronizedViewObserverBase<T, TView, int>
    {
        int countPrev;

        public _SynchronizedViewCountChanged(
            ISynchronizedView<T, TView> source,
            bool notifyCurrentCount,
            Observer<int> observer,
            CancellationToken cancellationToken) : base(source, observer, cancellationToken)
        {
            this.countPrev = source.Count;
            if (notifyCurrentCount)
            {
                observer.OnNext(source.Count);
            }
        }

        protected override void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs)
        {
            switch (eventArgs.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Reset when countPrev != source.Count:
                    observer.OnNext(source.Count);
                    break;
            }
            countPrev = source.Count;
        }
    }
}



sealed class SynchronizedViewRejected<T, TView>(ISynchronizedView<T, TView> source, CancellationToken cancellationToken)
    : Observable<RejectedViewChangedEvent>
{
    protected override IDisposable SubscribeCore(Observer<RejectedViewChangedEvent> observer)
    {
        return new _SynchronizedViewRejected(source, observer, cancellationToken);
    }

    sealed class _SynchronizedViewRejected : IDisposable
    {
        readonly ISynchronizedView<T, TView> source;
        readonly Observer<RejectedViewChangedEvent> observer;
        readonly CancellationTokenRegistration cancellationTokenRegistration;
        readonly Action<RejectedViewChangedAction, int, int> handlerDelegate;

        public _SynchronizedViewRejected(ISynchronizedView<T, TView> source, Observer<RejectedViewChangedEvent> observer, CancellationToken cancellationToken)
        {
            this.source = source;
            this.observer = observer;
            this.handlerDelegate = Handler;

            source.RejectedViewChanged += handlerDelegate;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationTokenRegistration = cancellationToken.UnsafeRegister(static state =>
                {
                    var s = (_SynchronizedViewRejected)state!;
                    s.observer.OnCompleted();
                    s.Dispose();
                }, this);
            }
        }

        public void Dispose()
        {
            source.RejectedViewChanged -= handlerDelegate;
            cancellationTokenRegistration.Dispose();
        }

        void Handler(RejectedViewChangedAction rejectedViewChangedAction, int newIndex, int oldIndex)
        {
            observer.OnNext(new RejectedViewChangedEvent(rejectedViewChangedAction, newIndex, oldIndex));
        }
    }
}

abstract class SynchronizedViewObserverBase<T, TView, TEvent> : IDisposable
{
    protected readonly ISynchronizedView<T, TView> source;
    protected readonly Observer<TEvent> observer;
    readonly CancellationTokenRegistration cancellationTokenRegistration;
    readonly NotifyViewChangedEventHandler<T, TView> handlerDelegate;

    public SynchronizedViewObserverBase(ISynchronizedView<T, TView> source, Observer<TEvent> observer, CancellationToken cancellationToken)
    {
        this.source = source;
        this.observer = observer;
        this.handlerDelegate = Handler;

        source.ViewChanged += handlerDelegate;

        if (cancellationToken.CanBeCanceled)
        {
            cancellationTokenRegistration = cancellationToken.UnsafeRegister(static state =>
            {
                var s = (SynchronizedViewObserverBase<T, TView, TEvent>)state!;
                s.observer.OnCompleted();
                s.Dispose();
            }, this);
        }
    }

    public void Dispose()
    {
        source.ViewChanged -= handlerDelegate;
        cancellationTokenRegistration.Dispose();
    }

    protected abstract void Handler(in SynchronizedViewChangedEventArgs<T, TView> eventArgs);
}