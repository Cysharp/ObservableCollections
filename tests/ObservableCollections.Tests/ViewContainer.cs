using System;
using System.Collections.Generic;

namespace ObservableCollections.Tests
{
    public struct ViewContainer<T> : IEquatable<ViewContainer<T>>, IComparable<ViewContainer<T>>
    {
        public ViewContainer(T value)
        {
            Value = value;
        }

        public T Value { get; }

        public static implicit operator ViewContainer<T>(T value) => new ViewContainer<T>(value);

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
}
