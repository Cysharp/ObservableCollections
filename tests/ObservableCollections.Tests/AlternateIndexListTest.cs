using ObservableCollections.Internal;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ObservableCollections.Tests;

public class AlternateIndexListTest
{
    [Fact]
    public void Insert()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(1, "bar");
        list.Insert(2, "baz");
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, "bar"), (2, "baz"));

        list.Insert(1, "new-bar");
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, "new-bar"), (2, "bar"), (3, "baz"));


        list.Insert(6, "zoo");
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, "new-bar"), (2, "bar"), (3, "baz"), (6, "zoo"));
    }

    [Fact]
    public void InsertRange()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(1, "bar");
        list.Insert(2, "baz");

        list.InsertRange(1, new[] { "new-foo", "new-bar", "new-baz" });
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, "new-foo"), (2, "new-bar"), (3, "new-baz"), (4, "bar"), (5, "baz"));
    }

    [Fact]
    public void InsertSparsed()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(2, "foo");
        list.Insert(8, "baz"); // baz
        list.Insert(4, "bar");
        list.GetIndexedValues().Should().Equal((2, "foo"), (4, "bar"), (9, "baz"));

        list.InsertRange(3, new[] { "new-foo", "new-bar", "new-baz" });
        list.GetIndexedValues().Should().Equal((2, "foo"), (3, "new-foo"), (4, "new-bar"), (5, "new-baz"), (7, "bar"), (12, "baz"));

        list.InsertRange(1, new[] { "zoo" });
        list.GetIndexedValues().Should().Equal((1, "zoo"), (3, "foo"), (4, "new-foo"), (5, "new-bar"), (6, "new-baz"), (8, "bar"), (13, "baz"));
    }

    [Fact]
    public void Remove()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(1, "bar");
        list.Insert(2, "baz");

        list.Remove("bar");
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, "baz"));

        list.RemoveAt(0);
        list.GetIndexedValues().Should().Equal((0, "baz"));
    }

    [Fact]
    public void RemoveRange()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(1, "bar");
        list.Insert(2, "baz");

        list.RemoveRange(1, 2);
        list.GetIndexedValues().Should().Equal((0, "foo"));
    }

    [Fact]
    public void TryGetSet()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");
        list.Insert(4, "baz");

        list.TryGetAtAlternateIndex(2, out var bar).Should().BeTrue();
        bar.Should().Be("bar");

        list.TrySetAtAlternateIndex(4, "new-baz", out var i).Should().BeTrue();
        list.TryGetAtAlternateIndex(4, out var baz).Should().BeTrue();
        baz.Should().Be("new-baz");
    }

    [Fact]
    public void TryReplaceByValue()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");
        list.Insert(4, "baz");

        list.TryReplaceByValue("bar", "new-bar", out var i);
        list.GetIndexedValues().Should().Equal((0, "foo"), (2, "new-bar"), (4, "baz"));
    }

    /// <summary>
    /// Guarantees that TryReplaceByValue reports the position in the list of the replaced element,
    /// and that it reports failure with -1 and leaves the list untouched when no element has the searched value.
    /// </summary>
    [Fact]
    public void TryReplaceByValueReportsResult()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");

        list.TryReplaceByValue("zoo", "new-zoo", out var notFoundIndex).Should().BeFalse();
        notFoundIndex.Should().Be(-1);
        list.GetIndexedValues().Should().Equal((0, "foo"), (2, "bar"));

        list.TryReplaceByValue("bar", "new-bar", out var replacedIndex).Should().BeTrue();
        replacedIndex.Should().Be(1);
        list.GetIndexedValues().Should().Equal((0, "foo"), (2, "new-bar"));
    }

    /// <summary>
    /// Guarantees that Remove removes only the first element that has the value, compares values with the default equality comparer
    /// including null, and returns -1 without modifying the list when no element has the value.
    /// </summary>
    [Fact]
    public void RemoveByValue()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(1, null);
        list.Insert(2, "foo");

        list.Remove("zoo").Should().Be(-1);
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, null), (2, "foo"));

        list.Remove("foo").Should().Be(0);
        list.GetIndexedValues().Should().Equal((0, null), (1, "foo"));

        list.Remove(null).Should().Be(0);
        list.GetIndexedValues().Should().Equal((0, "foo"));
    }

    /// <summary>
    /// Guarantees that setting an element through the indexer, whose argument is the position in the list
    /// (not the alternate index), replaces only the value of that element
    /// and leaves its alternate index and the other elements untouched.
    /// </summary>
    [Fact]
    public void IndexerSet()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");
        list.Insert(4, "baz");

        list[1] = "new-bar";
        list[1].Should().Be("new-bar");
        list.GetIndexedValues().Should().Equal((0, "foo"), (2, "new-bar"), (4, "baz"));
    }

    /// <summary>
    /// Guarantees that the indexer rejects a position outside the list with ArgumentOutOfRangeException,
    /// both when reading and when writing.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void IndexerRejectsOutOfRange(int index)
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");

        list.Invoking(x => _ = x[index]).Should().Throw<ArgumentOutOfRangeException>();
        list.Invoking(x => x[index] = "zoo").Should().Throw<ArgumentOutOfRangeException>();
        list.GetIndexedValues().Should().Equal((0, "foo"), (2, "bar"));
    }

    /// <summary>
    /// Guarantees that TrySetAtAlternateIndex replaces only the value of the element at the given alternate index,
    /// reports the position of that element in the list, and fails without modifying the list when the alternate index does not exist.
    /// </summary>
    [Fact]
    public void TrySetAtAlternateIndex()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");
        list.Insert(4, "baz");

        list.TrySetAtAlternateIndex(2, "new-bar", out var setIndex).Should().BeTrue();
        setIndex.Should().Be(1);
        list.GetIndexedValues().Should().Equal((0, "foo"), (2, "new-bar"), (4, "baz"));

        list.TrySetAtAlternateIndex(3, "zoo", out _).Should().BeFalse();
        list.GetIndexedValues().Should().Equal((0, "foo"), (2, "new-bar"), (4, "baz"));
    }

    /// <summary>
    /// Guarantees that TryReplaceAlternateIndex changes the alternate index of the element and moves it
    /// to the position that keeps the list ordered by alternate index, toward either end of the list,
    /// and that it fails without modifying the list when the alternate index does not exist.
    /// </summary>
    [Fact]
    public void TryReplaceAlternateIndex()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");
        list.Insert(4, "baz");

        list.TryReplaceAlternateIndex(0, 3).Should().BeTrue();
        list.GetIndexedValues().Should().Equal((2, "bar"), (3, "foo"), (4, "baz"));

        list.TryReplaceAlternateIndex(4, 1).Should().BeTrue();
        list.GetIndexedValues().Should().Equal((1, "baz"), (2, "bar"), (3, "foo"));

        list.TryReplaceAlternateIndex(5, 0).Should().BeFalse();
        list.GetIndexedValues().Should().Equal((1, "baz"), (2, "bar"), (3, "foo"));
    }

    /// <summary>
    /// Guarantees that a list built from more elements than the default capacity of its backing array (4) keeps them all in order,
    /// and that RemoveAt shifts the alternate index of every following element up to the last one, which lies past that capacity.
    /// </summary>
    [Fact]
    public void RemoveAtShiftsAllFollowingAlternateIndexes()
    {
        var list = new AlternateIndexList<string>(Enumerable.Range(0, 20).Select(x => (x * 2, x.ToString())));
        list.GetIndexedValues().Should().Equal(Enumerable.Range(0, 20).Select(x => (x * 2, x.ToString())));

        list.RemoveAt(0);
        list.GetIndexedValues().Should().Equal(Enumerable.Range(1, 19).Select(x => (x * 2 - 1, x.ToString())));
    }

    /// <summary>
    /// Guarantees that InsertRange keeps every element in order and shifts the alternate indexes of the following elements
    /// by the number of inserted elements, both when it fills an empty list beyond the default capacity of the backing array (4)
    /// and when an insertion into the middle of the list grows the backing array (from 16 to 32).
    /// </summary>
    [Fact]
    public void InsertRangeGrowsBackingArray()
    {
        var list = new AlternateIndexList<int>();

        list.InsertRange(0, Enumerable.Range(0, 10)).Should().Be(0);
        list.GetIndexedValues().Should().Equal(Enumerable.Range(0, 10).Select(x => (x, x)));

        list.InsertRange(5, Enumerable.Range(100, 7)).Should().Be(5);
        list.GetIndexedValues().Should().Equal(
            Enumerable.Range(0, 5).Select(x => (x, x))
                .Concat(Enumerable.Range(0, 7).Select(x => (x + 5, x + 100)))
                .Concat(Enumerable.Range(5, 5).Select(x => (x + 7, x))));
    }

    /// <summary>
    /// Guarantees that RemoveRange removes the given number of elements starting at the element with the given alternate index,
    /// also when the alternate indexes are not contiguous, and shifts the alternate indexes of the following elements by that number.
    /// </summary>
    [Fact]
    public void RemoveRangeSparse()
    {
        var list = new AlternateIndexList<string>(new[] { (0, "a"), (2, "b"), (5, "c"), (9, "d"), (12, "e") });

        list.RemoveRange(2, 2).Should().Be(1);
        list.GetIndexedValues().Should().Equal((0, "a"), (7, "d"), (10, "e"));
    }

    /// <summary>
    /// Guarantees that removing zero elements changes nothing, that removing the elements up to the end of the list is accepted,
    /// and that an invalid range is rejected without modifying the list, with the same exceptions as List&lt;T&gt;.RemoveRange:
    /// exactly ArgumentException for a range that extends past the end, and ArgumentOutOfRangeException for a negative count.
    /// </summary>
    [Fact]
    public void RemoveRangeBounds()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(1, "bar");
        list.Insert(2, "baz");

        list.RemoveRange(1, 0);
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, "bar"), (2, "baz"));

        list.Invoking(x => x.RemoveRange(1, 3)).Should().ThrowExactly<ArgumentException>();
        list.Invoking(x => x.RemoveRange(0, -1)).Should().ThrowExactly<ArgumentOutOfRangeException>();
        list.GetIndexedValues().Should().Equal((0, "foo"), (1, "bar"), (2, "baz"));

        list.RemoveRange(1, 2);
        list.GetIndexedValues().Should().Equal((0, "foo"));
    }

    /// <summary>
    /// Guarantees that operations on an empty list behave as on an empty List&lt;T&gt;: lookups fail,
    /// and inserting, removing nothing and clearing are accepted.
    /// </summary>
    [Fact]
    public void EmptyList()
    {
        var list = new AlternateIndexList<string>();

        list.Count.Should().Be(0);
        list.Should().BeEmpty();
        list.TryGetAtAlternateIndex(0, out _).Should().BeFalse();
        list.TrySetAtAlternateIndex(0, "foo", out _).Should().BeFalse();
        list.TryReplaceAlternateIndex(0, 1).Should().BeFalse();
        list.TryReplaceByValue("foo", "bar", out _).Should().BeFalse();
        list.Remove("foo").Should().Be(-1);
        list.Invoking(x => x.RemoveAt(0)).Should().Throw<InvalidOperationException>();

        list.RemoveRange(0, 0);
        list.UpdateAlternateIndex(0, 1);
        list.Clear();
        list.Should().BeEmpty();

        list.Insert(3, "foo").Should().Be(0);
        list.GetIndexedValues().Should().Equal((3, "foo"));
    }

    /// <summary>
    /// Guarantees that the list can be reused after Clear and Clear(values): the old elements do not reappear,
    /// and new elements are stored and ordered as in a new list.
    /// </summary>
    [Fact]
    public void ReuseAfterClear()
    {
        var list = new AlternateIndexList<string>(Enumerable.Range(0, 10).Select(x => (x, x.ToString())));

        list.Clear();
        list.Should().BeEmpty();

        list.Insert(1, "foo");
        list.Insert(0, "bar");
        list.GetIndexedValues().Should().Equal((0, "bar"), (2, "foo"));

        list.Clear(new[] { (5, "x"), (7, "y") });
        list.GetIndexedValues().Should().Equal((5, "x"), (7, "y"));

        list.Insert(6, "z");
        list.GetIndexedValues().Should().Equal((5, "x"), (6, "z"), (8, "y"));
    }

    /// <summary>
    /// Modifying the value or the alternate index of an element, or removing zero elements, is not a structural change.
    /// This guarantees that such operations during enumeration do not invalidate the enumeration
    /// and that the enumeration observes the modified values.
    /// </summary>
    [Fact]
    public void NonStructuralChangeDuringEnumerationContinues()
    {
        var list = new AlternateIndexList<string>();

        list.Insert(0, "foo");
        list.Insert(2, "bar");
        list.Insert(4, "baz");

        var values = new List<string>();
        foreach (var value in list)
        {
            values.Add(value);
            if (values.Count == 1)
            {
                list[1] = "new-bar";
                list.TrySetAtAlternateIndex(4, "new-baz", out _);
                list.TryReplaceByValue("foo", "new-foo", out _);
                list.UpdateAlternateIndex(1, 1);
                list.RemoveRange(0, 0);
            }
        }

        values.Should().Equal("foo", "new-bar", "new-baz");
        list.GetIndexedValues().Should().Equal((0, "new-foo"), (3, "new-bar"), (5, "new-baz"));
    }

    /// <summary>
    /// Guarantees that enumeration throws InvalidOperationException from the next MoveNext when the list is structurally changed
    /// or sorted (by TryReplaceAlternateIndex) during enumeration, as List&lt;T&gt; does,
    /// including when the change happens after the last element has been returned.
    /// </summary>
    [Theory]
    [InlineData("Insert")]
    [InlineData("InsertRange")]
    [InlineData("Remove")]
    [InlineData("RemoveAt")]
    [InlineData("RemoveRange")]
    [InlineData("TryReplaceAlternateIndex")]
    [InlineData("Clear")]
    public void StructuralChangeDuringEnumerationThrows(string operation)
    {
        Action<AlternateIndexList<string>> modify = operation switch
        {
            "Insert" => x => x.Insert(1, "zoo"),
            "InsertRange" => x => x.InsertRange(1, new[] { "zoo" }),
            "Remove" => x => x.Remove("foo"),
            "RemoveAt" => x => x.RemoveAt(0),
            "RemoveRange" => x => x.RemoveRange(0, 1),
            "TryReplaceAlternateIndex" => x => x.TryReplaceAlternateIndex(0, 3),
            "Clear" => x => x.Clear(),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        foreach (var modifyAtLast in new[] { false, true })
        {
            var list = new AlternateIndexList<string>();
            list.Insert(0, "foo");
            list.Insert(2, "bar");

            using var e = list.GetEnumerator();
            e.MoveNext().Should().BeTrue();
            if (modifyAtLast)
            {
                e.MoveNext().Should().BeTrue();
            }

            modify(list);

            e.Invoking(x => x.MoveNext()).Should().Throw<InvalidOperationException>();
        }
    }

    /// <summary>
    /// Guarantees that sorting a list with a single element (by TryReplaceAlternateIndex) still invalidates enumeration,
    /// as List&lt;T&gt;.Sort does even when there is nothing to reorder.
    /// </summary>
    [Fact]
    public void TrivialSortDuringEnumerationThrows()
    {
        var list = new AlternateIndexList<string>();
        list.Insert(0, "foo");

        using var e = list.GetEnumerator();
        e.MoveNext().Should().BeTrue();

        list.TryReplaceAlternateIndex(0, 1).Should().BeTrue();

        e.Invoking(x => x.MoveNext()).Should().Throw<InvalidOperationException>();
    }
}
