using System;
using System.Collections.Specialized;
using System.Runtime.InteropServices;

namespace ObservableCollections
{
    /// <summary>
    /// Contract:
    ///     IsSingleItem ? (NewItem, OldItem) : (NewItems, OldItems)
    ///     Action.Add
    ///         NewItem, NewItems, NewStartingIndex
    ///     Action.Remove
    ///         OldItem, OldItems, OldStartingIndex
    ///     Action.Replace
    ///         NewItem, NewItems, OldItem, OldItems, (NewStartingIndex, OldStartingIndex = samevalue)
    ///     Action.Move
    ///         NewStartingIndex, OldStartingIndex
    ///     Action.Reset
    ///         -
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct NotifyCollectionChangedEventArgs<T>
    {
        public readonly NotifyCollectionChangedAction Action;
        public readonly bool IsSingleItem;
        public readonly T NewItem;
        public readonly T OldItem;
        public readonly ReadOnlySpan<T> NewItems;
        public readonly ReadOnlySpan<T> OldItems;
        public readonly int NewStartingIndex;
        public readonly int OldStartingIndex;

        public NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction action, bool isSingleItem, T newItem = default, T oldItem = default, ReadOnlySpan<T> newItems = default, ReadOnlySpan<T> oldItems = default, int newStartingIndex = -1, int oldStartingIndex = -1)
        {
            Action = action;
            IsSingleItem = isSingleItem;
            NewItem = newItem;
            OldItem = oldItem;
            NewItems = newItems;
            OldItems = oldItems;
            NewStartingIndex = newStartingIndex;
            OldStartingIndex = oldStartingIndex;
        }

        public NotifyCollectionChangedEventArgs ToStandardEventArgs()
        {
            switch (Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (IsSingleItem)
                    {
                        return new NotifyCollectionChangedEventArgs(Action, NewItem, NewStartingIndex);
                    }
                    else
                    {
                        return new NotifyCollectionChangedEventArgs(Action, NewItems.ToArray(), NewStartingIndex);
                    }
                case NotifyCollectionChangedAction.Remove:
                    if (IsSingleItem)
                    {
                        return new NotifyCollectionChangedEventArgs(Action, OldItem, OldStartingIndex);
                    }
                    else
                    {
                        return new NotifyCollectionChangedEventArgs(Action, OldItems.ToArray(), OldStartingIndex);
                    }
                case NotifyCollectionChangedAction.Replace:
                    if (IsSingleItem)
                    {
                        return new NotifyCollectionChangedEventArgs(Action, NewItem, OldItem, NewStartingIndex);
                    }
                    else
                    {
                        return new NotifyCollectionChangedEventArgs(Action, NewItems.ToArray(), OldItems.ToArray(), NewStartingIndex);
                    }
                case NotifyCollectionChangedAction.Move:
                    {
                        return new NotifyCollectionChangedEventArgs(Action, OldItem, NewStartingIndex, OldStartingIndex);
                    }
                case NotifyCollectionChangedAction.Reset:
                    return new NotifyCollectionChangedEventArgs(Action);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static NotifyCollectionChangedEventArgs<T> Add(T newItem, int newStartingIndex)
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Add, true, newItem: newItem, newStartingIndex: newStartingIndex);
        }

        public static NotifyCollectionChangedEventArgs<T> Add(ReadOnlySpan<T> newItems, int newStartingIndex)
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Add, false, newItems: newItems, newStartingIndex: newStartingIndex);
        }

        public static NotifyCollectionChangedEventArgs<T> Remove(T oldItem, int oldStartingIndex)
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Remove, true, oldItem: oldItem, oldStartingIndex: oldStartingIndex);
        }

        public static NotifyCollectionChangedEventArgs<T> Remove(ReadOnlySpan<T> oldItems, int oldStartingIndex)
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Remove, false, oldItems: oldItems, oldStartingIndex: oldStartingIndex);
        }

        public static NotifyCollectionChangedEventArgs<T> Replace(T newItem, T oldItem, int startingIndex)
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Replace, true, newItem: newItem, oldItem: oldItem, newStartingIndex: startingIndex, oldStartingIndex: startingIndex);
        }

        public static NotifyCollectionChangedEventArgs<T> Replace(ReadOnlySpan<T> newItems, ReadOnlySpan<T> oldItems, int startingIndex)
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Replace, false, newItems: newItems, oldItems: oldItems, newStartingIndex: startingIndex, oldStartingIndex: startingIndex);
        }

        public static NotifyCollectionChangedEventArgs<T> Move(T changedItem, int newStartingIndex, int oldStartingIndex)
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Move, true, oldItem: changedItem, newItem: changedItem, newStartingIndex: newStartingIndex, oldStartingIndex: oldStartingIndex);
        }

        public static NotifyCollectionChangedEventArgs<T> Reset()
        {
            return new NotifyCollectionChangedEventArgs<T>(NotifyCollectionChangedAction.Reset, true);
        }
    }
}