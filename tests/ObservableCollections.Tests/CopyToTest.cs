using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ObservableCollections.Tests;

public class ObservableListCopyToTest
{
    static ObservableList<int> CreateSource()
    {
        return new ObservableList<int> { 1, 2, 3 };
    }

    /// <summary>
    /// Ensures all items are copied from the beginning of the array.
    /// </summary>
    [Fact]
    public void CopyToArray()
    {
        var destination = new int[3];

        CreateSource().CopyTo(destination, 0);

        destination.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Ensures items are copied from the position specified by arrayIndex, leaving the preceding elements untouched.
    /// </summary>
    [Fact]
    public void CopyToArrayWithOffset()
    {
        var destination = new int[5];

        CreateSource().CopyTo(destination, 2);

        destination.Should().Equal(0, 0, 1, 2, 3);
    }

    /// <summary>
    /// Ensures all items are copied through the Span overload.
    /// </summary>
    [Fact]
    public void CopyToSpan()
    {
        var destination = new int[3];

        CreateSource().CopyTo(destination.AsSpan());

        destination.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Ensures the remaining space is left untouched when the destination is longer than the item count.
    /// </summary>
    [Fact]
    public void CopyToLongerDestination()
    {
        var destination = new[] { -1, -1, -1, -1, -1 };

        CreateSource().CopyTo(destination.AsSpan());

        destination.Should().Equal(1, 2, 3, -1, -1);
    }

    /// <summary>
    /// Ensures an empty collection copies nothing and does not throw.
    /// </summary>
    [Fact]
    public void CopyToFromEmpty()
    {
        var destination = new[] { -1 };

        new ObservableList<int>().CopyTo(destination, 0);

        destination.Should().Equal(-1);
    }

    /// <summary>
    /// Ensures ArgumentException is thrown when the space after arrayIndex is not enough for the item count.
    /// </summary>
    [Fact]
    public void TooShortDestinationThrows()
    {
        var source = CreateSource();

        Action tooShortArray = () => source.CopyTo(new int[2], 0);
        Action tooShortByOffset = () => source.CopyTo(new int[3], 1);
        Action tooShortSpan = () => source.CopyTo(new int[2].AsSpan());

        tooShortArray.Should().ThrowExactly<ArgumentException>();
        tooShortByOffset.Should().ThrowExactly<ArgumentException>();
        tooShortSpan.Should().ThrowExactly<ArgumentException>();
    }

    /// <summary>
    /// Ensures ArgumentNullException is thrown when array is null, whether or not the collection has items.
    /// </summary>
    [Fact]
    public void NullArrayThrows()
    {
        Action nonEmpty = () => CreateSource().CopyTo(null!, 0);
        Action empty = () => new ObservableList<int>().CopyTo(null!, 0);

        nonEmpty.Should().ThrowExactly<ArgumentNullException>();
        empty.Should().ThrowExactly<ArgumentNullException>();
    }

    /// <summary>
    /// Ensures ArgumentOutOfRangeException is thrown when arrayIndex is negative.
    /// </summary>
    [Fact]
    public void NegativeArrayIndexThrows()
    {
        Action act = () => CreateSource().CopyTo(new int[3], -1);

        act.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }
}

/// <summary>
/// Verifies that CopyTo behaves consistently across every derived implementation of NotifyCollectionChangedSynchronizedViewList.
/// </summary>
public abstract class SynchronizedViewListCopyToTestBase
{
    protected abstract NotifyCollectionChangedSynchronizedViewList<int> CreateViewList(ObservableList<int> source);

    private NotifyCollectionChangedSynchronizedViewList<int> CreateViewList()
    {
        return CreateViewList(new ObservableList<int> { 1, 2, 3 });
    }

    /// <summary>
    /// Ensures ICollection&lt;T&gt;.CopyTo copies all items from the beginning of the array.
    /// </summary>
    [Fact]
    public void CopyToArray()
    {
        var destination = new int[3];

        ((ICollection<int>)CreateViewList()).CopyTo(destination, 0);

        destination.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Ensures items are copied from the position specified by arrayIndex, leaving the preceding elements untouched.
    /// </summary>
    [Fact]
    public void CopyToArrayWithOffset()
    {
        var destination = new int[5];

        ((ICollection<int>)CreateViewList()).CopyTo(destination, 2);

        destination.Should().Equal(0, 0, 1, 2, 3);
    }

    /// <summary>
    /// Ensures all items are copied through the Span overload.
    /// </summary>
    [Fact]
    public void CopyToSpan()
    {
        var destination = new int[3];

        CreateViewList().CopyTo(destination.AsSpan());

        destination.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Ensures ToList returns a List that preserves every item and its order.
    /// </summary>
    [Fact]
    public void ToList()
    {
        CreateViewList().ToList().Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Ensures the non-generic ICollection.CopyTo works against an array of the item type itself.
    /// </summary>
    [Fact]
    public void CopyToNonGenericTypedArray()
    {
        var destination = new int[3];

        ((ICollection)CreateViewList()).CopyTo(destination, 0);

        destination.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Ensures the non-generic ICollection.CopyTo also works against object[],
    /// which is what non-generic callers such as WPF pass in.
    /// </summary>
    [Fact]
    public void CopyToObjectArray()
    {
        var destination = new object[3];

        ((ICollection)CreateViewList()).CopyTo(destination, 0);

        destination.Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// Ensures an empty collection copies nothing and does not throw.
    /// </summary>
    [Fact]
    public void CopyToFromEmpty()
    {
        var destination = new[] { -1 };

        ((ICollection<int>)CreateViewList(new ObservableList<int>())).CopyTo(destination, 0);

        destination.Should().Equal(-1);
    }

    /// <summary>
    /// Ensures ArgumentException is thrown regardless of the implementation when the space after arrayIndex is not enough for the item count.
    /// </summary>
    [Fact]
    public void TooShortDestinationThrows()
    {
        var list = CreateViewList();

        Action tooShortArray = () => ((ICollection<int>)list).CopyTo(new int[2], 0);
        Action tooShortByOffset = () => ((ICollection<int>)list).CopyTo(new int[3], 1);
        Action tooShortSpan = () => list.CopyTo(new int[2].AsSpan());

        tooShortArray.Should().ThrowExactly<ArgumentException>();
        tooShortByOffset.Should().ThrowExactly<ArgumentException>();
        tooShortSpan.Should().ThrowExactly<ArgumentException>();
    }

    /// <summary>
    /// Ensures ArgumentNullException is thrown when array is null, whether or not the collection has items.
    /// </summary>
    [Fact]
    public void NullArrayThrows()
    {
        Action nonEmpty = () => ((ICollection<int>)CreateViewList()).CopyTo(null!, 0);
        Action empty = () => ((ICollection<int>)CreateViewList(new ObservableList<int>())).CopyTo(null!, 0);

        nonEmpty.Should().ThrowExactly<ArgumentNullException>();
        empty.Should().ThrowExactly<ArgumentNullException>();
    }

    /// <summary>
    /// Ensures ArgumentOutOfRangeException is thrown when arrayIndex is negative.
    /// </summary>
    [Fact]
    public void NegativeArrayIndexThrows()
    {
        Action act = () => ((ICollection<int>)CreateViewList()).CopyTo(new int[3], -1);

        act.Should().ThrowExactly<ArgumentOutOfRangeException>();
    }
}

public class ObservableListSynchronizedViewListCopyToTest : SynchronizedViewListCopyToTestBase
{
    protected override NotifyCollectionChangedSynchronizedViewList<int> CreateViewList(ObservableList<int> source)
    {
        return source.ToNotifyCollectionChangedSlim();
    }
}

public sealed class NonFilteredSynchronizedViewListCopyToTest : SynchronizedViewListCopyToTestBase
{
    protected override NotifyCollectionChangedSynchronizedViewList<int> CreateViewList(ObservableList<int> source)
    {
        return source.ToNotifyCollectionChanged();
    }
}

public sealed class FiltableSynchronizedViewListCopyToTest : SynchronizedViewListCopyToTestBase
{
    protected override NotifyCollectionChangedSynchronizedViewList<int> CreateViewList(ObservableList<int> source)
    {
        return source.CreateView(static x => x).ToNotifyCollectionChanged();
    }

    /// <summary>
    /// Ensures Count and the copied result contain only the filtered items when the filter is attached before the view list is created.
    /// </summary>
    [Fact]
    public void CopyToWithFilterAttachedBeforeCreate()
    {
        var view = new ObservableList<int> { 1, 2, 3, 4, 5 }.CreateView(static x => x);
        view.AttachFilter(static x => x % 2 == 1);

        var list = view.ToNotifyCollectionChanged();

        list.Count.Should().Be(3);
        list.ToList().Should().Equal(1, 3, 5);
    }

    /// <summary>
    /// Ensures Count and the copied result contain only the filtered items when the filter is attached after the view list is created.
    /// </summary>
    [Fact]
    public void CopyToWithFilterAttachedAfterCreate()
    {
        var view = new ObservableList<int> { 1, 2, 3, 4, 5 }.CreateView(static x => x);
        var list = view.ToNotifyCollectionChanged();

        view.AttachFilter(static x => x % 2 == 1);

        list.Count.Should().Be(3);
        list.ToList().Should().Equal(1, 3, 5);
    }

    /// <summary>
    /// Ensures all items are copied again after ResetFilter.
    /// </summary>
    [Fact]
    public void CopyToAfterResetFilter()
    {
        var view = new ObservableList<int> { 1, 2, 3, 4, 5 }.CreateView(static x => x);
        var list = view.ToNotifyCollectionChanged();

        view.AttachFilter(static x => x % 2 == 1);
        view.ResetFilter();

        list.Count.Should().Be(5);
        list.ToList().Should().Equal(1, 2, 3, 4, 5);
    }

    /// <summary>
    /// Ensures the copied result contains only the filtered items even when items are added while a filter excludes some of them.
    /// </summary>
    [Fact]
    public void CopyToAfterAddWithFilter()
    {
        var source = new ObservableList<int> { 1, 2, 3 };
        var view = source.CreateView(static x => x);
        var list = view.ToNotifyCollectionChanged();

        view.AttachFilter(static x => x % 2 == 1);
        source.Add(4);
        source.Add(5);

        list.Count.Should().Be(3);
        list.ToList().Should().Equal(1, 3, 5);
    }
}
