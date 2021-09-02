using ObservableCollections.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ObservableCollections
{
    public sealed class FreezedList<T> : IReadOnlyList<T>, IFreezedCollection<T>
    {
        readonly IReadOnlyList<T> list;

        public T this[int index]
        {
            get
            {
                return list[index];
            }
        }

        public int Count
        {
            get
            {
                return list.Count;
            }
        }

        public bool IsReadOnly => true;

        public FreezedList(IReadOnlyList<T> list)
        {
            this.list = list;
        }

        public ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false)
        {
            return new FreezedView<T, TView>(list, transform, reverse);
        }

        public ISortableSynchronizedView<T, TView> CreateSortableView<TView>(Func<T, TView> transform)
        {
            return new FreezedSortableView<T, TView>(list, transform);
        }

        public bool Contains(T item)
        {
            return list.Contains(item);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}