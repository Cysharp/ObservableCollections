using System;
using System.Collections.Specialized;

namespace ObservableCollections
{
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

    
}
