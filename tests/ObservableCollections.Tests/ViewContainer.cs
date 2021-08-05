using System;
using System.Collections.Generic;

namespace ObservableCollections.Tests
{
    public struct ViewContainer<T> : IEquatable<T>
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

        public bool Equals(T other)
        {
            return EqualityComparer<T>.Default.Equals(Value, other);
        }
    }
}
