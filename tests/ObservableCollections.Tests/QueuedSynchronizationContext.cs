using System.Collections.Concurrent;
using System.Threading;

namespace ObservableCollections.Tests;

/// <summary>
/// A stand-in for a UI thread (the WinUI 3 DispatcherQueue).
/// Posted callbacks only pile up in the queue and are not executed until Pump is called.
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
    /// The equivalent of a message loop. Executes the queued callbacks in order.
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
