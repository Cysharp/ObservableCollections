using System;

namespace ObservableCollections
{
    public interface ISynchronizedViewFilter<T, TView>
    {
        bool IsMatch(T value, TView view);
        void WhenTrue(T value, TView view);
        void WhenFalse(T value, TView view);
        void OnCollectionChanged(ChangedKind changedKind, T value, TView view);
    }

    public enum ChangedKind
    {
        Add, Remove, Move
    }

    public class SynchronizedViewFilter<T, TView> : ISynchronizedViewFilter<T, TView>
    {
        public static readonly ISynchronizedViewFilter<T, TView> Null = new NullViewFilter();

        readonly Func<T, TView, bool> isMatch;
        readonly Action<T, TView> whenTrue;
        readonly Action<T, TView> whenFalse;
        readonly Action<ChangedKind, T, TView> onCollectionChanged;

        public SynchronizedViewFilter(Func<T, TView, bool> isMatch, Action<T, TView> whenTrue, Action<T, TView> whenFalse, Action<ChangedKind, T, TView> onCollectionChanged)
        {
            this.isMatch = isMatch;
            this.whenTrue = whenTrue;
            this.whenFalse = whenFalse;
            this.onCollectionChanged = onCollectionChanged;
        }

        public bool IsMatch(T value, TView view) => isMatch(value, view);
        public void WhenFalse(T value, TView view) => whenFalse?.Invoke(value, view);
        public void WhenTrue(T value, TView view) => whenTrue?.Invoke(value, view);
        public void OnCollectionChanged(ChangedKind changedKind, T value, TView view) => onCollectionChanged?.Invoke(changedKind, value, view);

        class NullViewFilter : ISynchronizedViewFilter<T, TView>
        {
            public bool IsMatch(T value, TView view) => true;
            public void WhenFalse(T value, TView view) { }
            public void WhenTrue(T value, TView view) { }
            public void OnCollectionChanged(ChangedKind changedKind, T value, TView view) { }
        }
    }

    public static class SynchronizedViewFilterExtensions
    {
        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> filter)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(filter, null, null, null));
        }

        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> isMatch, Action<T, TView> whenTrue, Action<T, TView> whenFalse)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(isMatch, whenTrue, whenFalse, null));
        }

        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> isMatch, Action<T, TView> whenTrue, Action<T, TView> whenFalse, Action<ChangedKind, T, TView> onCollectionChanged)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(isMatch, whenTrue, whenFalse, onCollectionChanged));
        }

        public static bool IsNullFilter<T, TView>(this ISynchronizedViewFilter<T, TView> filter)
        {
            return filter == SynchronizedViewFilter<T, TView>.Null;
        }

        internal static void InvokeOnAdd<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value)
        {
            InvokeOnAdd(filter, value.value, value.view);
        }

        internal static void InvokeOnAdd<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view)
        {
            if (filter.IsMatch(value, view))
            {
                filter.WhenTrue(value, view);
            }
            else
            {
                filter.WhenFalse(value, view);
            }
            filter.OnCollectionChanged(ChangedKind.Add, value, view);
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value)
        {
            InvokeOnRemove(filter, value.value, value.view);
        }

        internal static void InvokeOnRemove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view)
        {
            filter.OnCollectionChanged(ChangedKind.Remove, value, view);
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T value, TView view) value)
        {
            InvokeOnMove(filter, value.value, value.view);
        }

        internal static void InvokeOnMove<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view)
        {
            filter.OnCollectionChanged(ChangedKind.Move, value, view);
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