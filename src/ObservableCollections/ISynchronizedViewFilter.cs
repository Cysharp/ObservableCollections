using System;
using System.Collections.Specialized;

namespace ObservableCollections
{
    public readonly struct SynchronizedViewChangedEventArgs<T, TView>(
        NotifyCollectionChangedAction action,
        T newValue = default!,
        T oldValue = default!,
        TView newView = default!,
        TView oldView = default!,
        int newViewIndex = -1,
        int oldViewIndex = -1)
    {
        public readonly NotifyCollectionChangedAction Action = action;
        public readonly T NewValue = newValue;
        public readonly T OldValue = oldValue;
        public readonly TView NewView = newView;
        public readonly TView OldView = oldView;
        public readonly int NewViewIndex = newViewIndex;
        public readonly int OldViewIndex = oldViewIndex;
    }

    public interface ISynchronizedViewFilter<T, TView>
    {
        bool IsMatch(T value, TView view);
        void WhenTrue(T value, TView view);
        void WhenFalse(T value, TView view);
        void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> eventArgs);
    }

    public class SynchronizedViewFilter<T, TView>(
        Func<T, TView, bool> isMatch,
        Action<T, TView>? whenTrue,
        Action<T, TView>? whenFalse,
        Action<SynchronizedViewChangedEventArgs<T, TView>>? onCollectionChanged)
        : ISynchronizedViewFilter<T, TView>
    {
        public static readonly ISynchronizedViewFilter<T, TView> Null = new NullViewFilter();

        public bool IsMatch(T value, TView view) => isMatch(value, view);
        public void WhenFalse(T value, TView view) => whenFalse?.Invoke(value, view);
        public void WhenTrue(T value, TView view) => whenTrue?.Invoke(value, view);
        public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> eventArgs) => onCollectionChanged?.Invoke(eventArgs);

        class NullViewFilter : ISynchronizedViewFilter<T, TView>
        {
            public bool IsMatch(T value, TView view) => true;
            public void WhenFalse(T value, TView view) { }
            public void WhenTrue(T value, TView view) { }
            public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> eventArgs) { }
        }
    }

    public static class SynchronizedViewFilterExtensions
    {
        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> filter)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(filter, null, null, null));
        }

        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> isMatch, Action<T, TView>? whenTrue, Action<T, TView>? whenFalse)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(isMatch, whenTrue, whenFalse, null));
        }

        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> isMatch, Action<T, TView>? whenTrue, Action<T, TView>? whenFalse, Action<SynchronizedViewChangedEventArgs<T, TView>>? onCollectionChanged)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(isMatch, whenTrue, whenFalse, onCollectionChanged));
        }

        public static bool IsNullFilter<T, TView>(this ISynchronizedViewFilter<T, TView> filter)
        {
            return filter == SynchronizedViewFilter<T, TView>.Null;
        }


        internal static void InvokeOnAdd<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, int index)
        {
            filter.InvokeOnAdd(value.value, value.view, index);
        }

        internal static void InvokeOnAdd<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, int index)
        {
            if (filter.IsMatch(value, view))
            {
                filter.WhenTrue(value, view);
            }
            else
            {
                filter.WhenFalse(value, view);
            }
            filter.OnCollectionChanged(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Add, newValue: value, newView: view, newViewIndex: index));
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, int oldIndex)
        {
            filter.InvokeOnRemove(value.value, value.view, oldIndex);
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, int oldIndex)
        {
            filter.OnCollectionChanged(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Remove, oldValue: value, oldView: view, oldViewIndex: oldIndex));
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, int index, int oldIndex)
        {
            InvokeOnMove(filter, value.value, value.view, index, oldIndex);
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, int index, int oldIndex)
        {
            filter.OnCollectionChanged(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Move, newValue: value, newView: view, newViewIndex: index, oldViewIndex: oldIndex));
        }

        internal static void InvokeOnReplace<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, (T value, TView view) oldValue, int index, int oldIndex = -1)
        {
            filter.InvokeOnReplace(value.value, value.view, oldValue.value, oldValue.view, index, oldIndex);
        }

        internal static void InvokeOnReplace<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, T oldValue, TView oldView, int index, int oldIndex = -1)
        {
            filter.OnCollectionChanged(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Replace, newValue: value, newView: view, oldValue: oldValue, oldView: oldView, newViewIndex: index, oldViewIndex: oldIndex >= 0 ? oldIndex : index));
        }

        internal static void InvokeOnReset<T, TView>(this ISynchronizedViewFilter<T, TView> filter)
        {
            filter.OnCollectionChanged(new SynchronizedViewChangedEventArgs<T, TView>(NotifyCollectionChangedAction.Reset));
        }

        internal static void InvokeOnAttach<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view)
        {
            if (filter.IsMatch(value, view))
            {
                filter.WhenTrue(value, view);
            }
            else
            {
                filter.WhenFalse(value, view);
            }
        }

        internal static bool IsMatch<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T, TView) value)
        {
            return filter.IsMatch(value);
        }
    }
}
