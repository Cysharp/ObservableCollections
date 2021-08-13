using System.Collections.Generic;

namespace ObservableCollections
{
    public sealed partial class ObservableHashSet<T> : IReadOnlyCollection<T>, IObservableCollection<T>
    {
        // TODO:
        public ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer) where TKey : notnull
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<T, TView> CreateSortedView<TKey, TView>(Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer) where TKey : notnull
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            throw new NotImplementedException();
        }
    }
}
