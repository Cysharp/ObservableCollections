using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Tests;

/// <summary>
/// 「CollectionChanged の通知だけを見ている観測者は、コレクションの中身を完全に再構築できる」
/// という契約を検証する。
/// 受け取った通知を影武者リストに適用し、その結果が実際のコレクションと一致するかを毎回確認する。
/// 個々のインデックスを目で追う必要がなく、インデックスのズレも Count のズレもこれ一つで捕まる。
/// </summary>
internal sealed class NotifyCollectionChangedContractTracker<T>
{
    readonly IReadOnlyList<T> target;
    readonly List<T> shadow;

    public NotifyCollectionChangedContractTracker(NotifyCollectionChangedSynchronizedViewList<T> target)
    {
        this.target = target;

        // 購読開始時点のスナップショット。
        // ToList / AddRange は ICollection<T>.CopyTo を使うが、これは NotSupportedException を投げるので使えない。
        this.shadow = new List<T>();
        Resync();

        target.CollectionChanged += OnCollectionChanged;
    }

    public List<NotifyCollectionChangedAction> Actions { get; } = new();

    public List<string> Violations { get; } = new();

    void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        Actions.Add(e.Action);

        try
        {
            Apply(e);
        }
        catch (Exception ex)
        {
            Violations.Add($"{e.Action}: 通知の適用に失敗した: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        if (!shadow.SequenceEqual(target))
        {
            Violations.Add($"{e.Action}: 通知から再構築した内容 [{Join(shadow)}] が実際の内容 [{Join(target)}] と一致しない");
        }
    }

    void Apply(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertRange(e.NewStartingIndex, e.NewItems);

                // WinUI 3 の ListView が IBindableVector.GetAt(index) で読むのと同じアクセス。
                // 契約が守られていなければここで例外になる (issue #115 のクラッシュ地点)。
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    _ = target[e.NewStartingIndex + i];
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                shadow.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                break;

            case NotifyCollectionChangedAction.Replace:
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    shadow[e.NewStartingIndex + i] = (T)e.NewItems[i];
                }
                break;

            case NotifyCollectionChangedAction.Move:
                var moved = shadow.GetRange(e.OldStartingIndex, e.OldItems.Count);
                shadow.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                shadow.InsertRange(e.NewStartingIndex, moved);
                break;

            case NotifyCollectionChangedAction.Reset:
                // Reset は「全部読み直せ」なので、実際のコレクションから再同期する。
                Resync();
                break;
        }
    }

    void Resync()
    {
        shadow.Clear();
        foreach (var item in target)
        {
            shadow.Add(item);
        }
    }

    void InsertRange(int index, IList items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            shadow.Insert(index + i, (T)items[i]);
        }
    }

    static string Join(IEnumerable<T> source)
    {
        return string.Join(", ", source);
    }
}
