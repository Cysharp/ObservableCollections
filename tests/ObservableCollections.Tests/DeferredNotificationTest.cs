using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
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
    /// Verifies that changes made on a worker thread to a view without a filter (NonFilteredSynchronizedViewList)
    /// are deferred, and that whenever a notification is raised, the visible content matches the content rebuilt
    /// from the notifications so far.
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
    /// Verifies that RemoveAt while a notification is pending removes the element the caller sees at the index,
    /// even though a pending Insert has shifted it in the source, and that the notifications then rebuild the
    /// resulting content.
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

    /// <summary>
    /// Verifies that Insert while a notification is pending takes the index in the content the caller sees, and
    /// inserts right before the element at that index even though a pending Insert has shifted the source.
    /// Also verifies that the notifications then rebuild that content.
    /// </summary>
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
    /// Verifies that RemoveAt on a filtered view while a notification is pending removes the element the caller sees
    /// at the index. The index is translated through the pending changes, and then from the view into the source.
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
    /// Verifies that RemoveAt throws InvalidOperationException and leaves the source unchanged when a pending change
    /// has already removed the element the caller sees at the index. No element is left to translate the index to,
    /// and removing another one would be wrong.
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

        // Subscribe, so that the changes are kept pending instead of being applied at once.
        notify.CollectionChanged += (_, _) => { };

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

        // Subscribe, so that the changes are kept pending instead of being applied at once.
        notify.CollectionChanged += (_, _) => { };

        Task.Run(() =>
        {
            list.Clear();
            list.Add(3);
        }).Wait();

        notify.Invoking(x => x.RemoveAt(0))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The collection has been reset*");

        // Unlike across a Remove, even an insertion position cannot be translated across a Reset.
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

        // Subscribe, as a view bound to a UI would be.
        notify.CollectionChanged += (_, _) => { };

        notify.Invoking(x => x.Insert(3, "$9")).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x.Insert(-1, "$9")).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x.RemoveAt(2)).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x.RemoveAt(-1)).Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x[2] = "$9").Should().Throw<ArgumentOutOfRangeException>();
        notify.Invoking(x => x[-1] = "$9").Should().Throw<ArgumentOutOfRangeException>();

        list.Should().Equal(new[] { 1, 2 });
        dispatcher.PendingCount.Should().Be(0);
    }

    /// <summary>
    /// Verifies that a Reset is deferred together with the changes around it. The visible content stays as it was
    /// until the notifications are raised, and then becomes the content after the last change, built on the
    /// snapshot that the Reset took when it was posted.
    /// </summary>
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

    /// <summary>
    /// Verifies that a Move is deferred. The visible content keeps the old order until the notification is raised,
    /// and then shows the moved order.
    /// </summary>
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
    /// Verifies that a change on the UI thread, which the dispatcher raises synchronously, does not overtake the
    /// notifications queued before it from another thread. The queued ones are raised first within the same call,
    /// and are not raised again later.
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
    /// A change on the UI thread while changes from another thread are queued raises the older notifications
    /// ahead, within the same call. Verifies that an exception thrown by a subscriber of such an older notification
    /// drops neither that notification nor the change on the UI thread, that it does not surface at the
    /// unrelated writer, and that it is thrown by the own callback of the older notification instead, where it
    /// would have been thrown without being raised ahead.
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

            // The UI thread, so raised synchronously. The queued Reset is raised ahead here, and its handler throws,
            // but the exception must not surface at this unrelated Add.
            ui.Invoke(() => list.Add(2));

            thrownCount.Should().Be(1);

            // Neither the notification nor the application of the Add may be lost.
            tracker.Actions.Should().Equal(new[]
            {
                NotifyCollectionChangedAction.Reset,
                NotifyCollectionChangedAction.Add,
            });

            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$2" });

            // The own callback of the Reset throws the exception of its handler, and raises nothing again.
            ui.Invoking(x => x.Pump()).Should().Throw<InvalidOperationException>()
                .WithInnerException<InvalidOperationException>().WithMessage("subscriber");

            thrownCount.Should().Be(1);
            tracker.Actions.Should().HaveCount(2);
            ui.PendingCount.Should().Be(0);
        }
        finally
        {
            notify.Dispose();
        }
    }

    /// <summary>
    /// Without range notifications, a range change is applied and raised element by element.
    /// Verifies that an exception thrown by a subscriber of an older notification, which the first element raises
    /// ahead, does not interrupt the range change, so that every element reaches the view and its subscribers.
    /// If it were thrown there, the remaining elements would be missing from the view for good.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SubscriberExceptionDoesNotInterruptRangeChange(bool viaNonFilteredList)
    {
        using var ui = new TestUiThread("ui");

        var list = new ObservableList<int>();
        list.Add(1);

        using var view = list.CreateView(x => $"${x}");

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        NotifyCollectionChangedContractTracker<string> tracker = null!;

        // Mimics a handler that does not expect NewItems to be null on Reset.
        static void Thrower(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                throw new InvalidOperationException("subscriber");
            }
        }

        ui.Invoke(() =>
        {
            var dispatcher = new SynchronizationContextCollectionEventDispatcher(ui.Context);
            notify = viaNonFilteredList
                ? list.ToNotifyCollectionChanged(x => $"${x}", dispatcher)
                : view.ToNotifyCollectionChanged(dispatcher);

            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
            notify.CollectionChanged += Thrower;
        });

        try
        {
            Task.Run(() => list.Clear()).Wait(); // Another thread, so the Reset is queued

            ui.PendingCount.Should().Be(1);

            ui.Invoke(() => list.AddRange(new[] { 2, 3 }));

            tracker.Actions.Should().Equal(new[]
            {
                NotifyCollectionChangedAction.Reset,
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Add,
            });

            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$2", "$3" });

            ui.Invoking(x => x.Pump()).Should().Throw<InvalidOperationException>()
                .WithInnerException<InvalidOperationException>().WithMessage("subscriber");

            // The view keeps tracking the source after that.
            ui.Invoke(() => list.RemoveAt(1));

            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$2" });
        }
        finally
        {
            notify.Dispose();
        }
    }

    /// <summary>
    /// CopyTo must copy the content that the subscribers have been notified of, the same content as Count and the
    /// indexer return. If it copied the writer-side content, a destination sized by Count would be too short while an
    /// Add is pending, and a larger one would expose an element that no notification has announced yet.
    /// Covers both implementations: the one created from a view (FiltableSynchronizedViewList) and the one created
    /// from the list itself (NonFilteredSynchronizedViewList).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyToCopiesPublishedContent(bool viaNonFilteredList)
    {
        var list = new ObservableList<int>();
        list.Add(1);

        var dispatcher = new QueuedCollectionEventDispatcher();

        using var view = list.CreateView(x => $"${x}");
        using var notify = viaNonFilteredList
            ? list.ToNotifyCollectionChanged(x => $"${x}", dispatcher)
            : view.ToNotifyCollectionChanged(dispatcher);

        notify.CollectionChanged += (_, _) => { };

        list.Add(2);

        notify.Count.Should().Be(1);

        var destination = new string[notify.Count];
        notify.CopyTo(destination.AsSpan());
        destination.Should().Equal(new[] { "$1" });

        dispatcher.Pump();

        destination = new string[notify.Count];
        notify.CopyTo(destination.AsSpan());
        destination.Should().Equal(new[] { "$1", "$2" });
    }

    /// <summary>
    /// Verifies that Insert succeeds when a pending Remove has removed the element right after the insertion point.
    /// Unlike the element itself, the position between the elements survives the removal: the item goes where the
    /// removed element was, and the notifications then rebuild that content.
    /// </summary>
    [Fact]
    public void InsertAtPositionOfPendingRemove()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3 });
        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.RemoveAt(1)).Wait(); // 2 is removed

        // Right before "$2" of the visible ["$1", "$2", "$3"], which is no longer in the source.
        notify.Insert(1, "$9");

        list.Should().Equal(new[] { 1, 9, 3 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$1", "$9", "$3" });
    }

    /// <summary>
    /// Verifies that Insert succeeds when pending Removes have removed several elements around the insertion point.
    /// The item goes where the removed elements were.
    /// </summary>
    [Fact]
    public void InsertAtPositionOfPendingRemoveRange()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });
        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.RemoveRange(0, 2)).Wait(); // 1 and 2 are removed

        // Right before "$2" of the visible ["$1", "$2", "$3", "$4"], that is, between two removed elements.
        notify.Insert(1, "$9");

        list.Should().Equal(new[] { 9, 3, 4 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$9", "$3", "$4" });
    }

    /// <summary>
    /// Verifies that the setter on a filtered view while a notification is pending replaces the element the caller
    /// sees at the index. The index is translated through the pending changes, and then from the view into the source.
    /// </summary>
    [Fact]
    public void SetDuringPendingNotification_Filter()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0); // ["$2", "$4"]

        using var notify = view.ToWritableNotifyCollectionChanged(ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 6)).Wait(); // Passes the filter, so it goes to the head of the view

        notify[1] = "$40"; // "$4" of the visible ["$2", "$4"]

        list.Should().Equal(new[] { 6, 1, 2, 3, 40 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$6", "$2", "$40" });
    }

    /// <summary>
    /// Verifies that, on a filtered view while a notification is pending, a write that the converter rejects changes
    /// the view of the element the caller sees at the index, and nothing else, and leaves the source unchanged.
    /// </summary>
    [Fact]
    public void RejectedSetDuringPendingNotification_Filter()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0); // ["$2", "$4"]

        using var notify = view.ToWritableNotifyCollectionChanged(RejectOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 6)).Wait();

        notify[1] = "$40"; // "$4" of the visible ["$2", "$4"]

        list.Should().Equal(new[] { 6, 1, 2, 3, 4 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$6", "$2", "$40" });
    }

    /// <summary>
    /// Verifies that Insert on a filtered view while a notification is pending inserts right before the element the
    /// caller sees at the index, and appends to the source when the index is the tail of the view.
    /// </summary>
    [Theory]
    [InlineData(1, new[] { 6, 1, 2, 3, 8, 4 }, new[] { "$6", "$2", "$8", "$4" })]
    [InlineData(2, new[] { 6, 1, 2, 3, 4, 8 }, new[] { "$6", "$2", "$4", "$8" })]
    public void InsertDuringPendingNotification_Filter(int index, int[] expectedSource, string[] expectedView)
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0); // ["$2", "$4"]

        using var notify = view.ToWritableNotifyCollectionChanged(ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Insert(0, 6)).Wait();

        notify.Insert(index, "$8");

        list.Should().Equal(expectedSource);

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(expectedView);
    }

    /// <summary>
    /// Verifies that changing the filter while notifications are pending is deferred like any other change. The
    /// filter change raises a Reset: until it is raised, the visible content stays as it was and no index can be
    /// translated, and once it is raised, the visible content is the one that the filter selects.
    /// </summary>
    [Fact]
    public void FilterChangeIsDeferred()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        using var view = list.CreateWritableView(x => $"${x}");
        using var notify = view.ToWritableNotifyCollectionChanged(ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            list.Add(5);
            view.AttachFilter(x => x % 2 == 0);
        }).Wait();

        notify.Should().Equal(new[] { "$1", "$2", "$3", "$4" });
        notify.Invoking(x => x.RemoveAt(0))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The collection has been reset*");

        dispatcher.Pump();

        tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Reset });
        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$2", "$4" });

        Task.Run(() => view.ResetFilter()).Wait();

        notify.Should().Equal(new[] { "$2", "$4" });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$1", "$2", "$3", "$4", "$5" });
    }

    /// <summary>
    /// Verifies that RemoveAt while range changes are pending removes the element the caller sees at the index.
    /// Without range notifications, a view with a dispatcher queues a range change as one change per element, and
    /// the index is translated through all of them.
    /// </summary>
    [Fact]
    public void RemoveAtDuringPendingRangeChanges()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3 });
        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            list.InsertRange(1, new[] { 10, 11 });
            list.RemoveRange(4, 1); // 3
        }).Wait();

        dispatcher.PendingCount.Should().Be(3);

        notify.RemoveAt(1); // "$2" of the visible ["$1", "$2", "$3"]

        list.Should().Equal(new[] { 1, 10, 11 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$1", "$10", "$11" });
    }

    /// <summary>
    /// Verifies that the setter while a Move is pending replaces the element the caller sees at the index, following
    /// the moved element to its new position in the source.
    /// </summary>
    [Fact]
    public void SetDuringPendingMove()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3 });
        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Move(0, 2)).Wait(); // [2, 3, 1]

        notify[0] = "$10"; // "$1" of the visible ["$1", "$2", "$3"]

        list.Should().Equal(new[] { 2, 3, 10 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$2", "$3", "$10" });
    }

    /// <summary>
    /// Verifies that RemoveAt while a Move is pending removes the element the caller sees at the index, which the
    /// Move has shifted in the source.
    /// </summary>
    [Fact]
    public void RemoveAtDuringPendingMove()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3 });
        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Move(0, 2)).Wait(); // [2, 3, 1]

        notify.RemoveAt(1); // "$2" of the visible ["$1", "$2", "$3"]

        list.Should().Equal(new[] { 3, 1 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$3", "$1" });
    }

    /// <summary>
    /// Verifies that Insert right before an element that a pending Move takes elsewhere does not follow the moved
    /// element, and keeps its position among the other elements, as documented on DeferredViewList.ToWriterIndex.
    /// </summary>
    [Fact]
    public void InsertDuringPendingMove()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1, 2, 3 });
        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Move(0, 2)).Wait(); // [2, 3, 1]

        notify.Insert(0, "$9"); // right before "$1" of the visible ["$1", "$2", "$3"]

        list.Should().Equal(new[] { 9, 2, 3, 1 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$9", "$2", "$3", "$1" });
    }

    /// <summary>
    /// Verifies that the visible content of a view over a dictionary is deferred too. A Replace from a dictionary
    /// carries no index, so the view locates the element by its old value; the notifications must still rebuild the
    /// content.
    /// </summary>
    [Fact]
    public void DictionaryChangesAreDeferred()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var dictionary = new ObservableDictionary<int, string>();
        dictionary.Add(1, "a");
        dictionary.Add(2, "b");

        using var view = dictionary.CreateView(x => $"{x.Key}={x.Value}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            dictionary[1] = "c";
            dictionary.Add(3, "d");
            dictionary.Remove(2);
        }).Wait();

        notify.Should().Equal(new[] { "1=a", "2=b" });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "1=c", "3=d" });
    }

    /// <summary>
    /// Verifies that the visible content of a view over a queue is deferred too, when elements are added at the tail
    /// and removed from the head.
    /// </summary>
    [Fact]
    public void QueueChangesAreDeferred()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var queue = new ObservableQueue<int>();
        queue.Enqueue(1);
        queue.Enqueue(2);

        using var view = queue.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            queue.Enqueue(3);
            queue.Dequeue();
        }).Wait();

        notify.Should().Equal(new[] { "$1", "$2" });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$2", "$3" });
    }

    /// <summary>
    /// Verifies that the visible content of a view over a stack is deferred too, when elements are pushed to and
    /// popped from the head.
    /// </summary>
    [Fact]
    public void StackChangesAreDeferred()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var stack = new ObservableStack<int>();
        stack.Push(1);
        stack.Push(2);

        using var view = stack.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            stack.Push(3);
            stack.Pop();
            stack.Push(4);
        }).Wait();

        notify.Should().Equal(new[] { "$2", "$1" });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$4", "$2", "$1" });
    }

    /// <summary>
    /// Verifies that the visible content of a view over a ring buffer is deferred too, when elements are added and
    /// removed at both ends.
    /// </summary>
    [Fact]
    public void RingBufferChangesAreDeferred()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var buffer = new ObservableRingBuffer<int>(new[] { 1, 2 });

        using var view = buffer.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() =>
        {
            buffer.AddFirst(0);
            buffer.AddLast(3);
            buffer.RemoveFirst();
        }).Wait();

        notify.Should().Equal(new[] { "$1", "$2" });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$1", "$2", "$3" });
    }

    /// <summary>
    /// Verifies that a subscriber of PropertyChanged alone also makes the view defer its content, so that Count read
    /// in the handler is the count of the content notified so far. Also verifies that a write that the converter
    /// rejects raises no PropertyChanged, since it does not change the count.
    /// </summary>
    [Fact]
    public void PropertyChangedOnlySubscriberSeesNotifiedCount()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1 });
        using var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", RejectOriginal, dispatcher);

        var counts = new List<int>();
        notify.PropertyChanged += (_, _) => counts.Add(notify.Count);

        Task.Run(() =>
        {
            list.Add(2);
            list.Add(3);
        }).Wait();

        notify.Count.Should().Be(1);

        dispatcher.Pump();

        counts.Should().Equal(new[] { 2, 3 });

        notify[0] = "$9"; // Rejected by the converter, so only the view changes

        dispatcher.Pump();

        counts.Should().Equal(new[] { 2, 3 });
        notify.Should().Equal(new[] { "$9", "$2", "$3" });
    }

    /// <summary>
    /// Pins what Dispose does to the notifications that are still pending. A notification posted before Dispose is
    /// still raised and applied when the dispatcher runs it, so that the content seen by a subscriber stays
    /// consistent with what it has been told. A change of the source after Dispose no longer reaches the view.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisposeKeepsPendingNotifications(bool viaNonFilteredList)
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var list = new ObservableList<int>(new[] { 1 });

        using var view = list.CreateView(x => $"${x}");
        var notify = viaNonFilteredList
            ? list.ToNotifyCollectionChanged(x => $"${x}", dispatcher)
            : view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

        Task.Run(() => list.Add(2)).Wait();

        notify.Dispose();

        list.Add(3);

        dispatcher.PendingCount.Should().Be(1);

        dispatcher.Pump();

        tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add });
        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$1", "$2" });
    }

    /// <summary>
    /// A dispatcher that only records the posted events, so that a test can invoke them in any order and on any
    /// thread, as a dispatcher outside the single-thread contract might.
    /// </summary>
    sealed class RecordingDispatcher : ICollectionEventDispatcher
    {
        public List<CollectionEventDispatcherEventArgs> Posted { get; } = new();

        public void Post(CollectionEventDispatcherEventArgs ev)
        {
            lock (Posted)
            {
                Posted.Add(ev);
            }
        }
    }

    /// <summary>
    /// Verifies that no subscriber exception is lost when a dispatcher outside the single-thread contract runs the
    /// callbacks out of order on several threads. The callback of a newer event raises an older one ahead, and the
    /// own callback of the older one completes on another thread meanwhile. Its exception then has no callback left
    /// to be rethrown from, so it is thrown here together with the exception of the newer one, as an
    /// AggregateException.
    /// </summary>
    [Fact]
    public void ExceptionsAreNotLostWhenCallbacksRunOutOfOrder()
    {
        var dispatcher = new RecordingDispatcher();

        var list = new ObservableList<int>();

        using var view = list.CreateView(x => $"${x}");
        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        CollectionEventDispatcherEventArgs older = null;
        Exception olderCallbackError = null;

        notify.CollectionChanged += (_, e) =>
        {
            if ((string)e.NewItems![0]! == "$1")
            {
                // The own callback of the older event runs on another thread while the event is raised ahead.
                var thread = new Thread(() =>
                {
                    try
                    {
                        older.Invoke();
                    }
                    catch (Exception ex)
                    {
                        olderCallbackError = ex;
                    }
                });

                thread.Start();
                thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue();

                throw new InvalidOperationException("older");
            }

            throw new InvalidOperationException("newer");
        };

        list.Add(1);
        list.Add(2);

        older = dispatcher.Posted[0];
        var newer = dispatcher.Posted[1];

        var error = Record.Exception(() => newer.Invoke());

        olderCallbackError.Should().BeNull();
        error.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Select(x => x.Message).Should().Equal(new[] { "older", "newer" });

        notify.Should().Equal(new[] { "$1", "$2" });
    }
}
