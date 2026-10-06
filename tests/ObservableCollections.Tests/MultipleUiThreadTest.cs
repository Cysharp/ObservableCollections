using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Tests;

/// <summary>
/// When a process has more than one thread with a SynchronizationContext.
/// SynchronizationContextCollectionEventDispatcher must decide whether to raise synchronously by
/// "is SynchronizationContext.Current the one I am bound to", not by "is SynchronizationContext.Current null".
/// </summary>
public class MultipleUiThreadTest
{
    static readonly NotifyCollectionChangedAction[] AddThenRemove = new[]
    {
        NotifyCollectionChangedAction.Add,
        NotifyCollectionChangedAction.Remove,
    };

    /// <summary>
    /// A change made on another UI thread must be deferred to the bound UI thread.
    /// If a non-null Current were the condition for raising synchronously, the notification would be
    /// raised directly on the mutating thread.
    /// </summary>
    [Fact]
    public void MutationOnAnotherUiThread()
    {
        using var ui1 = new TestUiThread("ui1");
        using var ui2 = new TestUiThread("ui2");

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        NotifyCollectionChangedContractTracker<string> tracker = null!;
        var raisedOn = new List<int>();

        ui1.Invoke(() =>
        {
            notify = view.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(ui1.Context));
            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
            notify.CollectionChanged += (_, _) => raisedOn.Add(Environment.CurrentManagedThreadId);
        });

        try
        {
            ui2.Invoke(() =>
            {
                list.Add(10);
                list.RemoveAt(0);
            });

            // ui2 also has a SynchronizationContext, but the notifications must go to the bound ui1.
            ui1.PendingCount.Should().Be(2);
            ui2.PendingCount.Should().Be(0);
            tracker.Actions.Should().BeEmpty();

            ui1.Pump();

            tracker.Actions.Should().Equal(AddThenRemove);
            tracker.Violations.Should().BeEmpty();

            raisedOn.Should().Equal(new[] { ui1.ManagedThreadId, ui1.ManagedThreadId });
        }
        finally
        {
            notify.Dispose();
        }
    }

    /// <summary>
    /// When views of the same source are bound to different UI threads, only the one bound to the
    /// mutating thread raises synchronously.
    /// </summary>
    [Fact]
    public void EachViewNotifiesItsOwnUiThread()
    {
        using var ui1 = new TestUiThread("ui1");
        using var ui2 = new TestUiThread("ui2");

        var list = new ObservableList<int>();
        using var view1 = list.CreateView(x => $"${x}");
        using var view2 = list.CreateView(x => $"#{x}");

        NotifyCollectionChangedSynchronizedViewList<string> notify1 = null!;
        NotifyCollectionChangedSynchronizedViewList<string> notify2 = null!;
        NotifyCollectionChangedContractTracker<string> tracker1 = null!;
        NotifyCollectionChangedContractTracker<string> tracker2 = null!;

        ui1.Invoke(() =>
        {
            notify1 = view1.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(ui1.Context));
            tracker1 = new NotifyCollectionChangedContractTracker<string>(notify1);
        });

        ui2.Invoke(() =>
        {
            notify2 = view2.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(ui2.Context));
            tracker2 = new NotifyCollectionChangedContractTracker<string>(notify2);
        });

        try
        {
            ui1.Invoke(() =>
            {
                list.Add(10);
                list.RemoveAt(0);
            });

            // The change was made on ui1's own thread, so ui1 raises synchronously.
            ui1.PendingCount.Should().Be(0);
            tracker1.Actions.Should().Equal(AddThenRemove);

            // From ui2's point of view the change came from another thread, so it is deferred.
            ui2.PendingCount.Should().Be(2);
            tracker2.Actions.Should().BeEmpty();

            ui2.Pump();

            tracker2.Actions.Should().Equal(AddThenRemove);

            tracker1.Violations.Should().BeEmpty();
            tracker2.Violations.Should().BeEmpty();
        }
        finally
        {
            notify1.Dispose();
            notify2.Dispose();
        }
    }
}
