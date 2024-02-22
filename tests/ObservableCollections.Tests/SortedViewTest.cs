using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Tests;

public class SortedViewTest
{
    [Fact]
    public void Sort()
    {
        var list = new ObservableList<int>();
        var sortedView = list.CreateSortedView(
            x => x,
            x => new ViewContainer<int>(x),
            Comparer<int>.Default);

        list.Add(10);
        list.Add(50);
        list.Add(30);
        list.Add(20);
        list.Add(40);

        using var e = sortedView.GetEnumerator();
        e.MoveNext().Should().BeTrue();
        e.Current.Value.Should().Be(10);
        e.MoveNext().Should().BeTrue();
        e.Current.Value.Should().Be(20);
        e.MoveNext().Should().BeTrue();
        e.Current.Value.Should().Be(30);
        e.MoveNext().Should().BeTrue();
        e.Current.Value.Should().Be(40);
        e.MoveNext().Should().BeTrue();
        e.Current.Value.Should().Be(50);
        e.MoveNext().Should().BeFalse();
    }
    
    [Fact]
    public void ObserveIndex()
    {
        var list = new ObservableList<int>();
        var sortedView = list.CreateSortedView(
            x => x,
            x => new ViewContainer<int>(x),
            Comparer<int>.Default);

        var filter = new TestFilter<int>((value, view) => value % 2 == 0);
        list.Add(50);
        list.Add(10);
        
        sortedView.AttachFilter(filter);
        
        list.Add(20);
        filter.CalledOnCollectionChanged[0].Action.Should().Be(NotifyCollectionChangedAction.Add);
        filter.CalledOnCollectionChanged[0].NewValue.Should().Be(20);
        filter.CalledOnCollectionChanged[0].NewView.Should().Be(new ViewContainer<int>(20));
        filter.CalledOnCollectionChanged[0].NewViewIndex.Should().Be(1);

        list.Remove(20);
        filter.CalledOnCollectionChanged[1].Action.Should().Be(NotifyCollectionChangedAction.Remove);
        filter.CalledOnCollectionChanged[1].OldValue.Should().Be(20);
        filter.CalledOnCollectionChanged[1].OldView.Should().Be(new ViewContainer<int>(20));
        filter.CalledOnCollectionChanged[1].OldViewIndex.Should().Be(1);

        list[1] = 999; // from 10(at 0 in original) to 999
        filter.CalledOnCollectionChanged[2].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        filter.CalledOnCollectionChanged[2].NewValue.Should().Be(999);
        filter.CalledOnCollectionChanged[2].OldValue.Should().Be(10);
        filter.CalledOnCollectionChanged[2].NewViewIndex.Should().Be(1);
    }
}