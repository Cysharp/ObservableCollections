using System.Collections.Concurrent;
using System.Threading;

namespace ObservableCollections.Tests;

/// <summary>
/// UI スレッド (WinUI 3 の DispatcherQueue) の代替。
/// Post されたコールバックはキューに溜まるだけで、Pump を呼ぶまで実行されない。
/// </summary>
internal sealed class QueuedSynchronizationContext : SynchronizationContext
{
    readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> queue = new();

    public override void Post(SendOrPostCallback d, object state)
    {
        queue.Enqueue((d, state));
    }

    public override void Send(SendOrPostCallback d, object state)
    {
        d(state);
    }

    public int PendingCount => queue.Count;

    /// <summary>
    /// メッセージ ループ相当。溜まっているコールバックを順に実行する。
    /// </summary>
    public void Pump()
    {
        var previous = Current;
        SetSynchronizationContext(this);
        try
        {
            while (queue.TryDequeue(out var item))
            {
                item.Callback(item.State);
            }
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
