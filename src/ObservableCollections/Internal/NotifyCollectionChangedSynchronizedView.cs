using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ObservableCollections.Internal
{
    internal class NotifyCollectionChangedSynchronizedView<T, TView> :
        INotifyCollectionChangedSynchronizedView<TView>,
        ISynchronizedViewFilter<T, TView>
    {
        static readonly PropertyChangedEventArgs CountPropertyChangedEventArgs = new("Count");
        static readonly Action<NotifyCollectionChangedEventArgs> raiseChangedEventInvoke = RaiseChangedEvent;

        readonly ISynchronizedView<T, TView> parent;
        readonly ISynchronizedViewFilter<T, TView> currentFilter;
        readonly ICollectionEventDispatcher eventDispatcher;

        public NotifyCollectionChangedSynchronizedView(ISynchronizedView<T, TView> parent, ICollectionEventDispatcher? eventDispatcher)
        {
            this.parent = parent;
            this.eventDispatcher = eventDispatcher ?? DirectCollectionEventDispatcher.Instance;
            currentFilter = parent.CurrentFilter;
            parent.AttachFilter(this);
        }

        public int Count => parent.Count;

        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        public event Action<NotifyCollectionChangedAction>? CollectionStateChanged
        {
            add { parent.CollectionStateChanged += value; }
            remove { parent.CollectionStateChanged -= value; }
        }

        public event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged
        {
            add { parent.RoutingCollectionChanged += value; }
            remove { parent.RoutingCollectionChanged -= value; }
        }

        public void Dispose()
        {
            parent.Dispose();
        }

        public IEnumerator<TView> GetEnumerator()
        {
            foreach (var (value, view) in parent)
            {
                yield return view;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsMatch(T value, TView view) => currentFilter.IsMatch(value, view);
        public void WhenTrue(T value, TView view) => currentFilter.WhenTrue(value, view);
        public void WhenFalse(T value, TView view) => currentFilter.WhenFalse(value, view);

        public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> args)
        {
            currentFilter.OnCollectionChanged(args);

            if (CollectionChanged == null && PropertyChanged == null) return;

            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Add, args.NewView, args.NewViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                    break;
                case NotifyCollectionChangedAction.Remove:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Remove, args.OldView, args.OldViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                    break;
                case NotifyCollectionChangedAction.Reset:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Reset)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = true
                    });
                    break;
                case NotifyCollectionChangedAction.Replace:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Replace, args.NewView, args.OldView, args.NewViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = false
                    });
                    break;
                case NotifyCollectionChangedAction.Move:
                    eventDispatcher.Post(new CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction.Move, args.NewView, args.NewViewIndex, args.OldViewIndex)
                    {
                        Collection = this,
                        Invoker = raiseChangedEventInvoke,
                        IsInvokeCollectionChanged = true,
                        IsInvokePropertyChanged = false
                    });
                    break;
            }
        }

        static void RaiseChangedEvent(NotifyCollectionChangedEventArgs e)
        {
            var e2 = (CollectionEventDispatcherEventArgs)e;
            var self = (NotifyCollectionChangedSynchronizedView<T, TView>)e2.Collection;

            if (e2.IsInvokeCollectionChanged)
            {
                self.CollectionChanged?.Invoke(self, e);
            }
            if (e2.IsInvokePropertyChanged)
            {
                self.PropertyChanged?.Invoke(self, CountPropertyChangedEventArgs);
            }
        }
    }
}