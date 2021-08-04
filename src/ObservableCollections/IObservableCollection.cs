using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections
{
    public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);

    public interface IObservableCollection<T>
    {
        event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
        ISynchronizedView<T, TView> CreateSortedView<TView>(Func<T, TView> transform, IComparer<T> comparer);
        ISynchronizedView<T, TView> CreateSortedView<TView>(Func<T, TView> transform, IComparer<TView> viewComparer);

        // TODO:Grouping
        // IGroupedSynchronizedView<T, TKey, TView> CreateGroupedView<TKey, TView>(Func<T, TKey> keySelector, Func<T, TView> transform);
    }

    internal interface INotifyCollectionChanged<T>
    {
        internal event NotifyCollectionChangedEventHandler<T>? CollectionChanged;
    }

    public interface IFreezedCollection<T>
    {
        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
        ISortableSynchronizedView<T, TView> CreateSortableView<TView>(Func<T, TView> transform);
    }

    public interface ISynchronizedView<T, TView> : IReadOnlyCollection<(T, TView)>, IDisposable
    {
        object SyncRoot { get; }
        event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
        event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
        void ResetFilter(Action<T, TView>? resetAction);

        INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged();
    }

    public interface ISortableSynchronizedView<T, TView> : ISynchronizedView<T, TView>
    {
        void Sort(IComparer<T> comparer);
        void Sort(IComparer<TView> viewComparer);
    }

    public interface INotifyCollectionChangedSynchronizedView<T, TView> : ISynchronizedView<T, TView>, INotifyCollectionChanged
    {
    }
}