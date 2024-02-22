using System;
using System.Collections.Specialized;

namespace ObservableCollections
{
    public interface ISynchronizedViewFilter<T, TView>
    {
        bool IsMatch(T value, TView view);
        void WhenTrue(T value, TView view);
        void WhenFalse(T value, TView view);
        void OnCollectionChanged(NotifyCollectionChangedAction changedAction, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs);
    }

    public class SynchronizedViewFilter<T, TView> : ISynchronizedViewFilter<T, TView>
    {
        public static readonly ISynchronizedViewFilter<T, TView> Null = new NullViewFilter();

        readonly Func<T, TView, bool> isMatch;
        readonly Action<T, TView>? whenTrue;
        readonly Action<T, TView>? whenFalse;
        readonly Action<NotifyCollectionChangedAction, T, TView>? onCollectionChanged;

        public SynchronizedViewFilter(Func<T, TView, bool> isMatch, Action<T, TView>? whenTrue, Action<T, TView>? whenFalse, Action<NotifyCollectionChangedAction, T, TView>? onCollectionChanged)
        {
            this.isMatch = isMatch;
            this.whenTrue = whenTrue;
            this.whenFalse = whenFalse;
            this.onCollectionChanged = onCollectionChanged;
        }

        public bool IsMatch(T value, TView view) => isMatch(value, view);
        public void WhenFalse(T value, TView view) => whenFalse?.Invoke(value, view);
        public void WhenTrue(T value, TView view) => whenTrue?.Invoke(value, view);
        public void OnCollectionChanged(NotifyCollectionChangedAction changedAction, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs) => 
            onCollectionChanged?.Invoke(changedAction, value, view);

        class NullViewFilter : ISynchronizedViewFilter<T, TView>
        {
            public bool IsMatch(T value, TView view) => true;
            public void WhenFalse(T value, TView view) { }
            public void WhenTrue(T value, TView view) { }
            public void OnCollectionChanged(NotifyCollectionChangedAction changedAction, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs) { }
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

        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> isMatch, Action<T, TView>? whenTrue, Action<T, TView>? whenFalse, Action<NotifyCollectionChangedAction, T, TView>? onCollectionChanged)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(isMatch, whenTrue, whenFalse, onCollectionChanged));
        }

        public static bool IsNullFilter<T, TView>(this ISynchronizedViewFilter<T, TView> filter)
        {
            return filter == SynchronizedViewFilter<T, TView>.Null;
        }

        internal static void InvokeOnAdd<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            InvokeOnAdd(filter, value.value, value.view, eventArgs);
        }

        internal static void InvokeOnAdd<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            if (filter.IsMatch(value, view))
            {
                filter.WhenTrue(value, view);
            }
            else
            {
                filter.WhenFalse(value, view);
            }
            filter.OnCollectionChanged(NotifyCollectionChangedAction.Add, value, view, eventArgs);
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            InvokeOnRemove(filter, value.value, value.view, eventArgs);
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            filter.OnCollectionChanged(NotifyCollectionChangedAction.Remove, value, view, eventArgs);
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            InvokeOnMove(filter, value.value, value.view, eventArgs);
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            filter.OnCollectionChanged(NotifyCollectionChangedAction.Move, value, view, eventArgs);
        }
        
        internal static void InvokeOnReplace<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            InvokeOnReplace(filter, value.value, value.view, eventArgs);
        }

        internal static void InvokeOnReplace<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            filter.OnCollectionChanged(NotifyCollectionChangedAction.Replace, value, view, eventArgs);
        }

        internal static void InvokeOnReset<T, TView>(this ISynchronizedViewFilter<T, TView> filter, in NotifyCollectionChangedEventArgs<T> eventArgs)
        {
            filter.OnCollectionChanged(NotifyCollectionChangedAction.Reset, default!, default!, eventArgs);
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
    }
}