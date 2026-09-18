using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Tests;

/// <summary>
/// SynchronizationContext を持つスレッドがプロセス内に複数ある場合。
/// SynchronizationContextCollectionEventDispatcher は「SynchronizationContext.Current が null か否か」ではなく
/// 「自分が束縛された SynchronizationContext と同一か」で同期発火を判定しなければならない。
/// </summary>
public class MultipleUiThreadTest
{
    static readonly NotifyCollectionChangedAction[] AddThenRemove = new[]
    {
        NotifyCollectionChangedAction.Add,
        NotifyCollectionChangedAction.Remove,
    };

    /// <summary>
    /// 別の UI スレッドからの変更は、束縛先の UI スレッドへ遅延しなければならない。
    /// Current が null でないことを同期発火の条件にすると、変更したスレッド上で直接発火してしまう。
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

            // ui2 も SynchronizationContext を持つが、通知先は束縛された ui1 でなければならない。
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
    /// 同じソースを別々の UI スレッドに束縛した場合、変更したスレッドの側だけが同期発火する。
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

            // ui1 は自分のスレッド上での変更なので同期発火。
            ui1.PendingCount.Should().Be(0);
            tracker1.Actions.Should().Equal(AddThenRemove);

            // ui2 から見れば別スレッドからの変更なので遅延。
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
