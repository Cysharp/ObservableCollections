using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using ObservableCollections;

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
    /// Adding an element that the filter rejects only shifts the alternate indexes of the filtered view,
    /// and replacing an element that the filter accepts only replaces a value in it; neither is a structural change of the view.
    /// This guarantees that doing so on the same thread while the filtered view is being enumerated does not break the enumeration
    /// on any target framework, and that the enumeration observes the replaced value.
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
                list.Add(1); // rejected by the filter
                list[1] = 10; // accepted by the filter
            }
        }

        values.Should().Equal("$2", "$10", "$6");
        notify.Should().Equal("$2", "$10", "$6");
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
