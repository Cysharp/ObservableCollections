using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections
{
    public sealed partial class ObservableLinkedList<T> : IReadOnlyCollection<T>, IObservableCollection<LinkedListNode<T>>
    {
        public ISynchronizedView<LinkedListNode<T>, TView> CreateView<TView>(Func<LinkedListNode<T>, TView> transform, bool reverse = false)
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<LinkedListNode<T>, TView> CreateSortedView<TKey, TView>(Func<LinkedListNode<T>, TKey> identitySelector, Func<LinkedListNode<T>, TView> transform, IComparer<LinkedListNode<T>> comparer) where TKey : notnull
        {
            throw new NotImplementedException();
        }

        public ISynchronizedView<LinkedListNode<T>, TView> CreateSortedView<TKey, TView>(Func<LinkedListNode<T>, TKey> identitySelector, Func<LinkedListNode<T>, TView> transform, IComparer<TView> viewComparer) where TKey : notnull
        {
            throw new NotImplementedException();
        }

        sealed class View<TView> : ISynchronizedView<LinkedListNode<T>, TView>
        {
            readonly ObservableLinkedList<T> source;
            readonly LinkedList<(LinkedListNode<T>, TView)> list;
            readonly Func<LinkedListNode<T>, TView> selector;

            public View(ObservableLinkedList<T> source)
            {
                this.source = source;

                lock (source.SyncRoot)
                {
                    // TODO:get map
                    source.CollectionChanged += SourceCollectionChanged;
                }
            }


            public object SyncRoot => throw new NotImplementedException();
            public event NotifyCollectionChangedEventHandler<LinkedListNode<T>>? RoutingCollectionChanged;
            public event Action<NotifyCollectionChangedAction>? CollectionStateChanged;


            public int Count => throw new NotImplementedException();


            public void AttachFilter(ISynchronizedViewFilter<LinkedListNode<T>, TView> filter)
            {
                throw new NotImplementedException();
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public IEnumerator<(LinkedListNode<T> Value, TView View)> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public void ResetFilter(Action<LinkedListNode<T>, TView>? resetAction)
            {
                throw new NotImplementedException();
            }

            public INotifyCollectionChangedSynchronizedView<LinkedListNode<T>, TView> WithINotifyCollectionChanged()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }


            private void SourceCollectionChanged(in NotifyCollectionChangedEventArgs<LinkedListNode<T>> e)
            {
                lock (SyncRoot)
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            {
                                // AddAfter
                                if (e.OldItem != null)
                                {
                                }

                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
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
}
