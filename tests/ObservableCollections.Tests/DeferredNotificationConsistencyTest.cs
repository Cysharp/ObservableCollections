using System.Collections.Specialized;

namespace ObservableCollections.Tests;

/// <summary>
/// When an ICollectionEventDispatcher defers notifications to the UI thread, the content of each
/// notification must match the state of the collection at the moment the UI thread handles it.
/// If only the internal list update runs ahead on the mutating thread, that consistency breaks.
///
/// https://github.com/Cysharp/ObservableCollections/issues/115
/// </summary>
public class DeferredNotificationConsistencyTest
{
    /// <summary>
    /// Verifies that changes are deferred when the dispatcher posts them, as it does for a change on a worker thread,
    /// and that whenever a notification is raised, the visible content matches the content rebuilt from the
    /// notifications so far.
    /// </summary>
    [Fact]
    public void WorkerThreadMutation()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        list.Add(10);
        list.RemoveAt(0);

        // Confirms that the notifications are deferred. If this were 0, the test would verify nothing.
        dispatcher.PendingCount.Should().Be(2);

        dispatcher.Pump();

        tracker.Actions.Should().Equal(new[]
        {
            NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Remove,
        });

        tracker.Violations.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that changes to a filtered view are deferred when the dispatcher posts them, as it does for a change
    /// on a worker thread, and that whenever a notification is raised, the visible content matches the content
    /// rebuilt from the notifications so far.
    /// The indexes of the notifications are those in the view, not in the source.
    /// </summary>
    [Fact]
    public void WorkerThreadMutation_Filter()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0); // ["$2", "$4"]

        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        list.Insert(0, 6); // Passes the filter, so it goes to the head of the view
        list.RemoveAt(4);  // The original 4; it disappears from the tail of the view

        dispatcher.PendingCount.Should().Be(2);

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();

        notify.Should().Equal(new[] { "$6", "$2" });
    }
}
