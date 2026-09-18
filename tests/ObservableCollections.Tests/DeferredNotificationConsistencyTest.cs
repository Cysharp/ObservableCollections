using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace ObservableCollections.Tests;

/// <summary>
/// ICollectionEventDispatcher が通知を UI スレッドへ遅延させる場合、
/// UI スレッドが通知を処理する時点で通知の内容とコレクションの状態が一致していなければならない。
/// 内部リストの更新だけが変更したスレッド上で先に進むと、この一致が崩れる。
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

        // Wait することで「UI スレッドが通知を処理する前に両方の変更が完了している」状態を確定させる。
        Task.Run(() =>
        {
            list.Add(10);
            list.RemoveAt(0);
        }).Wait();

        // 通知が遅延していることの確認。これが 0 ならテストは何も検証していない。
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
            list.Insert(0, 6); // フィルタを通るので view の先頭に入る
            list.RemoveAt(4);  // 元の 4。view の末尾から消える
        }).Wait();

        dispatcher.PendingCount.Should().Be(2);

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();

        notify.Should().Equal(new[] { "$6", "$2" });
    }

    /// <summary>
    /// 比較用。UI スレッド上で変更すれば SynchronizationContextCollectionEventDispatcher は
    /// 同期発火を選ぶので破綻しない。修正後もこちらが壊れないことを保証する。
    /// </summary>
    [Fact]
    public void UiThreadMutation()
    {
        var context = new QueuedSynchronizationContext();
        var previous = SynchronizationContext.Current;

        // SynchronizationContextCollectionEventDispatcher は静的初期化で SynchronizationContext.Current を
        // 要求する (ICollectionEventDispatcher.cs の static readonly Current) ため、
        // この型に触る前に設定しておく必要がある。
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var list = new ObservableList<int>();
            using var view = list.CreateView(x => $"${x}");
            using var notify = view.ToNotifyCollectionChanged(new SynchronizationContextCollectionEventDispatcher(context));

            var tracker = new NotifyCollectionChangedContractTracker<string>(notify);

            list.Add(10);
            list.RemoveAt(0);

            // 同期発火が選ばれたので、キューには何も溜まっていない。
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
