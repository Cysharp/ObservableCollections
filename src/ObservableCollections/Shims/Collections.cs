#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Runtime.InteropServices;

namespace System.Collections.Generic
{
    internal static class CollectionExtensions
    {
        const int ArrayMaxLength = 0X7FFFFFC7;

        public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp, out TKey key, out TValue value)
        {
            key = kvp.Key;
            value = kvp.Value;
        }

        public static bool Remove<TKey, TValue>(this SortedDictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out value))
            {
                return dict.Remove(key);
            }
            return false;
        }

        public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, out TValue value)
        {
            if (dict.TryGetValue(key, out value))
            {
                return dict.Remove(key);
            }
            return false;
        }

#if !NET8_0_OR_GREATER
#pragma warning disable CS0436

        // CollectionExtensions.AddRange
        public static void AddRange<T>(this List<T> list, ReadOnlySpan<T> source)
        {
            if (!source.IsEmpty)
            {
                if (!CollectionsMarshal.IsLegacyList)
                {
                    ref var view = ref Unsafe.As<List<T>, CollectionsMarshal.ListView<T>>(ref list!);

                    if (view._items.Length - view._size < source.Length)
                    {
                        Grow(ref view._items, view._size, checked(view._size + source.Length));
                    }

                    source.CopyTo(view._items.AsSpan(view._size));
                    view._size += source.Length;
                    view._version++;
                }
                else
                {
                    ref var view = ref Unsafe.As<List<T>, CollectionsMarshal.LegacyListView<T>>(ref list!);

                    if (view._items.Length - view._size < source.Length)
                    {
                        Grow(ref view._items, view._size, checked(view._size + source.Length));
                    }

                    source.CopyTo(view._items.AsSpan(view._size));
                    view._size += source.Length;
                    view._version++;
                }
            }
        }

        // CollectionExtensions.InsertRange
        public static void InsertRange<T>(this List<T> list, int index, ReadOnlySpan<T> source)
        {
            if (!source.IsEmpty)
            {
                if (!CollectionsMarshal.IsLegacyList)
                {
                    ref var view = ref Unsafe.As<List<T>, CollectionsMarshal.ListView<T>>(ref list!);

                    if (view._items.Length - view._size < source.Length)
                    {
                        Grow(ref view._items, view._size, checked(view._size + source.Length));
                    }

                    if (index < view._size)
                    {
                        Array.Copy(view._items, index, view._items, index + source.Length, view._size - index);
                    }

                    source.CopyTo(view._items.AsSpan(index));
                    view._size += source.Length;
                    view._version++;
                }
                else
                {
                    ref var view = ref Unsafe.As<List<T>, CollectionsMarshal.LegacyListView<T>>(ref list!);

                    if (view._items.Length - view._size < source.Length)
                    {
                        Grow(ref view._items, view._size, checked(view._size + source.Length));
                    }

                    if (index < view._size)
                    {
                        Array.Copy(view._items, index, view._items, index + source.Length, view._size - index);
                    }

                    source.CopyTo(view._items.AsSpan(index));
                    view._size += source.Length;
                    view._version++;
                }
            }
        }

        static void Grow<T>(ref T[] items, int size, int capacity)
        {
            SetCapacity(ref items, size, GetNewCapacity(items.Length, capacity));
        }

        static void SetCapacity<T>(ref T[] items, int size, int capacity)
        {
            if (capacity != items.Length)
            {
                if (capacity > 0)
                {
                    T[] newItems = new T[capacity];
                    if (size > 0)
                    {
                        Array.Copy(items, newItems, size);
                    }
                    items = newItems;
                }
                else
                {
                    items = Array.Empty<T>();
                }
            }
        }

        static int GetNewCapacity(int length, int capacity)
        {
            int newCapacity = length == 0 ? 4 : 2 * length;

            if ((uint)newCapacity > ArrayMaxLength) newCapacity = ArrayMaxLength;

            if (newCapacity < capacity) newCapacity = capacity;

            return newCapacity;
        }

#pragma warning restore CS0436
#endif

#if !NET6_0_OR_GREATER

        public static bool TryGetNonEnumeratedCount<T>(this IEnumerable<T> source, out int count)
        {
            if (source is ICollection<T> collection)
            {
                count = collection.Count;
                return true;
            }
            if (source is IReadOnlyCollection<T> rCollection)
            {
                count = rCollection.Count;
                return true;
            }
            count = 0;
            return false;
        }

#endif
    }

#if !NET5_0_OR_GREATER

    internal interface IReadOnlySet<T> : System.Collections.Generic.IEnumerable<T>, System.Collections.Generic.IReadOnlyCollection<T>
    {
    }

#endif
}

