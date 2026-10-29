using System.Collections.Specialized;
using System.Threading;

namespace ObservableCollections.Tests;

/// <summary>
/// When the UI thread does not keep a single SynchronizationContext instance.
/// WPF installs a new DispatcherSynchronizationContext for every dispatcher operation, so
/// SynchronizationContextCollectionEventDispatcher must decide whether to raise synchronously by the thread,
/// not by the instance of SynchronizationContext.Current.
/// </summary>
public class RenewedSynchronizationContextTest
{
    /// <summary>
    /// A change made in a later operation on the bound UI thread must be raised synchronously, and the view must
    /// reflect it immediately, even though SynchronizationContext.Current is no longer the instance the dispatcher
    /// was created with.
    /// </summary>
    [Fact]
    public void MutationInAnotherOperationOnBoundThreadIsSynchronous()
    {
        using var ui = new TestUiThread("ui", renewContextPerOperation: true);

        var list = new ObservableList<int>();
        list.Add(1);

        using var view = list.CreateView(x => $"${x}");

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        NotifyCollectionChangedContractTracker<string> tracker = null!;
        SynchronizationContext boundContext = null!;

        ui.Invoke(() =>
        {
            boundContext = SynchronizationContext.Current!;
            notify = view.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(boundContext));
            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
        });

        try
        {
            var countRightAfterAdd = -1;

            // e.g. a click handler, which WPF runs under another DispatcherSynchronizationContext instance.
            ui.Invoke(() =>
            {
                SynchronizationContext.Current.Should().NotBeSameAs(boundContext);

                list.Add(2);
                countRightAfterAdd = notify.Count;
            });

            countRightAfterAdd.Should().Be(2);
            ui.PendingCount.Should().Be(0);
            tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add });
            tracker.Violations.Should().BeEmpty();
        }
        finally
        {
            notify.Dispose();
        }
    }

    /// <summary>
    /// A dispatcher created off the bound thread cannot tell the bound thread, and must not guess it by posting
    /// anything to the context. Verifies that it posts nothing when created, that it then raises a change on the UI
    /// thread under a renewed context instance by posting it, which only delays it, and that the visible content
    /// still stays consistent with the notifications.
    /// </summary>
    [Fact]
    public void DispatcherCreatedOffBoundThreadPostsChangesOnUiThread()
    {
        using var ui = new TestUiThread("ui", renewContextPerOperation: true);

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");

        // Created on the test thread, so the dispatcher cannot tell the bound thread.
        var dispatcher = new SynchronizationContextCollectionEventDispatcher(ui.Context);
        ui.PendingCount.Should().Be(0);

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        NotifyCollectionChangedContractTracker<string> tracker = null!;

        ui.Invoke(() =>
        {
            notify = view.ToNotifyCollectionChanged(dispatcher);
            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
        });

        try
        {
            var countRightAfterAdd = -1;

            ui.Invoke(() =>
            {
                list.Add(1);
                countRightAfterAdd = notify.Count;
            });

            // Deferred, and the view does not show the change ahead of its notification.
            countRightAfterAdd.Should().Be(0);
            ui.PendingCount.Should().Be(1);
            tracker.Actions.Should().BeEmpty();

            ui.Pump();

            tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add });
            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$1" });
        }
        finally
        {
            notify.Dispose();
        }
    }
}
