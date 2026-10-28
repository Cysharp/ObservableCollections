using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

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
    /// A dispatcher created off the bound thread learns the bound thread from the context itself.
    /// Until the context runs the posted probe, changes on the UI thread are only deferred; after that, they are
    /// raised synchronously, while changes from another thread are still deferred.
    /// </summary>
    [Fact]
    public void DispatcherCreatedOffBoundThreadLearnsBoundThread()
    {
        using var ui = new TestUiThread("ui", renewContextPerOperation: true);

        var list = new ObservableList<int>();
        using var view = list.CreateView(x => $"${x}");

        // Created on the test thread, so the dispatcher cannot tell the bound thread yet.
        var dispatcher = new SynchronizationContextCollectionEventDispatcher(ui.Context);
        ui.PendingCount.Should().Be(1);

        NotifyCollectionChangedSynchronizedViewList<string> notify = null!;
        NotifyCollectionChangedContractTracker<string> tracker = null!;

        ui.Invoke(() =>
        {
            notify = view.ToNotifyCollectionChanged(dispatcher);
            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
        });

        try
        {
            // Not resolved yet, so even a change on the UI thread is deferred.
            ui.Invoke(() => list.Add(1));

            ui.PendingCount.Should().Be(2);
            tracker.Actions.Should().BeEmpty();

            ui.Pump();

            tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add });

            // Resolved, so a change on the UI thread is raised synchronously.
            ui.Invoke(() => list.Add(2));

            ui.PendingCount.Should().Be(0);
            tracker.Actions.Should().HaveCount(2);

            // A change from another thread is still deferred.
            Task.Run(() => list.Add(3)).Wait();

            ui.PendingCount.Should().Be(1);
            tracker.Actions.Should().HaveCount(2);

            ui.Pump();

            tracker.Actions.Should().HaveCount(3);
            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$1", "$2", "$3" });
        }
        finally
        {
            notify.Dispose();
        }
    }
}
