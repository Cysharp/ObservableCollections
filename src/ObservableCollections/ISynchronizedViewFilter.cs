using System;
using System.Collections.Specialized;

namespace ObservableCollections
{
    public readonly struct SynchronizedViewChangedEventArgs<T, TView>(
        NotifyViewChangedAction action,
        T newValue = default!,
        T oldValue = default!,
        TView newView = default!,
        TView oldView = default!,
        int newViewIndex = -1,
        int oldViewIndex = -1)
    {
        public readonly NotifyViewChangedAction Action = action;
        public readonly T NewValue = newValue;
        public readonly T OldValue = oldValue;
        public readonly TView NewView = newView;
        public readonly TView OldView = oldView;
        public readonly int NewViewIndex = newViewIndex;
        public readonly int OldViewIndex = oldViewIndex;
    }

    public enum NotifyViewChangedAction
    {
        Add = 0,
        Remove = 1,
        Replace = 2,
        Move = 3,
        Reset = 4,
        FilterReset = 5,
    }

    public interface ISynchronizedViewFilter<T>
    {
        bool IsMatch(T value);
    }

    public class SynchronizedViewFilter<T>(Func<T, bool> isMatch) : ISynchronizedViewFilter<T>
    {
        public static readonly ISynchronizedViewFilter<T> Null = new NullViewFilter();

        public bool IsMatch(T value) => isMatch(value);

        class NullViewFilter : ISynchronizedViewFilter<T>
        {
            public bool IsMatch(T value) => true;
        }
    }

    public static class SynchronizedViewFilterExtensions
    {
        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, bool> filter)
        {
            source.AttachFilter(new SynchronizedViewFilter<T>(filter));
        }

        public static bool IsNullFilter<T>(this ISynchronizedViewFilter<T> filter)
        {
            return filter == SynchronizedViewFilter<T>.Null;
        }

        internal static void InvokeOnAdd<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, (T value, TView view) value, int index)
        {
            InvokeOnAdd(collection, ref filteredCount, ev, value.value, value.view, index);
        }

        internal static void InvokeOnAdd<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, T value, TView view, int index)
        {
            var isMatch = collection.Filter.IsMatch(value);
            if (isMatch)
            {
                filteredCount++;
                if (ev != null)
                {
                    ev.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyViewChangedAction.Add, newValue: value, newView: view, newViewIndex: index));
                }
            }
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, (T value, TView view) value, int oldIndex)
        {
            InvokeOnRemove(collection, ref filteredCount, ev, value.value, value.view, oldIndex);
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, T value, TView view, int oldIndex)
        {
            var isMatch = collection.Filter.IsMatch(value);
            if (isMatch)
            {
                filteredCount--;
                if (ev != null)
                {
                    ev.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyViewChangedAction.Remove, oldValue: value, oldView: view, oldViewIndex: oldIndex));
                }
            }
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, (T value, TView view) value, int index, int oldIndex)
        {
            InvokeOnMove(collection, ref filteredCount, ev, value.value, value.view, index, oldIndex);
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, T value, TView view, int index, int oldIndex)
        {
            if (ev != null)
            {
                // move does not changes filtered-count
                var isMatch = collection.Filter.IsMatch(value);
                if (isMatch)
                {
                    ev.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyViewChangedAction.Move, newValue: value, newView: view, newViewIndex: index, oldViewIndex: oldIndex));
                }
            }
        }

        internal static void InvokeOnReplace<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, (T value, TView view) value, (T value, TView view) oldValue, int index, int oldIndex = -1)
        {
            InvokeOnReplace(collection, ref filteredCount, ev, value.value, value.view, oldValue.value, oldValue.view, index, oldIndex);
        }

        internal static void InvokeOnReplace<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev, T value, TView view, T oldValue, TView oldView, int index, int oldIndex = -1)
        {
            var oldMatched = collection.Filter.IsMatch(oldValue);
            var newMatched = collection.Filter.IsMatch(value);
            var bothMatched = oldMatched && newMatched;

            if (bothMatched)
            {
                if (ev != null)
                {
                    ev.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyViewChangedAction.Replace, newValue: value, newView: view, oldValue: oldValue, oldView: oldView, newViewIndex: index, oldViewIndex: oldIndex >= 0 ? oldIndex : index));
                }
            }
            else if (oldMatched)
            {
                // only-old is remove
                filteredCount--;
                if (ev != null)
                {
                    ev.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyViewChangedAction.Remove, oldValue: value, oldView: view, oldViewIndex: oldIndex));
                }

            }
            else if (newMatched)
            {
                // only-new is add
                filteredCount++;
                if (ev != null)
                {
                    ev.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyViewChangedAction.Add, newValue: value, newView: view, newViewIndex: index));
                }
            }
        }

        internal static void InvokeOnReset<T, TView>(this ISynchronizedView<T, TView> collection, ref int filteredCount, Action<SynchronizedViewChangedEventArgs<T, TView>>? ev)
        {
            filteredCount = 0;
            if (ev != null)
            {
                ev.Invoke(new SynchronizedViewChangedEventArgs<T, TView>(NotifyViewChangedAction.Reset));
            }
        }
    }
}
