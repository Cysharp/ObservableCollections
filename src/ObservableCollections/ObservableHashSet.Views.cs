using System.Collections.Generic;

namespace ObservableCollections
{
    public sealed partial class ObservableHashSet<T> : IReadOnlyCollection<T>, IObservableCollection<T>
    {
        // TODO:

        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            throw new NotImplementedException();
        }
    }
}
