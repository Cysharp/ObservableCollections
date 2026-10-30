using System;
using System.Collections.Specialized;
using System.Linq;
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
    /// <summary>
    /// Verifies that changes made on a worker thread are deferred, and that whenever a notification is raised, the
    /// visible content matches the content rebuilt from the notifications so far.
    /// </summary>
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

    /// <summary>
    /// Verifies that changes made on a worker thread to a filtered view are deferred, and that whenever a
    /// notification is raised, the visible content matches the content rebuilt from the notifications so far.
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

        // The dispatcher learns its bound thread in the constructor, by comparing SynchronizationContext.Current
        // with the given context, so install the context before creating it. Otherwise even the changes on this
        // thread would be posted.
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

    const int WorkerChanges = 20000;
    const int MinimumUiRounds = 2000;

    /// <summary>
    /// Makes one random change to the list. Holds the lock of the list, so that the index it picks is still valid
    /// when another thread changes the list too.
    /// </summary>
    static void ChangeRandomly(ObservableList<int> list, Random random)
    {
        lock (list.SyncRoot)
        {
            var count = list.Count;
            switch (random.Next(count == 0 ? 3 : 8))
            {
                case 0:
                case 1:
                    list.Add(random.Next(100));
                    break;
                case 2:
                    list.Insert(random.Next(count + 1), random.Next(100));
                    break;
                case 3:
                case 4:
                    list.RemoveAt(random.Next(count));
                    break;
                case 5:
                    list[random.Next(count)] = random.Next(100);
                    break;
                case 6:
                    list.Move(random.Next(count), random.Next(count));
                    break;
                default:
                    if (random.Next(10) == 0)
                    {
                        list.Clear();
                    }
                    else
                    {
                        list.Add(random.Next(100));
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Runs the changes of a worker thread, while the UI thread keeps changing the list too, runs the extra action,
    /// and raises the queued notifications. Rethrows an exception of the worker.
    /// </summary>
    static void RunConcurrently(TestUiThread ui, ObservableList<int> list, Action uiAction)
    {
        Exception workerError = null;

        var worker = new Thread(() =>
        {
            try
            {
                var random = new Random(1);
                for (var i = 0; i < WorkerChanges; i++)
                {
                    ChangeRandomly(list, random);
                }
            }
            catch (Exception ex)
            {
                workerError = ex;
            }
        })
        {
            IsBackground = true,
        };

        worker.Start();

        var uiRandom = new Random(2);
        for (var round = 0; round < MinimumUiRounds || worker.IsAlive; round++)
        {
            // A change on the UI thread is raised synchronously, and raises the queued ones ahead.
            ui.Invoke(() =>
            {
                ChangeRandomly(list, uiRandom);
                uiAction?.Invoke();
            });

            ui.Pump();
        }

        worker.Join();
        workerError.Should().BeNull();

        ui.Pump();
    }

    /// <summary>
    /// Verifies that the visible content stays consistent with the notifications while a worker thread and the UI
    /// thread keep changing the source at the same time, and the UI thread keeps raising the queued notifications.
    /// This exercises the lock between the writer side and the visible side under contention, including the
    /// notifications that a change on the UI thread raises ahead. Finally the visible content must match the source.
    /// </summary>
    [Fact]
    public void ConcurrentChangesKeepContentConsistent()
    {
        using var ui = new TestUiThread("ui");

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        NotifyCollectionChangedContractTracker<string> tracker = null!;

        ui.Invoke(() =>
        {
            notify = view.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(ui.Context));
            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
        });

        try
        {
            RunConcurrently(ui, list, null);

            string[] violations = null;
            string[] content = null;
            ui.Invoke(() =>
            {
                violations = tracker.Violations.ToArray();
                content = notify.ToArray();
            });

            violations.Should().BeEmpty();
            content.Should().Equal(list.Select(x => $"${x}"));
        }
        finally
        {
            notify.Dispose();
        }
    }

    /// <summary>
    /// Verifies that the visible content converges to the source while a subscriber comes and goes during concurrent
    /// changes. Without a subscriber, a change is applied at once; with one, it is queued. This exercises the switch
    /// between the two while changes are pending.
    /// </summary>
    [Fact]
    public void SubscribingAndUnsubscribingDuringChangesConverges()
    {
        using var ui = new TestUiThread("ui");

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        ui.Invoke(() => notify = view.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(ui.Context)));

        NotifyCollectionChangedEventHandler handler = (_, _) => { };
        var subscribed = false;

        try
        {
            RunConcurrently(ui, list, () =>
            {
                if (subscribed)
                {
                    notify.CollectionChanged -= handler;
                }
                else
                {
                    notify.CollectionChanged += handler;
                }

                subscribed = !subscribed;
            });

            string[] content = null;
            ui.Invoke(() => content = notify.ToArray());

            content.Should().Equal(list.Select(x => $"${x}"));
        }
        finally
        {
            notify.Dispose();
        }
    }
}
