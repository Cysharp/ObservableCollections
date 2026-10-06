using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace ObservableCollections.Tests;

/// <summary>
/// When an ICollectionEventDispatcher defers notifications, the content of the list as seen by subscribers
/// must also be deferred until the notifications are raised (issue #115).
/// </summary>
public class DeferredNotificationTest
{
    static int ToOriginal(string newView, int original, ref bool setValue)
    {
        setValue = true;
        return int.Parse(newView.Substring(1));
    }

    static int RejectOriginal(string newView, int original, ref bool setValue)
    {
        setValue = false;
        return original;
    }

    sealed class Flagged
    {
        public bool Visible { get; set; }
    }

    /// <summary>
    /// When the filter evaluates differently on add and on remove, a remove notification arrives for an
    /// element that is not in the view.
    /// Verifies that subscribers are not notified, since the content of the view has not changed.
    /// Queuing a notification with an unknown index (-1) could not be applied when raised, and the
    /// content would stay inconsistent forever.
    /// </summary>
    [Fact]
    public void RemoveOfItemMissingFromViewIsNotNotified()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var item = new Flagged { Visible = false };

        var set = new ObservableHashSet<Flagged>();
        set.Add(item);

        using var view = set.CreateView(x => x);
        view.AttachFilter(x => x.Visible); // item is filtered out, so it is not in the view

        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<Flagged>(notify);

        item.Visible = true; // The view does not re-evaluate, so it stays empty

        set.Remove(item); // The filter is true on remove, so a Remove notification comes through

        dispatcher.Pump();

