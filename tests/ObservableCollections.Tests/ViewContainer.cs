using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Tests
{
    public struct ViewContainer<T> : IEquatable<ViewContainer<T>>, IComparable<ViewContainer<T>>
    {
        public ViewContainer(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public static implicit operator ViewContainer<T>(T value) => new(value);

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public int CompareTo(ViewContainer<T> other)
        {
            return Comparer<T>.Default.Compare(Value, other.Value);
        }

        public bool Equals(ViewContainer<T> other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other.Value);
        }
    }

    //public class TestFilter<T> : ISynchronizedViewFilter<T>
    //{
    //    readonly Func<T, bool> filter;
    //    public List<SynchronizedViewChangedEventArgs<T, ViewContainer<T>>> CalledOnCollectionChanged = new();

    //    public TestFilter(Func<T, bool> filter)
    //    {
    //        this.filter = filter;
    //    }

    //    public void Clear()
    //    {
    //        CalledWhenTrue.Clear();
    //        CalledWhenFalse.Clear();
    //        CalledOnCollectionChanged.Clear();
    //    }

    //    public bool IsMatch(T value)
    //    {
    //        return this.filter.Invoke(value);
    //    }

    //    public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, ViewContainer<T>> args)
    //    {
    //        CalledOnCollectionChanged.Add(args);
    //    }

    //    public void WhenTrue(T value, ViewContainer<T> view)
    //    {
    //        CalledWhenTrue.Add((value, view));
    //    }

    //    public void WhenFalse(T value, ViewContainer<T> view)
    //    {
    //        CalledWhenFalse.Add((value, view));
    //    }
    //}

    //public class TestFilter2<T> : ISynchronizedViewFilter<KeyValuePair<T, T>, ViewContainer<T>>
    //{
    //    readonly Func<KeyValuePair<T, T>, ViewContainer<T>, bool> filter;
    //    public List<(KeyValuePair<T, T>, ViewContainer<T>)> CalledWhenTrue = new();
    //    public List<(KeyValuePair<T, T>, ViewContainer<T>)> CalledWhenFalse = new();
    //    public List<SynchronizedViewChangedEventArgs<KeyValuePair<T, T>, ViewContainer<T>>> CalledOnCollectionChanged = new();

    //    public TestFilter2(Func<KeyValuePair<T, T>, ViewContainer<T>, bool> filter)
    //    {
    //        this.filter = filter;
    //    }

    //    public void Clear()
    //    {
    //        CalledWhenTrue.Clear();
    //        CalledWhenFalse.Clear();
    //        CalledOnCollectionChanged.Clear();
    //    }

    //    public bool IsMatch(KeyValuePair<T, T> value, ViewContainer<T> view)
    //    {
    //        return this.filter.Invoke(value, view);
    //    }

    //    public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<KeyValuePair<T, T>, ViewContainer<T>> args)
    //    {
    //        CalledOnCollectionChanged.Add(args);
    //    }

    //    public void WhenTrue(KeyValuePair<T, T> value, ViewContainer<T> view)
    //    {
    //        CalledWhenTrue.Add((value, view));
    //    }

    //    public void WhenFalse(KeyValuePair<T, T> value, ViewContainer<T> view)
    //    {
    //        CalledWhenFalse.Add((value, view));
    //    }
    //}
}
