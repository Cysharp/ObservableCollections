using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Tests;

public class ToNotifyCollectionChangedTest
{
    [Fact]
    public void ToNotifyCollectionChanged()
    {
        var list = new ObservableList<int>();

        list.Add(10);
        list.Add(20);
        list.Add(30);

        var notify = list.CreateView(x => $"${x}").ToNotifyCollectionChanged();

        list.Add(40);
        list.Add(50);

        using var e = notify.GetEnumerator();
        e.MoveNext().Should().BeTrue();
        e.Current.Should().Be("$10");
        e.MoveNext().Should().BeTrue();
        e.Current.Should().Be("$20");
        e.MoveNext().Should().BeTrue();
        e.Current.Should().Be("$30");
        e.MoveNext().Should().BeTrue();
        e.Current.Should().Be("$40");
        e.MoveNext().Should().BeTrue();
        e.Current.Should().Be("$50");
        e.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void ToNotifyCollectionChanged_Filter()
    {
        var list = new ObservableList<int>();

        list.Add(1);
        list.Add(2);
        list.Add(5);
        list.Add(3);

        var view = list.CreateView(x => $"${x}");
        var notify = view.ToNotifyCollectionChanged();

        view.AttachFilter((value) => value % 2 == 0);

        list.Add(4);

        using var e = notify.GetEnumerator();
        e.MoveNext().Should().BeTrue();
        e.Current.Should().Be("$2");
        e.MoveNext().Should().BeTrue();
        e.Current.Should().Be("$4");
        e.MoveNext().Should().BeFalse();
    }

    /// <summary>
    /// Inserting an element that the filter rejects only shifts the alternate indexes of the filtered view,
    /// and replacing an element that the filter accepts with a value that it also accepts only replaces a value in it;
    /// neither is a structural change of the view.
    /// This guarantees that doing so on the same thread while the filtered view is being enumerated does not break the enumeration,
    /// that the enumeration observes the replaced value, and that the alternate indexes were actually shifted,
    /// so that a later replacement still reaches the right element.
    /// </summary>
    [Fact]
    public void ToNotifyCollectionChanged_Filter_NonStructuralChangeDuringEnumeration()
    {
        var list = new ObservableList<int>();

        list.Add(2);
        list.Add(4);
        list.Add(6);

        var view = list.CreateView(x => $"${x}");
        var notify = view.ToNotifyCollectionChanged();

        view.AttachFilter((value) => value % 2 == 0);

        var values = new List<string>();
        foreach (var value in notify)
        {
            values.Add(value);
            if (values.Count == 1)
            {
                list.Insert(0, 1); // rejected by the filter; shifts the alternate indexes of all the elements of the view
                list[2] = 10; // accepted by the filter; replaces 4, whose alternate index is now 2
            }
        }

        values.Should().Equal("$2", "$10", "$6");
        notify.Should().Equal("$2", "$10", "$6");

        list[3] = 8; // replaces 6, whose alternate index is now 3
        notify.Should().Equal("$2", "$10", "$8");
    }

    /// <summary>
    /// Guarantees that a filtered view whose alternate indexes are not contiguous follows InsertRange into the middle, RemoveRange and AddRange
    /// of the source when the filter accepts all the inserted or removed elements. It is tested both with range notifications
    /// (the view uses AlternateIndexList.InsertRange and RemoveRange) and without them (the view inserts and removes element by element).
    /// After each operation, an element after the range is replaced, which reaches the right element only if its alternate index
    /// was shifted by exactly the number of inserted or removed elements.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Filter_RangeOperations(bool supportRange)
    {
        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        var view = list.CreateView(x => $"${x}");
        IEnumerable<string> viewList = supportRange ? view.ToViewList() : view.ToNotifyCollectionChanged();

        view.AttachFilter((value) => value % 2 == 0);
        viewList.Should().Equal("$2", "$4");

        list.InsertRange(2, new[] { 6, 8 }); // [1, 2, 6, 8, 3, 4]
        viewList.Should().Equal("$2", "$6", "$8", "$4");
        list[5] = 40; // replaces 4, whose alternate index must have been shifted from 3 to 5
        viewList.Should().Equal("$2", "$6", "$8", "$40");

        list.RemoveRange(2, 2); // [1, 2, 3, 40]
        viewList.Should().Equal("$2", "$40");
        list[3] = 44; // replaces 40, whose alternate index must have been shifted from 5 to 3
        viewList.Should().Equal("$2", "$44");

        list.AddRange(new[] { 10, 12 }); // [1, 2, 3, 44, 10, 12]
        viewList.Should().Equal("$2", "$44", "$10", "$12");
        list[3] = 46; // appending must not shift the preceding elements
        list[5] = 16; // the appended elements get the alternate indexes of their source indexes
        viewList.Should().Equal("$2", "$46", "$10", "$16");
    }

    [Fact]
    public void ToNotifyCollectionChanged_Move()
    {
        var list = new ObservableList<int> { 0, 1, 2, 3 };

        var view = list.CreateView(i => i)
            .ToNotifyCollectionChanged();

        int moveEventCount = 0;

        view.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
                moveEventCount++;
        };

        list.Move(0, 1);

        moveEventCount.Should().Be(1);
    }
}
