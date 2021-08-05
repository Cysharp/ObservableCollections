using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);

    public interface IObservableCollection<T>
    {
        event NotifyCollectionChangedEventHandler<T>? CollectionChanged;

        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
        ISynchronizedView<T, TView> CreateSortedView<TView>(Func<T, TView> transform, IComparer<T> comparer);
        ISynchronizedView<T, TView> CreateSortedView<TView>(Func<T, TView> transform, IComparer<TView> viewComparer);
    }

    public interface IFreezedCollection<T>
    {
        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
        ISortableSynchronizedView<T, TView> CreateSortableView<TView>(Func<T, TView> transform);
    }

    public interface ISynchronizedView<T, TView> : IReadOnlyCollection<(T Value, TView View)>, IDisposable
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

    public interface INotifyCollectionChangedSynchronizedView<T, TView> : ISynchronizedView<T, TView>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }
}