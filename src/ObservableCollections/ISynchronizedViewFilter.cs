using System;

namespace ObservableCollections
{
    public interface ISynchronizedViewFilter<T, TView>
    {
        bool IsMatch(T value, TView view);
        void WhenTrue(T value, TView view);
        void WhenFalse(T value, TView view);
    }

    public class SynchronizedViewFilter<T, TView> : ISynchronizedViewFilter<T, TView>
    {
        public static readonly ISynchronizedViewFilter<T, TView> AlwaysTrue = new TrueViewFilter();

        readonly Func<T, TView, bool> isMatch;
        readonly Action<T, TView>? whenTrue;
        readonly Action<T, TView>? whenFalse;

        public SynchronizedViewFilter(Func<T, TView, bool> isMatch, Action<T, TView>? whenTrue, Action<T, TView>? whenFalse)
        {
            this.isMatch = isMatch;
            this.whenTrue = whenTrue;
            this.whenFalse = whenFalse;
        }

        public bool IsMatch(T value, TView view) => isMatch(value, view);
        public void WhenFalse(T value, TView view) => whenFalse?.Invoke(value, view);
        public void WhenTrue(T value, TView view) => whenTrue?.Invoke(value, view);

        class TrueViewFilter : ISynchronizedViewFilter<T, TView>
        {
            public bool IsMatch(T value, TView view) => true;
            public void WhenFalse(T value, TView view) { }
            public void WhenTrue(T value, TView view) { }
        }
    }

    public static class SynchronizedViewFilterExtensions
    {
        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> filter)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(filter, null, null));
        }

        public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, TView, bool> isMatch, Action<T, TView>? whenTrue, Action<T, TView>? whenFalse)
        {
            source.AttachFilter(new SynchronizedViewFilter<T, TView>(isMatch, whenTrue, whenFalse));
        }

        public static void Invoke<T, TView>(this ISynchronizedViewFilter<T, TView> filter, (T, TView) value)
        {
            Invoke(filter, value.Item1, value.Item2);
        }

        public static void Invoke<T, TView>(this ISynchronizedViewFilter<T, TView> filter, T value, TView view)
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