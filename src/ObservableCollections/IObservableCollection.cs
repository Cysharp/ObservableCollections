using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);
    public delegate void NotifyViewChangedEventHandler<T, TView>(in SynchronizedViewChangedEventArgs<T, TView> e);
    public delegate T WritableViewChangedEventHandler<T, TView>(TView newView, T originalValue, ref bool setValue);

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

    public enum RejectedViewChangedAction
    {
        Add, Remove, Move
    }

    public interface ISynchronizedView<T, TView> : IReadOnlyCollection<TView>, IDisposable
    {
        object SyncRoot { get; }
        ISynchronizedViewFilter<T> Filter { get; }
        IEnumerable<(T Value, TView View)> Filtered { get; }
        IEnumerable<(T Value, TView View)> Unfiltered { get; }
        int UnfilteredCount { get; }

        event NotifyViewChangedEventHandler<T, TView>? ViewChanged;
        event Action<RejectedViewChangedAction, int, int>? RejectedViewChanged;
        event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        void AttachFilter(ISynchronizedViewFilter<T> filter);
        void ResetFilter();
        ISynchronizedViewList<TView> ToViewList();
        INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged();
        INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher);
    }

    public interface IWritableSynchronizedView<T, TView> : ISynchronizedView<T, TView>
    {
        (T Value, TView View) GetAt(int index);
        void SetViewAt(int index, TView view);
        void SetToSourceCollection(int index, T value);
        void AddToSourceCollection(T value);
        IWritableSynchronizedViewList<TView> ToWritableViewList(WritableViewChangedEventHandler<T, TView> converter);
        INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter);
        INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher);
        INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter, ICollectionEventDispatcher? collectionEventDispatcher);
    }

    public interface ISynchronizedViewList<out TView> : IReadOnlyList<TView>, IDisposable
    {
    }

    public interface IWritableSynchronizedViewList<TView> : ISynchronizedViewList<TView>
    {
        new TView this[int index] { get; set; }
    }

    public interface INotifyCollectionChangedSynchronizedViewList<TView> : IList<TView>, IList, ISynchronizedViewList<TView>, INotifyCollectionChanged, INotifyPropertyChanged
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

        public static INotifyCollectionChangedSynchronizedViewList<T> ToNotifyCollectionChanged<T>(this IObservableCollection<T> collection)
        {
            return ToNotifyCollectionChanged(collection, null);
        }

        public static INotifyCollectionChangedSynchronizedViewList<T> ToNotifyCollectionChanged<T>(this IObservableCollection<T> collection, ICollectionEventDispatcher? collectionEventDispatcher)
        {
            return ToNotifyCollectionChanged(collection, static x => x, collectionEventDispatcher);
        }

        public static INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged<T, TView>(this IObservableCollection<T> collection, Func<T, TView> transform)
        {
            return ToNotifyCollectionChanged(collection, transform, null!);
        }

        public static INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged<T, TView>(this IObservableCollection<T> collection, Func<T, TView> transform, ICollectionEventDispatcher? collectionEventDispatcher)
        {
            // Optimized for non filtered
            return new NonFilteredNotifyCollectionChangedSynchronizedViewList<T, TView>(collection.CreateView(transform), collectionEventDispatcher);
        }
    }
}