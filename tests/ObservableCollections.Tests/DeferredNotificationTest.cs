using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace ObservableCollections.Tests;

/// <summary>
/// ICollectionEventDispatcher が通知を遅延させる場合、購読者から見えるリストの内容も
/// 通知の発火に合わせて遅延しなければならない (issue #115)。
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
    /// フィルターの判定が追加時と削除時で食い違うと、ビューに入っていない要素の削除通知が流れてくる。
    /// ビューの内容が変わっていないのだから、購読者に通知してはならないことを確認する。
    /// インデックスが不明な (-1 の) 通知を積むと、発火時に適用できず内容の整合が永久に崩れる。
    /// </summary>
    [Fact]
    public void RemoveOfItemMissingFromViewIsNotNotified()
    {
        var dispatcher = new QueuedCollectionEventDispatcher();

        var item = new Flagged { Visible = false };

        var set = new ObservableHashSet<Flagged>();
        set.Add(item);

        using var view = set.CreateView(x => x);
        view.AttachFilter(x => x.Visible); // item は除外されるのでビューに入らない

        using var notify = view.ToNotifyCollectionChanged(dispatcher);

        var tracker = new NotifyCollectionChangedContractTracker<Flagged>(notify);

        item.Visible = true; // ビューは再評価しないので内容は空のまま

        set.Remove(item); // 削除時の判定は true なので Remove の通知が流れる

        dispatcher.Pump();

        tracker.Actions.Should().BeEmpty();
        tracker.Violations.Should().BeEmpty();
        notify.Should().BeEmpty();
    }

    /// <summary>
    /// フィルタを持たないビュー (NonFilteredSynchronizedViewList) でも同じ保証が必要。
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
    /// 購読者がいなければ整合させるべき通知が無いので、その場で反映してよい。
    /// キューに何も積まないので、後から購読しても過去の通知は流れてこない。
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

        // 購読を始めた後の変更は遅延する。
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
    /// 購読を解除しても、キューに残っている通知が先に発火されるまでは遅延を続ける。
    /// 途中で同期適用に切り替えると、適用の順序が入れ替わって内容が壊れる。
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

        // 購読者はいないが、先の通知が未処理なので順序を保つために積む。
        dispatcher.PendingCount.Should().Be(2);
        notify.Should().Equal(new[] { "$1" });

        dispatcher.Pump();

        notify.Should().Equal(new[] { "$3", "$2", "$1" });
    }

    /// <summary>
    /// 遅延中の位置指定書き込みは、見えているインデックスを未処理の変更で読み替えてからソースへ渡す。
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

        // 見えているのは ["$1", "$2", "$3", "$4"] なので、[1] は "$2"。
        // ソースでは先頭に 0 が入っているのでインデックス 2 にある。
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

        // 見えている ["$1", "$2", "$3"] の [1] の位置、つまり "$2" の直前へ。
        notify.Insert(1, "$9");

        list.Should().Equal(new[] { 0, 1, 9, 2, 3 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$9", "$2", "$3" });
    }

    /// <summary>
    /// 遅延中に位置指定書き込みをしても、購読者から見える内容が通知から再構築した内容と
    /// 一致し続けること、および通知が余分に増えないことを確認する。
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

        // 見えている ["$1", "$2", "$3"] の [1]、つまり "$2" を差し替える。
        notify[1] = "$9";

        list.Should().Equal(new[] { 0, 1, 9, 3 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$9", "$3" });

        // ソース由来の Replace と二重に通知しない。
        tracker.Actions.Should().Equal(new[] { NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Replace });
    }

    /// <summary>
    /// converter がソースへの書き込みを拒否してソース由来の Replace 通知が出ない場合も、
    /// 購読者から見える内容が通知から再構築した内容と一致し続けることを確認する。
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
    /// フィルタ付きのビューでも、逆引き (View インデックス → ソース インデックス) と組み合わせて機能する。
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

        Task.Run(() => list.Insert(0, 6)).Wait(); // フィルタを通るので view の先頭に入る

        dispatcher.PendingCount.Should().Be(1);

        // 見えているのは ["$2", "$4"] なので、[1] は "$4"。
        notify.RemoveAt(1);

        list.Should().Equal(new[] { 6, 1, 2, 3 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$6", "$2" });
    }

    /// <summary>
    /// 対象の要素自体が未処理の削除で消えている場合は、読み替えようがないので失敗させる。
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

        Task.Run(() => list.RemoveAt(1)).Wait(); // 2 が消える

        // 見えている ["$1", "$2", "$3"] の [1] は既に存在しない。
        notify.Invoking(x => x.RemoveAt(1))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("The element at index 1 has been removed*");

        list.Should().Equal(new[] { 1, 3 });
    }

    /// <summary>
    /// 未処理の Reset があるとどのインデックスも読み替えられない。
    /// 特定の要素が消えた場合と混同させず、インデックスを変えれば通ると誤解させないことを確認する。
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

        // 挿入位置であっても Reset は読み替えられない。
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
    /// 見えているリストの末尾への挿入は、未処理の変更があっても末尾追加として扱われる。
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

        // 見えているのは ["$1", "$2"] なので、[2] は末尾。
        notify.Insert(2, "$9");

        list.Should().Equal(new[] { 0, 1, 2, 9 });

        dispatcher.Pump();

        tracker.Violations.Should().BeEmpty();
        notify.Should().Equal(new[] { "$0", "$1", "$2", "$9" });
    }

    /// <summary>
    /// 範囲外のインデックスの扱いは、ディスパッチャーの有無で変わってはならない。
    /// 呼び出し元が渡した -1 を、追跡不能を表す内部の -1 と混同すると
    /// ArgumentOutOfRangeException ではなく InvalidOperationException になってしまう。
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
    /// 同じディスパッチャーが非同期発火と同期発火を混在させる場合。
    /// UI スレッド上の変更は同期発火するが、その前に積まれている通知を追い越してはならない。
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
            Task.Run(() => list.Insert(0, 2)).Wait(); // 別スレッドなので遅延

            ui.PendingCount.Should().Be(1);

            ui.Invoke(() => list.Insert(0, 3)); // UI スレッドなので同期発火

            // 遅延していた分も同時に処理されるので、二つとも発火し終わっている。
            tracker.Actions.Should().Equal(new[]
            {
                NotifyCollectionChangedAction.Add,
                NotifyCollectionChangedAction.Add,
            });

            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$3", "$2", "$1" });

            ui.Pump(); // 積まれたままの通知は無い

            tracker.Actions.Should().HaveCount(2);
        }
        finally
        {
            notify.Dispose();
        }
    }

    /// <summary>
    /// 購読者が例外を投げても、まだ発火していない通知を捨ててはならない。
    /// 別スレッドの変更が積まれている状態で UI スレッドから変更すると、古い通知も同じ呼び出しの中で
    /// 発火されるため、そこで例外が出ると後続の通知が発火されないまま取り残される。
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

        // Reset で NewItems が null になることを考えていないハンドラー相当。
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

            // 全通知を記録したいので tracker を先に購読する。
            tracker = new NotifyCollectionChangedContractTracker<string>(notify);
            notify.CollectionChanged += Thrower;
        });

        try
        {
            Task.Run(() => list.Clear()).Wait(); // 別スレッドなので Reset が積まれる

            ui.PendingCount.Should().Be(1);

            Exception error = null;

            ui.Invoke(() =>
            {
                try
                {
                    list.Add(2); // UI スレッドなので同期発火。積まれていた Reset もここで発火される
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });

            // Reset のハンドラーが投げた例外は、無関係な Add の呼び出し元に出てくる。
            thrownCount.Should().Be(1);
            error.Should().BeOfType<InvalidOperationException>();

            // Add の通知と適用が失われてはならない。
            tracker.Actions.Should().Equal(new[]
            {
                NotifyCollectionChangedAction.Reset,
                NotifyCollectionChangedAction.Add,
            });

            tracker.Violations.Should().BeEmpty();
            notify.Should().Equal(new[] { "$2" });

            ui.Pump(); // 取り残された通知は無い

            tracker.Actions.Should().HaveCount(2);
        }
        finally
        {
            notify.Dispose();
        }
    }
}
