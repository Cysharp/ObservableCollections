using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if !NET7_0_OR_GREATER

#pragma warning disable CS0649
#pragma warning disable CS8618
#pragma warning disable CS8619

namespace System.Runtime.InteropServices;

internal static class CollectionsMarshal
{
    internal static readonly bool IsLegacyList;

#if NETSTANDARD2_0 || NETSTANDARD2_1
    static CollectionsMarshal()
    {
        int listSize = 0;
        try
        {
            listSize = typeof(List<>).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Length;
        }
        catch
        {
            listSize = 3;
        }

        // In .NET Framework, List<T> has a _syncRoot field, so the number of fields becomes 4.
        IsLegacyList = listSize == 4;
    }
#endif

    /// <summary>
    /// similar as AsSpan but modify size to create fixed-size span.
    /// </summary>
    public static Span<T> AsSpan<T>(List<T>? list)
    {
        if (list is null) return default;

        if (IsLegacyList)
        {
            ref var view = ref Unsafe.As<List<T>, LegacyListView<T>>(ref list!);
            return view._items.AsSpan(0, list.Count);
        }
        else
        {
            ref var view = ref Unsafe.As<List<T>, ListView<T>>(ref list!);
            return view._items.AsSpan(0, list.Count);
        }
    }

    internal sealed class ListView<T>
    {
        public T[] _items;
        public int _size;
        public int _version;
    }

    internal sealed class LegacyListView<T>
    {
        public T[] _items;
        public int _size;
        public int _version;
        public Object _syncRoot; // in .NET Framework
    }
}

#endif
