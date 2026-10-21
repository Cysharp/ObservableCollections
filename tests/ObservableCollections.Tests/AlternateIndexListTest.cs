using ObservableCollections.Internal;
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
    /// Guarantees that setting an element through the indexer replaces only the value of that element
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
    /// Guarantees that TrySetAtAlternateIndex replaces only the value of the element at the given alternate index,
    /// reports its position, and fails without modifying the list when the alternate index does not exist.
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
    /// to the position that keeps the list ordered by alternate index, and fails when the alternate index does not exist.
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

        list.TryReplaceAlternateIndex(1, 5).Should().BeFalse();
        list.GetIndexedValues().Should().Equal((2, "bar"), (3, "foo"), (4, "baz"));
    }

    /// <summary>
    /// Guarantees that removing an element shifts the alternate index of every following element,
    /// including the last one, so the shifted range is not cut short.
    /// </summary>
    [Fact]
    public void RemoveShiftsAllFollowingAlternateIndexes()
    {
        var list = new AlternateIndexList<string>(Enumerable.Range(0, 20).Select(x => (x * 2, x.ToString())));

        list.RemoveAt(0);
        list.GetIndexedValues().Should().Equal(Enumerable.Range(1, 19).Select(x => (x * 2 - 1, x.ToString())));
    }
}
