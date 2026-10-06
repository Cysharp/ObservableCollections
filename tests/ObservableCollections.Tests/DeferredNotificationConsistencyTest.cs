using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

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
    [Fact]
    public void WorkerThreadMutation()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        // Waiting guarantees that both changes complete before the UI thread handles the notifications.
        Task.Run(() =>
        {
            list.Add(10);
            list.RemoveAt(0);
        }).Wait();

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

        Task.Run(() =>
        {
            list.Insert(0, 6); // Passes the filter, so it goes to the head of the view
            list.RemoveAt(4);  // The original 4; it disappears from the tail of the view
        }).Wait();

        dispatcher.PendingCount.Should().Be(2);

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();

        notify.Should().Equal(new[] { "$6", "$2" });
    }

    /// <summary>
    /// For comparison. When the change is made on the UI thread, SynchronizationContextCollectionEventDispatcher
    /// chooses to raise synchronously, so nothing breaks. Ensures this case keeps working after the fix.
    /// </summary>
    [Fact]
    public void UiThreadMutation()
    {
        var context = new QueuedSynchronizationContext();
        var previous = SynchronizationContext.Current;

        // SynchronizationContextCollectionEventDispatcher requires SynchronizationContext.Current in its
        // static initializer (the static readonly Current in ICollectionEventDispatcher.cs), so it has to
        // be set before this type is touched.
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var list = new ObservableList<int>();
            using var view = list.CreateView(x => $"${x}");
            using var notify = view.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(context));

            var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

            list.Add(10);
            list.RemoveAt(0);

            // Synchronous raising was chosen, so nothing is queued.
            context.PendingCount.Should().Be(0);

            tracker.Actions.Should().Equal(new[]
            {
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Remove,
            });

            tracker.Violations.Should().BeEmpty();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }
}
