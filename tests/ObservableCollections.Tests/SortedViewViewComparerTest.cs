using System.Collections.Generic;

namespace ObservableCollections.Tests;

public class SortedViewViewComparerTest
{
    [Fact]
    public void Sort()
    {
        var list = new ObservableList<int>();
        var sortedView = list.CreateSortedView(
            x => x,
            x => new ViewContainer<int>(x),
            Comparer<ViewContainer<int>>.Default);

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
            Comparer<ViewContainer<int>>.Default);

        var filter = new TestFilter<int>((value, view) => value % 2 == 0);
        list.Add(50);
        list.Add(10);
        
        sortedView.AttachFilter(filter);
        
        list.Add(20);
        filter.CalledOnCollectionChanged[0].changedKind.Should().Be(ChangedKind.Add);
        filter.CalledOnCollectionChanged[0].value.Should().Be(20);
        filter.CalledOnCollectionChanged[0].index.Should().Be(1);

        list.Remove(20);
        filter.CalledOnCollectionChanged[1].changedKind.Should().Be(ChangedKind.Remove);
        filter.CalledOnCollectionChanged[1].value.Should().Be(20);
        filter.CalledOnCollectionChanged[1].oldIndex.Should().Be(1);

        list[1] = 999; // from 10(at 0 in original) to 999
        filter.CalledOnCollectionChanged[2].changedKind.Should().Be(ChangedKind.Remove);
        filter.CalledOnCollectionChanged[2].value.Should().Be(10);
        filter.CalledOnCollectionChanged[2].oldIndex.Should().Be(0);
        filter.CalledOnCollectionChanged[3].changedKind.Should().Be(ChangedKind.Add);
        filter.CalledOnCollectionChanged[3].value.Should().Be(999);
        filter.CalledOnCollectionChanged[3].index.Should().Be(1);
    }
}