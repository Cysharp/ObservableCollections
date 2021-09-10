using ObservableCollections.Internal;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections
{
    public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);

    public interface IObservableCollection<T> : IReadOnlyCollection<T>
    {
        event NotifyCollectionChangedEventHandler<T> CollectionChanged;
        object SyncRoot { get; }
        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
    }

    public interface IFreezedCollection<T>
    {
        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
        ISortableSynchronizedView<T, TView> CreateSortableView<TView>(Func<T, TView> transform);
    }

    public interface ISynchronizedView<T, TView> : IReadOnlyCollection<(T Value, TView View)>, IDisposable
    {
        object SyncRoot { get; }

        event NotifyCollectionChangedEventHandler<T> RoutingCollectionChanged;
        event Action<NotifyCollectionChangedAction> CollectionStateChanged;

        void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
        void ResetFilter(Action<T, TView> resetAction);
        INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged();
    }

    public interface ISortableSynchronizedView<T, TView> : ISynchronizedView<T, TView>
    {
        void Sort(IComparer<T> comparer);
        void Sort(IComparer<TView> viewComparer);
    }

    // will be implemented in the future?
    //public interface IGroupedSynchoronizedView<T, TKey, TView> : ILookup<TKey, (T, TView)>, ISynchronizedView<T, TView>
    //{
    //}

    public interface INotifyCollectionChangedSynchronizedView<T, TView> : ISynchronizedView<T, TView>, INotifyCollectionChanged, INotifyPropertyChanged
    {
    }

    public static class ObservableCollectionsExtensions
    {
        public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
            
        {
            return new SortedView<T, TKey, TView>(source, identitySelector, transform, comparer);
        }

        public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer)
            
        {
            return new SortedViewViewComparer<T, TKey, TView>(source, identitySelector, transform, viewComparer);
        }

        public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView, TCompare>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, Func<T, TCompare> compareSelector, bool ascending = true)
            
        {
            return source.CreateSortedView(identitySelector, transform, new AnonymousComparer<T, TCompare>(compareSelector, ascending));
        }

        public static ISortableSynchronizedView<T, TView> CreateSortableView<T, TView>(this IFreezedCollection<T> source, Func<T, TView> transform, IComparer<T> initialSort)
        {
            var view = source.CreateSortableView(transform);
            view.Sort(initialSort);
            return view;
        }

        public static ISortableSynchronizedView<T, TView> CreateSortableView<T, TView>(this IFreezedCollection<T> source, Func<T, TView> transform, IComparer<TView> initialViewSort)
        {
            var view = source.CreateSortableView(transform);
            view.Sort(initialViewSort);
            return view;
        }

        public static ISortableSynchronizedView<T, TView> CreateSortableView<T, TView, TCompare>(this IFreezedCollection<T> source, Func<T, TView> transform, Func<T, TCompare> initialCompareSelector, bool ascending = true)
        {
            var view = source.CreateSortableView(transform);
            view.Sort(initialCompareSelector, ascending);
            return view;
        }

        public static void Sort<T, TView, TCompare>(this ISortableSynchronizedView<T, TView> source, Func<T, TCompare> compareSelector, bool ascending = true)
        {
            source.Sort(new AnonymousComparer<T, TCompare>(compareSelector, ascending));
        }

        class AnonymousComparer<T, TCompare> : IComparer<T>
        {
            readonly Func<T, TCompare> selector;
            readonly int f;

            public AnonymousComparer(Func<T, TCompare> selector, bool ascending)
            {
                this.selector = selector;
                this.f = ascending ? 1 : -1;
            }

            public int Compare(T x, T y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return 1 * f;
                if (y == null) return -1 * f;

                return Comparer<TCompare>.Default.Compare(selector(x), selector(y)) * f;
            }
        }
    }
}