        tracker.Actions.Should().BeEmpty();
        tracker.Violations.Should().BeEmpty();
        notify.Should().BeEmpty();
    }

    /// <summary>
    /// The same guarantee is required for a view without a filter (NonFilteredSynchronizedViewList).
    /// </summary>
    [Fact]
    public void NonFiltered_WorkerThreadMutation()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        using var notify = list.ToNotifyCollectionChanged(x => $"${x}", dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            list.Add(10);
            list.Insert(0, 20);
            list.RemoveAt(1);
        }).Wait();

        dispatcher.PendingCount.Should().Be(3);
        notify.Should().BeEmpty();

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$20" });
    }

    /// <summary>
    /// Without subscribers there is no notification to stay consistent with, so changes may be applied
    /// immediately.
    /// Nothing is queued, so a later subscriber does not receive past notifications.
    /// </summary>
    [Fact]
    public void NoSubscriber_AppliedImmediately()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        Task.Run(() => list.Add(10)).Wait();

        dispatcher.PendingCount.Should().Be(0);
        notify.Should().Equal(new[] { "$10" });

        // Changes after the subscription starts are deferred.
        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Add(20)).Wait();

        dispatcher.PendingCount.Should().Be(1);
        notify.Should().Equal(new[] { "$10" });

        dispatcher.Pump();

        tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add });
        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$10", "$20" });
    }

    /// <summary>
    /// Even after unsubscribing, deferral continues until the notifications left in the queue are raised.
    /// Switching to immediate application midway would reorder the applications and corrupt the content.
    /// </summary>
    [Fact]
    public void Unsubscribed_KeepsDeferringWhileNotificationIsPending()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);

        using var view = list.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        void Handler(object sender, NotifyCollectionChangedEventArgs e) { }
        notify.CollectionChanged += Handler;

        Task.Run(() => list.Insert(0, 2)).Wait();

        dispatcher.PendingCount.Should().Be(1);

        notify.CollectionChanged -= Handler;

        Task.Run(() => list.Insert(0, 3)).Wait();

        // There is no subscriber, but the earlier notification is still pending, so this is queued to keep the order.
        dispatcher.PendingCount.Should().Be(2);
        notify.Should().Equal(new[] { "$1" });

        dispatcher.Pump();

        notify.Should().Equal(new[] { "$3", "$2", "$1" });
    }

    /// <summary>
    /// A positional write during deferral translates the visible index through the pending changes before
    /// passing it to the source.
    /// </summary>
    [Fact]
    public void RemoveAtDuringPendingNotification()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 0)).Wait();

        dispatcher.PendingCount.Should().Be(1);

        // The visible content is ["$1", "$2", "$3", "$4"], so [1] is "$2".
        // In the source, 0 has been inserted at the head, so it is at index 2.
        notify.RemoveAt(1);

        list.Should().Equal(new[] { 0, 1, 3, 4 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$3", "$4" });
    }

    [Fact]
    public void InsertDuringPendingNotification()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 0)).Wait();

        // At [1] of the visible ["$1", "$2", "$3"], that is, right before "$2".
        notify.Insert(1, "$9");

        list.Should().Equal(new[] { 0, 1, 9, 2, 3 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$9", "$2", "$3" });
    }

    /// <summary>
    /// Verifies that a positional write during deferral keeps the content seen by subscribers consistent
    /// with the content reconstructed from the notifications, and does not add extra notifications.
    /// </summary>
    [Fact]
    public void SetDuringPendingNotification()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 0)).Wait();

        // Replaces [1] of the visible ["$1", "$2", "$3"], that is, "$2".
        notify[1] = "$9";

        list.Should().Equal(new[] { 0, 1, 9, 3 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$9", "$3" });

        // Does not notify twice alongside the Replace from the source.
        tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace });
    }

    /// <summary>
    /// Verifies that even when the converter rejects the write to the source and no Replace comes from
    /// the source, the content seen by subscribers stays consistent with the content reconstructed from
    /// the notifications.
    /// </summary>
    [Fact]
    public void SetRejectedByConverterDuringPendingNotification()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", RejectOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 0)).Wait();

        notify[1] = "$9";

        list.Should().Equal(new[] { 0, 1, 2 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$9" });
        tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace });
    }

    /// <summary>
    /// Also works on a filtered view, combined with the reverse lookup (view index → source index).
    /// </summary>
    [Fact]
    public void RemoveAtDuringPendingNotification_Filter()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0); // ["$2", "$4"]

        using var notify = view.ToWritableNotifyCollectionChanged(ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 6)).Wait(); // Passes the filter, so it goes to the head of the view

        dispatcher.PendingCount.Should().Be(1);

        // The visible content is ["$2", "$4"], so [1] is "$4".
        notify.RemoveAt(1);

        list.Should().Equal(new[] { 6, 1, 2, 3 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$6", "$2" });
    }

    /// <summary>
    /// When the target element itself has been removed by a pending change, there is nothing to translate
    /// to, so the operation fails.
    /// </summary>
    [Fact]
    public void RemoveAtDuringPendingNotification_TargetIsAlreadyRemoved()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        _ = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.RemoveAt(1)).Wait(); // 2 is removed

        // [1] of the visible ["$1", "$2", "$3"] no longer exists.
        notify.Invoking(x => x.RemoveAt(1))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The element at index 1 has been removed*");

        list.Should().Equal(new[] { 1, 3 });
    }

    /// <summary>
    /// With a pending Reset, no index can be translated.
    /// Verifies that this is not confused with the removal of a specific element, so that callers are
    /// not misled into thinking a different index would succeed.
    /// </summary>
    [Fact]
    public void WriteDuringPendingReset()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        _ = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            list.Clear();
            list.Add(3);
        }).Wait();

        // Even an insertion position cannot be translated across a Reset.
        notify.Invoking(x => x.RemoveAt(0))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The collection has been reset*");

        notify.Invoking(x => x.Insert(0, "$9"))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The collection has been reset*");

        notify.Invoking(x => x[0] = "$9")
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The collection has been reset*");

        list.Should().Equal(new[] { 3 });
    }

    /// <summary>
    /// An insertion at the tail of the visible list is treated as an append even with pending changes.
    /// </summary>
    [Fact]
    public void InsertAtTailDuringPendingNotification()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 0)).Wait();

        // The visible content is ["$1", "$2"], so [2] is the tail.
        notify.Insert(2, "$9");

        list.Should().Equal(new[] { 0, 1, 2, 9 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$2", "$9" });
    }

    /// <summary>
    /// The handling of out-of-range indices must not depend on whether there is a dispatcher.
    /// Confusing a -1 passed by the caller with the internal -1 that means "untrackable" would result in
    /// InvalidOperationException instead of ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void OutOfRangeIndexIsRejected()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);

        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        _ = new NotifyCollectionChangedContractTracker<string>(notify);

        notify.Invoking(x => x.Insert(3, "$9")).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x.Insert(-1, "$9")).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x.RemoveAt(2)).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x.RemoveAt(-1)).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x[2] = "$9").Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x[-1] = "$9").Should().Throw<ArgumentOutOfRangeException>();

        list.Should().Equal(new[] { 1, 2 });
        dispatcher.PendingCount.Should().Be(0);
    }

    [Fact]
    public void ResetIsDeferred()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);

        using var view = list.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            list.Add(3);
            list.Clear();
            list.Add(4);
        }).Wait();

        notify.Should().Equal(new[] { "$1", "$2" });

        dispatcher.Pump();

        tracker.Actions.Should().Equal(new[]
        {
            NotifyCollectionChangedAction.Add,
            NotifyCollectionChangedAction.Reset,
            NotifyCollectionChangedAction.Add,
        });

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$4" });
    }

    [Fact]
    public void MoveIsDeferred()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);

        using var view = list.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Move(0, 2)).Wait();

        notify.Should().Equal(new[] { "$1", "$2", "$3" });

        dispatcher.Pump();

        tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Move });
        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$2", "$3", "$1" });
    }

    /// <summary>
    /// When the same dispatcher mixes asynchronous and synchronous raising.
    /// A change on the UI thread is raised synchronously, but it must not overtake the notifications
    /// queued before it.
    /// </summary>
    [Fact]
    public void SynchronousNotificationDoesNotOvertakePendingOne()
    {
        using var ui = new TestUiThread("ui");

        var list = new ObservableList<int>();
        list.Add(1);

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
            Task.Run(() => list.Insert(0, 2)).Wait(); // Another thread, so deferred

            ui.PendingCount.Should().Be(1);

            ui.Invoke(() => list.Insert(0, 3)); // The UI thread, so raised synchronously

            // The deferred one is processed at the same time, so both have been raised.
            tracker.Actions.Should().Equal(new[]
            {
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Add,
            });

            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$3", "$2", "$1" });

            ui.Pump(); // No notification is left in the queue

            tracker.Actions.Should().HaveCount(2);
        }
        finally
        {
            notify.Dispose();
        }
    }

    /// <summary>
    /// Even when a subscriber throws an exception, notifications that have not been raised yet must not be dropped.
    /// A change on the UI thread while changes from another thread are queued raises the older notifications
    /// within the same call, so an exception there would leave the subsequent notifications unraised.
    /// </summary>
    [Fact]
    public void SubscriberExceptionDoesNotDropRemainingNotifications()
    {
        using var ui = new TestUiThread("ui");

        var list = new ObservableList<int>();
        list.Add(1);

        using var view = list.CreateView(x => $"${x}");

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        NotifyCollectionChangedContractTracker<string> tracker = null!;

        var thrownCount = 0;

        // Mimics a handler that does not expect NewItems to be null on Reset.
        void Thrower(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                thrownCount++;
                throw new InvalidOperationException("subscriber");
            }
        }

        ui.Invoke(() =>
        {
            notify = view.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(ui.Context));

            // The tracker subscribes first so that it records every notification.
            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
            notify.CollectionChanged += Thrower;
        });

        try
        {
            Task.Run(() => list.Clear()).Wait(); // Another thread, so the Reset is queued

            ui.PendingCount.Should().Be(1);

            Exception error = null;

            ui.Invoke(() =>
            {
                try
                {
                    list.Add(2); // The UI thread, so raised synchronously. The queued Reset is also raised here
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            // The exception thrown by the Reset handler surfaces at the caller of the unrelated Add.
            thrownCount.Should().Be(1);
            error.Should().BeOfType<InvalidOperationException>();

            // Neither the notification nor the application of the Add may be lost.
            tracker.Actions.Should().Equal(new[]
            {
                NotifyCollectionChangedAction.Reset,
                NotifyCollectionChangedAction.Add,
            });

            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$2" });

            ui.Pump(); // No notification is left behind

            tracker.Actions.Should().HaveCount(2);
        }
        finally
        {
            notify.Dispose();
        }
    }
}
