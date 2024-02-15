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

        public NotifyCollectionChangedSynchronizedView(ISynchronizedView<T, TView> parent)
        {
            this.parent = parent;
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

        IEnumerator IEnumerable.GetEnumerator() => parent.GetEnumerator();

        public bool IsMatch(T value, TView view) => parent.CurrentFilter.IsMatch(value, view);
        public void WhenTrue(T value, TView view) => parent.CurrentFilter.WhenTrue(value, view);
        public void WhenFalse(T value, TView view) => parent.CurrentFilter.WhenFalse(value, view);

        public void OnCollectionChanged(ChangedKind changedKind, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            parent.CurrentFilter.OnCollectionChanged(changedKind, value, view, in eventArgs);

            switch (changedKind)
            {
                case ChangedKind.Add:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, view, eventArgs.NewStartingIndex));
                    return;
                case ChangedKind.Remove:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, view, eventArgs.OldStartingIndex));
                    break;
                case ChangedKind.Move:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, view, eventArgs.NewStartingIndex, eventArgs.OldStartingIndex));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(changedKind), changedKind, null);
            }
        }
    }
}