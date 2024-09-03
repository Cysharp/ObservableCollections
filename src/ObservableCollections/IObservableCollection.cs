using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);
    public delegate void NotifyViewChangedEventHandler<T, TView>(in SynchronizedViewChangedEventArgs<T, TView> e);

    public interface IObservableCollection<T> : IReadOnlyCollection<T>
    {
        event NotifyCollectionChangedEventHandler<T>? CollectionChanged;
        object SyncRoot { get; }
        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform);
    }

    public interface IReadOnlyObservableList<T> :
        IReadOnlyList<T>, IObservableCollection<T>
    {
    }

    public interface IReadOnlyObservableDictionary<TKey, TValue> :
        IReadOnlyDictionary<TKey, TValue>, IObservableCollection<KeyValuePair<TKey, TValue>>
    {
    }

    public interface ISynchronizedView<T, TView> : IReadOnlyCollection<TView>, IDisposable
    {
        object SyncRoot { get; }
        ISynchronizedViewFilter<T> Filter { get; }
        IEnumerable<(T Value, TView View)> Filtered { get; }
        IEnumerable<(T Value, TView View)> Unfiltered { get; }
        int UnfilteredCount { get; }

        event NotifyViewChangedEventHandler<T, TView>? ViewChanged;
        event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        void AttachFilter(ISynchronizedViewFilter<T> filter);
        void ResetFilter();
        ISynchronizedViewList<TView> ToViewList();
        INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged();
        INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher);
    }

    public interface ISynchronizedViewList<out TView> : IReadOnlyList<TView>, IDisposable
    {
    }

    public interface INotifyCollectionChangedSynchronizedView<out TView> : IReadOnlyCollection<TView>, INotifyCollectionChanged, INotifyPropertyChanged, IDisposable
    {
    }

    public static class ObservableCollectionExtensions
    {
        public static ISynchronizedViewList<T> ToViewList<T>(this IObservableCollection<T> collection)
        {
            return ToViewList(collection, static x => x);
        }

        public static ISynchronizedViewList<TView> ToViewList<T, TView>(this IObservableCollection<T> collection, Func<T, TView> transform)
        {
            // Optimized for non filtered
            return new NonFilteredSynchronizedViewList<T, TView>(collection.CreateView(transform));
        }

        public static INotifyCollectionChangedSynchronizedView<T> ToNotifyCollectionChanged<T>(this IObservableCollection<T> collection)
        {
            return ToNotifyCollectionChanged(collection, null);
        }

        public static INotifyCollectionChangedSynchronizedView<T> ToNotifyCollectionChanged<T>(this IObservableCollection<T> collection, ICollectionEventDispatcher? collectionEventDispatcher)
        {
            return ToNotifyCollectionChanged(collection, static x => x, collectionEventDispatcher);
        }

        public static INotifyCollectionChangedSynchronizedView<TView> ToNotifyCollectionChanged<T, TView>(this IObservableCollection<T> collection, Func<T, TView> transform, ICollectionEventDispatcher? collectionEventDispatcher)
        {
            // Optimized for non filtered
            return new NonFilteredNotifyCollectionChangedSynchronizedView<T, TView>(collection.CreateView(transform), collectionEventDispatcher);
        }
    }
}