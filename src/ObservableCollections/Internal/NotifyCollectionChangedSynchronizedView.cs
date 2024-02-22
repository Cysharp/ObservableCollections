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

        readonly ISynchronizedView<T, TView> parent;
        readonly ISynchronizedViewFilter<T, TView> currentFilter;

        public NotifyCollectionChangedSynchronizedView(ISynchronizedView<T, TView> parent)
        {
            this.parent = parent;
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

            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, args.NewView, args.NewViewIndex));
                    PropertyChanged?.Invoke(this, CountPropertyChangedEventArgs);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, args.OldView, args.OldViewIndex));
                    PropertyChanged?.Invoke(this, CountPropertyChangedEventArgs);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    PropertyChanged?.Invoke(this, CountPropertyChangedEventArgs);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, args.NewView, args.OldView, args.NewViewIndex));
                    break;
                case NotifyCollectionChangedAction.Move:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, args.NewView, args.NewViewIndex, args.OldViewIndex));
                    break;
            }
        }
    }
}