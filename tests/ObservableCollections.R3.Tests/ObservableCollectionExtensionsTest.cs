using System.Collections.Generic;
using System.Threading;
using R3;

namespace ObservableCollections.R3.Tests;

public class ObservableCollectionExtensionsTest
{
    [Fact]
    public void ObserveAdd()
    {
        var events = new List<CollectionAddEvent<int>>();
        
        var collection = new ObservableList<int>();

        var subscription = collection.ObserveAdd().Subscribe(ev => events.Add(ev));
        collection.Add(10);
        collection.Add(50);
        collection.Add(30);

        events.Count.Should().Be(3);
        events[0].Index.Should().Be(0);
        events[0].Value.Should().Be(10);
        events[1].Index.Should().Be(1);
        events[1].Value.Should().Be(50);
        events[2].Index.Should().Be(2);
        events[2].Value.Should().Be(30);
        
        subscription.Dispose();
        
        collection.Add(100);
        events.Count.Should().Be(3);
    }
    
    [Fact]
    public void ObserveAdd_CancellationToken()
    {
        var cts = new CancellationTokenSource();
        var events = new List<CollectionAddEvent<int>>();
        var result = default(Result?);
        
        var collection = new ObservableList<int>();

        var subscription = collection.ObserveAdd(cts.Token).Subscribe(ev => events.Add(ev), x => result = x);
        collection.Add(10);
        collection.Add(50);
        collection.Add(30);

        events.Count.Should().Be(3);
        
        cts.Cancel();

        result.HasValue.Should().BeTrue();
        
        subscription.Dispose();
        
        collection.Add(100);
        events.Count.Should().Be(3);
    }
    
    [Fact]
    public void ObserveRemove()
    {
        var events = new List<CollectionRemoveEvent<int>>();
        var collection = new ObservableList<int>([111, 222, 333]);
        var cts = new CancellationTokenSource();
        var result = default(Result?);

        var subscription = collection.ObserveRemove(cts.Token).Subscribe(ev => events.Add(ev), x => result = x);
        collection.RemoveAt(1);

        events.Count.Should().Be(1);
        events[0].Index.Should().Be(1);
        events[0].Value.Should().Be(222);
        
        cts.Cancel();
        result.HasValue.Should().BeTrue();
        
        subscription.Dispose();
        
        collection.RemoveAt(0);
        events.Count.Should().Be(1);
    }
    
    [Fact]
    public void ObserveReplace()
    {
        var events = new List<CollectionReplaceEvent<int>>();
        var collection = new ObservableList<int>([111, 222, 333]);
        var cts = new CancellationTokenSource();
        var result = default(Result?);

        var subscription = collection.ObserveReplace(cts.Token).Subscribe(ev => events.Add(ev), x => result = x);
        collection[1] = 999;

        events.Count.Should().Be(1);
        events[0].Index.Should().Be(1);
        events[0].OldValue.Should().Be(222);
        events[0].NewValue.Should().Be(999);
        
        cts.Cancel();
        result.HasValue.Should().BeTrue();
        
        subscription.Dispose();

        collection[1] = 444;
        events.Count.Should().Be(1);
    }
    
    [Fact]
    public void ObserveMove()
    {
        var events = new List<CollectionMoveEvent<int>>();
        var collection = new ObservableList<int>([111, 222, 333]);
        var cts = new CancellationTokenSource();
        var result = default(Result?);

        var subscription = collection.ObserveMove(cts.Token).Subscribe(ev => events.Add(ev), x => result = x);
        
        collection.Move(1, 2);

        events.Count.Should().Be(1);
        events[0].OldIndex.Should().Be(1);
        events[0].NewIndex.Should().Be(2);
        events[0].Value.Should().Be(222);
        
        cts.Cancel();
        result.HasValue.Should().BeTrue();
        
        subscription.Dispose();

        collection.Move(1, 2);
        events.Count.Should().Be(1);
    }
}