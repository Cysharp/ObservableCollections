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
        ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
            where TKey : notnull;
        ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer)
            where TKey : notnull;
    }

    public interface IFreezedCollection<T>
    {
        ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
        ISortableSynchronizedView<T, TView> CreateSortableView<TView>(Func<T, TView> transform);
    }

    public interface ISynchronizedView<T, TView> : IReadOnlyCollection<(T Value, TView View)>, IDisposable
    {
        // TODO:Remove SyncRoot
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

    public static class ObservableCollectionsExtensions
    {
        public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView, TCompare>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, Func<T, TCompare> compareSelector, bool ascending = true)
            where TKey : notnull
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

            public int Compare(T? x, T? y)
            {
                if (x == null && y == null) return 0;
                if (x == null) return 1 * f;
                if (y == null) return -1 * f;

                return Comparer<TCompare>.Default.Compare(selector(x), selector(y)) * f;
            }
        }
    }
}