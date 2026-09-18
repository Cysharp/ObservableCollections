using System.Collections.Generic;

namespace ObservableCollections.Tests;

/// <summary>
/// UI スレッドのディスパッチャーの代替。Post された通知はキューに溜まるだけで、Pump を呼ぶまで発火しない。
/// SynchronizationContext に依存しないので、テストの実行順序やスレッドの状態に左右されない。
/// </summary>
internal sealed class QueuedCollectionEventDispatcher : ICollectionEventDispatcher
{
    readonly Queue<CollectionEventDispatcherEventArgs> queue = new();

    public int PendingCount
    {
        get
        {
            lock (queue)
            {
                return queue.Count;
            }
        }
    }

    public void Post(CollectionEventDispatcherEventArgs ev)
    {
        lock (queue)
        {
            queue.Enqueue(ev);
        }
    }

    /// <summary>
    /// メッセージ ループ相当。溜まっている通知を順に発火する。
    /// </summary>
    public void Pump()
    {
        while (true)
        {
            CollectionEventDispatcherEventArgs ev;
            lock (queue)
            {
                if (queue.Count == 0)
                {
                    return;
                }

                ev = queue.Dequeue();
            }

            ev.Invoke();
        }
    }
}
