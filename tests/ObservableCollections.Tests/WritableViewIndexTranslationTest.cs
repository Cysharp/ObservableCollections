using System;

namespace ObservableCollections.Tests;

/// <summary>
/// Position-based operations on a writable view (the setter, RemoveAt, and Insert) must translate the
/// view index back to the source index (AlternateIndexList.GetAlternateIndex) before applying them to
/// the source.
///
/// T and TView are different types so that the shortcut that skips the converter
/// (typeof(T) == typeof(TView)) does not interfere.
/// </summary>
public class WritableViewIndexTranslationTest
{
    static int ToOriginal(string newView, int original, ref bool setValue)
    {
        return int.Parse(newView.Substring(1));
    }

    [Fact]
    public void RemoveAt()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"]. Its [1] is "$4", so 4 should be removed from the source.
        bindable.RemoveAt(1);

        list.Should().Equal(new[] { 1, 2, 3 });

        bindable.Count.Should().Be(1);
        bindable[0].Should().Be("$2");
    }

    [Fact]
    public void Insert()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"]. We want to insert at its [1], so in the source
        // it should go right before 4 (source index 3).
        bindable.Insert(1, "$6");

        list.Should().Equal(new[] { 1, 2, 3, 6, 4 });

        bindable.Count.Should().Be(3);
        bindable[0].Should().Be("$2");
        bindable[1].Should().Be("$6");
        bindable[2].Should().Be("$4");
    }

    /// <summary>
    /// An insertion at the tail has no corresponding source index, so it is treated as an append.
    /// This is a legitimate operation and must not be confused with rejecting an out-of-range index.
    /// </summary>
    [Fact]
    public void InsertAtTail()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"]. Its [2] is the tail.
        bindable.Insert(2, "$6");

        list.Should().Equal(new[] { 1, 2, 3, 4, 6 });

        bindable.Should().Equal(new[] { "$2", "$4", "$6" });
    }

    /// <summary>
    /// Verifies that an out-of-range index is rejected with ArgumentOutOfRangeException and leaves the
    /// source unchanged.
    /// The fallback to appending used to swallow out-of-range indices as well.
    /// </summary>
    [Fact]
    public void OutOfRangeIndexIsRejected()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal);

        // The view is ["$2", "$4"], so Count is 2.
        bindable.Invoking(x => x.Insert(3, "$6")).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.Insert(-1, "$6")).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.RemoveAt(2)).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x.RemoveAt(-1)).Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x[2] = "$6").Should().Throw<ArgumentOutOfRangeException>();
        bindable.Invoking(x => x[-1] = "$6").Should().Throw<ArgumentOutOfRangeException>();

        list.Should().Equal(new[] { 1, 2, 3, 4 });
    }
}
