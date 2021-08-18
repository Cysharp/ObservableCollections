using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections
{
    // TODO:Remove this???
    public sealed partial class ObservableLinkedList<T> : IReadOnlyCollection<T>, IObservableCollection<LinkedListNode<T>>
    {
        readonly LinkedList<T> list;
        public object SyncRoot { get; } = new object();

        public event NotifyCollectionChangedEventHandler<LinkedListNode<T>>? CollectionChanged;

        public ObservableLinkedList()
        {
            this.list = new LinkedList<T>();
        }

        public ObservableLinkedList(IEnumerable<T> collection)
        {
            this.list = new LinkedList<T>(collection);
        }

        // TODO: First, Last
        // Find, FindLast

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return list.Count;
                }
            }
        }

        public LinkedListNode<T> AddFirst(T item)
        {
            lock (SyncRoot)
            {
                var node = list.AddFirst(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<LinkedListNode<T>>.Add(node, 0));
                return node;
            }
        }

        public LinkedListNode<T> AddLast(T item)
        {
            lock (SyncRoot)
            {
                var index = list.Count;
                var node = list.AddLast(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<LinkedListNode<T>>.Add(node, index));
                return node;
            }
        }

        public LinkedListNode<T> AddBefore(LinkedListNode<T> node, T value)
        {
            lock (SyncRoot)
            {
                var newNode = list.AddBefore(node, value);

                // special event, oldItem is target, newStartingIndex:-1 = before
                var ev = new NotifyCollectionChangedEventArgs<LinkedListNode<T>>(
                    NotifyCollectionChangedAction.Add, isSingleItem: true,
                    newItem: newNode, oldItem: node, newStartingIndex: -1);

                CollectionChanged?.Invoke(ev);
                return newNode;
            }
        }

        public LinkedListNode<T> AddAfter(LinkedListNode<T> node, T value)
        {
            lock (SyncRoot)
            {
                var newNode = list.AddAfter(node, value);

                // special event, oldItem is target, newStartingIndex:1 = after
                var ev = new NotifyCollectionChangedEventArgs<LinkedListNode<T>>(
                    NotifyCollectionChangedAction.Add, isSingleItem: true,
                    newItem: newNode, oldItem: node, newStartingIndex: 1);

                CollectionChanged?.Invoke(ev);
                return newNode;
            }
        }

        public void RemoveLast()
        {
            lock (SyncRoot)
            {
                var last = list.Last;
                list.RemoveLast();
                if (last != null)
                {
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<LinkedListNode<T>>.Remove(last, -1));
                }
            }
        }

        public void RemoveFirst()
        {
            lock (SyncRoot)
            {
                var first = list.First;
                list.RemoveFirst();
                if (first != null)
                {
                    CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<LinkedListNode<T>>.Remove(first, -1));
                }
            }
        }

        public void Remove(LinkedListNode<T> item)
        {
            lock (SyncRoot)
            {
                list.Remove(item);
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<LinkedListNode<T>>.Remove(item, -1));
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                list.Clear();
                CollectionChanged?.Invoke(NotifyCollectionChangedEventArgs<LinkedListNode<T>>.Reset());
            }
        }

        public IEnumerator<LinkedListNode<T>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            throw new NotImplementedException();
        }



        // TODO: GetEnumerator

    }
}
