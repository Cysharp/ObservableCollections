using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections
{
    public sealed partial class ObservableLinkedList<T>
    {
        public ISynchronizedView<LinkedListNode<T>, TView> CreateView<TView>(Func<LinkedListNode<T>, TView> transform, bool reverse = false)
        {
            throw new NotImplementedException();
        }

        sealed class View<TView> : ISynchronizedView<LinkedListNode<T>, TView>
        {
            readonly ObservableLinkedList<T> source;
            readonly Dictionary<LinkedListNode<T>, LinkedListNode<(LinkedListNode<T>, TView)>> nodeMap;
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
                    // Range operations is not supported.
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            {
                                var view = selector(e.NewItem);
                                var value = (e.NewItem, view);
                                LinkedListNode<(LinkedListNode<T>, TView)>? addNode = null;

                                if (e.OldItem == null)
                                {
                                    // AddFirst
                                    if (e.NewStartingIndex == 0)
                                    {
                                        addNode = list.AddFirst(value);
                                    }
                                    // AddLast
                                    else
                                    {
                                        addNode = list.AddLast(value);
                                    }
                                }
                                else
                                {
                                    // AddBefore
                                    if (e.NewStartingIndex == -1)
                                    {
                                        if (nodeMap.TryGetValue(e.OldItem, out var node))
                                        {
                                            addNode = list.AddBefore(node, value);
                                        }
                                    }
                                    // AddAfter
                                    else
                                    {
                                        if (nodeMap.TryGetValue(e.OldItem, out var node))
                                        {
                                            addNode = list.AddAfter(node, value);
                                        }
                                    }
                                }

                                if (addNode != null)
                                {
                                    nodeMap.Add(e.NewItem, addNode);
                                    // TODO: filter invoke.
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Remove:
                            {
                                if (nodeMap.Remove(e.OldItem, out var node))
                                {
                                    list.Remove(node);
                                    // TODO:filter invoke
                                }
                            }
                            break;
                        case NotifyCollectionChangedAction.Reset:
                            {
                                nodeMap.Clear();
                                list.Clear();

                                // TODO:filter invoke
                            }
                            break;
                        case NotifyCollectionChangedAction.Replace:
                        case NotifyCollectionChangedAction.Move:
                        default:
                            break;
                    }
                }
            }
        }
    }
}