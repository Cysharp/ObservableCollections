using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Tests;

/// <summary>
/// 書き込み可能ビューのセッターは、可視内容を変えたなら必ず通知を伴わなければならない。
/// ソースへ書き込む場合はソース由来の Replace がその通知になり、書き込まない場合はセッター自身が
/// 通知を出す必要がある。
///
/// T と TView を別の型にして、converter をスキップするショートカット
/// (typeof(T) == typeof(TView)) と絡まないようにしている。
/// </summary>
public class WritableViewSetterNotificationTest
{
    static int ToOriginal(string newView, int original, ref bool setValue)
    {
        return int.Parse(newView.Substring(1));
    }

    static int RejectSourceWrite(string newView, int original, ref bool setValue)
    {
        setValue = false;
        return original;
    }

    static ObservableList<int> CreateSource()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        return list;
    }

    /// <summary>
    /// converter がソースへの書き込みを拒否した (setValue == false) とき、ソース由来の Replace は来ない。
    /// セッターが可視内容だけを書き換えて無通知で済ませてしまうと、購読者は変更を知る手段がない。
    /// ディスパッチャー未指定の場合も通知が出ることを確認する。
    /// </summary>
    [Fact]
    public void ConverterRejectedWriteRaisesReplaceWithoutDispatcher()
    {
        var list = CreateSource();

        using var view = list.CreateWritableView(x => $"${x}");
        using var bindable = view.ToWritableNotifyCollectionChanged(RejectSourceWrite);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable[1] = "$99";

        list.Should().Equal(new[] { 1, 2, 3 }); // 拒否されたのでソースは変わらない
        bindable.Should().Equal(new[] { "$1", "$99", "$3" });

        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        events[0].NewStartingIndex.Should().Be(1);
        events[0].NewItems![0].Should().Be("$99");
        events[0].OldItems![0].Should().Be("$2");
    }

    /// <summary>
    /// フィルターなしのビュー (NonFilteredSynchronizedViewList) でも同じ保証が必要。
    /// </summary>
    [Fact]
    public void ConverterRejectedWriteRaisesReplaceOnNonFilteredView()
    {
        var list = CreateSource();

        using var bindable = list.ToWritableNotifyCollectionChanged(x => $"${x}", RejectSourceWrite);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable[1] = "$99";

        list.Should().Equal(new[] { 1, 2, 3 });
        bindable.Should().Equal(new[] { "$1", "$99", "$3" });

        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        events[0].NewStartingIndex.Should().Be(1);
    }

    /// <summary>
    /// ディスパッチャーがある場合、拒否された書き込みの通知も他の通知と同様に遅延され、
    /// 発火するまで可視内容は変わらない。
    /// </summary>
    [Fact]
    public void ConverterRejectedWriteIsDeferredWithDispatcher()
    {
        var list = CreateSource();

        var dispatcher = new QueuedCollectionEventDispatcher();
        using var bindable = list.ToWritableNotifyCollectionChanged(x => $"${x}", RejectSourceWrite, dispatcher);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable[1] = "$99";

        bindable.Should().Equal(new[] { "$1", "$2", "$3" }); // 通知前なので変わっていない
        events.Should().BeEmpty();

        dispatcher.Pump();

        bindable.Should().Equal(new[] { "$1", "$99", "$3" });
        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        events[0].NewStartingIndex.Should().Be(1);
    }

    /// <summary>
    /// ソースへの書き込みが失敗した場合、その Replace 通知は届かない。
    /// セッターが先に可視内容を書き換えていると、通知のない変更が残ってしまう。
    /// ここではビューより先に登録された購読者を throw させて、ソースの通知がビューに届かない状況を作る。
    /// (ソース自体はこの時点で既に書き換わっており、ビューとの乖離は別の既存の問題。)
    /// </summary>
    [Fact]
    public void FailedSourceWriteDoesNotChangeVisibleContentSilently()
    {
        var list = CreateSource();
        list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> _) => throw new InvalidOperationException("boom");

        var dispatcher = new QueuedCollectionEventDispatcher();
        using var bindable = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable.Invoking(x => x[1] = "$99").Should().Throw<InvalidOperationException>();

        dispatcher.Pump();

        events.Should().BeEmpty();
        bindable.Should().Equal(new[] { "$1", "$2", "$3" });
    }

    /// <summary>
    /// フィルター付きのビューでも同様。ここでは View インデックスとソース インデックスがずれる。
    /// </summary>
    [Fact]
    public void FailedSourceWriteDoesNotChangeVisibleContentSilentlyOnFilteredView()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        // ビューより先に登録する必要がある。後だとビューのハンドラーが先に走ってしまう。
        list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> _) => throw new InvalidOperationException("boom");

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        var dispatcher = new QueuedCollectionEventDispatcher();
        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal, dispatcher);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        // View は ["$2", "$4"]。その [1] はソース インデックス 3。
        bindable.Invoking(x => x[1] = "$99").Should().Throw<InvalidOperationException>();

        dispatcher.Pump();

        events.Should().BeEmpty();
        bindable.Should().Equal(new[] { "$2", "$4" });
    }
}
