using System.Collections;
using System.Collections.Specialized;

namespace ObservableCollections.Internal
{
    // mutable lookup.
    internal class Lookup<TKey, TValue> : ILookup<TKey, TValue>
        where TKey : notnull
    {
        Grouping<TKey, TValue>?[] groupingBuckets;
        IEqualityComparer<TKey> keyComparer;
        int count;
        Grouping<TKey, TValue>? lastGroup;

        // TODO:
        public int Count => throw new NotImplementedException();

        public int ItemsCount { get; private set; }

        public IEnumerable<TValue> this[TKey key] => throw new NotImplementedException();

        public Lookup(IEqualityComparer<TKey> keyComparer)
        {
            this.groupingBuckets = new Grouping<TKey, TValue>?[7]; // initial size
            this.count = 0;
            this.lastGroup = null;
            this.keyComparer = keyComparer;
        }

        // TODO:
        public bool Contains(TKey key)
        {
            throw new NotImplementedException();
        }

        public void Add(TKey key, TValue value)
        {
            var keyHash = keyComparer.GetHashCode(key);
            var g = groupingBuckets[keyHash % groupingBuckets.Length];
            var last = g;
            while (g != null)
            {
                if (keyComparer.Equals(key, g.key))
                {
                    break; // hit.
                }

                last = g;
                g = g.hashNext;
            }

            if (g == null)
            {
                g = new Grouping<TKey, TValue>(key, keyHash);
                if (last != null)
                {
                    last.hashNext = g;
                }
                else
                {
                    if (groupingBuckets.Length == count)
                    {
                        Resize();
                    }

                    groupingBuckets[keyHash % groupingBuckets.Length] = g;
                }
                count++; // new group added
            }

            g.Add(value);

            if (lastGroup == null)
            {
                lastGroup = g;
                lastGroup.nextGroup = g; // last's next is first.
            }
            else
            {
                g.nextGroup = lastGroup.nextGroup;
                lastGroup.nextGroup = g;
                lastGroup = g;
            }

            ItemsCount++;
        }

        public void RemoveKeyAll(TKey key)
        {
            throw new NotImplementedException();
        }

        public void RemoveValue(TKey key, TValue value)
        {
        }

        void Resize()
        {
            var newSize = checked((count * 2) + 1);
            var newGrouping = new Grouping<TKey, TValue>[newSize];

            var g = lastGroup!; // when resize called, always lastGroup is not null.
            do
            {
                g = g.nextGroup!; // nextGroup is always not null, initial last.next is first.
                var index = g.hash % newSize;
                g.hashNext = newGrouping[index];
                newGrouping[index] = g;
            }
            while (g != lastGroup);

            groupingBuckets = newGrouping;
        }

        public IEnumerator<IGrouping<TKey, TValue>> GetEnumerator()
        {
            if (lastGroup == null) yield break;

            var g = lastGroup.nextGroup; // last's next is first.
            do
            {
                if (g == null) yield break;
                yield return g;
                g = g.nextGroup;
            }
            while (g != lastGroup); // reaches end.
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal class Grouping<TKey, TValue> : IGrouping<TKey, TValue>
    {
        internal readonly TKey key;
        internal readonly int hash;
        internal readonly List<TValue> elements;

        internal Grouping<TKey, TValue>? hashNext;  // same buckets linknode
        internal Grouping<TKey, TValue>? nextGroup; // guarantee added order

        public TKey Key => key;

        public Grouping(TKey key, int hash)
        {
            this.key = key;
            this.hash = hash;
            this.elements = new List<TValue>(1); // initial size is single.
        }

        public void Add(TValue value)
        {
            elements.Add(value);
        }

        public void Remove(TValue value)
        {
            elements.Remove(value);
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            return elements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    internal class GroupedView<T, TKey, TView> : IGroupedSynchoronizedView<T, TKey, TView>
        where TKey : notnull
    {
        readonly Lookup<TKey, (T Value, TView View)> lookup;
        readonly IObservableCollection<T> source;
        readonly Func<T, TKey> keySelector;
        readonly Func<T, TView> viewSelector;

        ISynchronizedViewFilter<T, TView> filter;

        public GroupedView(IObservableCollection<T> source, Func<T, TKey> keySelector, Func<T, TView> viewSelector, IEqualityComparer<TKey> keyComparer)
        {
            this.source = source;
            this.filter = SynchronizedViewFilter<T, TView>.Null;
            this.keySelector = keySelector;
            this.viewSelector = viewSelector;

            lock (source.SyncRoot)
            {
                lookup = new Lookup<TKey, (T, TView)>(keyComparer);

                foreach (var value in source)
                {
                    var key = keySelector(value);
                    var view = viewSelector(value);
                    lookup.Add(key, (value, view));
                }

                source.CollectionChanged += SourceCollectionChanged;
            }
        }

        public IEnumerable<(T, TView)> this[TKey key]
        {
            get
            {
                lock (SyncRoot)
                {
                    var v = lookup[key];
                    foreach (var item in v)
                    {
                        yield return item;
                    }
                }
            }
        }

        public object SyncRoot { get; } = new object();

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return lookup.Count;
                }
            }
        }

        int IReadOnlyCollection<(T Value, TView View)>.Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return lookup.ItemsCount;
                }
            }
        }

        public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

        public void AttachFilter(ISynchronizedViewFilter<T, TView> filter)
        {
            lock (SyncRoot)
            {
                this.filter = filter;
                foreach (var item in lookup)
                {
                    foreach (var (value, view) in item)
                    {
                        filter.InvokeOnAttach(value, view);
                    }
                }
            }
        }

        public void ResetFilter(Action<T, TView>? resetAction)
        {
            lock (SyncRoot)
            {
                this.filter = SynchronizedViewFilter<T, TView>.Null;
                if (resetAction != null)
                {
                    foreach (var item in lookup)
                    {
                        foreach (var (value, view) in item)
                        {
                            resetAction(value, view);
                        }
                    }
                }
            }
        }

        public bool Contains(TKey key)
        {
            lock (SyncRoot)
            {
                return lookup.Contains(key);
            }
        }

        public void Dispose()
        {
            source.CollectionChanged -= SourceCollectionChanged;
        }

        public INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged()
        {
            lock (SyncRoot)
            {
                return new NotifyCollectionChangedSynchronizedView<T, TView>(this);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator<(T Value, TView View)> IEnumerable<(T Value, TView View)>.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<IGrouping<TKey, (T, TView)>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        private void SourceCollectionChanged(in NotifyCollectionChangedEventArgs<T> e)
        {
            lock (SyncRoot)
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.IsSingleItem)
                        {
                            var value = e.NewItem;
                            var key = keySelector(value);
                            var view = viewSelector(value);
                            lookup.Add(key, (value, view));
                            filter.InvokeOnAdd(value, view);
                        }
                        else
                        {
                            foreach (var value in e.NewItems)
                            {
                                var key = keySelector(value);
                                var view = viewSelector(value);
                                lookup.Add(key, (value, view));
                                filter.InvokeOnAdd(value, view);
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        if (e.IsSingleItem)
                        {
                            var value = e.OldItem;
                            var key = keySelector(value);

                            lookup

                            //var removeItems = lookup[key];
                            //foreach (var v in removeItems)
                            //{
                            //    filter.InvokeOnRemove(v);
                            //}

                            //lookup.Remove(key);
                            //filter.InvokeOnRemove(
                            //lookup
                            //dict.Remove((value, id), out var v);
                            //filter.InvokeOnRemove(v.Value, v.View);
                        }
                        else
                        {
                            //foreach (var value in e.OldItems)
                            //{
                            //    var id = identitySelector(value);
                            //    dict.Remove((value, id), out var v);
                            //    filter.InvokeOnRemove(v.Value, v.View);
                            //}
                        }

                        break;
                    case NotifyCollectionChangedAction.Replace:
                        break;
                    case NotifyCollectionChangedAction.Move:
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
