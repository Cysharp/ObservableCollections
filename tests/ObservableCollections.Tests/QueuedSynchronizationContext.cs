using System.Collections.Concurrent;
using System.Threading;

namespace ObservableCollections.Tests;

/// <summary>
/// A stand-in for a UI thread (the WinUI 3 DispatcherQueue).
/// Posted callbacks only pile up in the queue and are not executed until Pump is called.
/// </summary>
internal sealed class QueuedSynchronizationContext : SynchronizationContext
{
    readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> queue;
    readonly bool renewPerOperation;

    /// <param name="renewPerOperation">
    /// Mimics WPF, which installs a new DispatcherSynchronizationContext for every dispatcher operation.
    /// When true, <see cref="CreateOperationContext"/> returns a new instance that shares the queue.
    /// </param>
    public QueuedSynchronizationContext(bool renewPerOperation = false)
        : this(new ConcurrentQueue<(SendOrPostCallback, object)>(), renewPerOperation)
    {
    }

    QueuedSynchronizationContext(ConcurrentQueue<(SendOrPostCallback, object)> queue, bool renewPerOperation)
    {
        this.queue = queue;
        this.renewPerOperation = renewPerOperation;
    }

    public override void Post(SendOrPostCallback d, object state)
    {
        queue.Enqueue((d, state));
    }

    public override void Send(SendOrPostCallback d, object state)
    {
        d(state);
    }

    // the base implementation returns a plain SynchronizationContext, which would post to the thread pool
    public override SynchronizationContext CreateCopy()
    {
        return new QueuedSynchronizationContext(queue, renewPerOperation);
    }

    public int PendingCount => queue.Count;

    /// <summary>
    /// The context to install as SynchronizationContext.Current while an operation runs on the thread.
    /// </summary>
    public QueuedSynchronizationContext CreateOperationContext()
    {
        return renewPerOperation ? new QueuedSynchronizationContext(queue, renewPerOperation) : this;
    }

    /// <summary>
    /// The equivalent of a message loop. Executes the queued callbacks in order.
    /// </summary>
    public void Pump()
    {
        var previous = Current;
        try
        {
            while (queue.TryDequeue(out var item))
            {
                SetSynchronizationContext(CreateOperationContext());
                item.Callback(item.State);
            }
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }
}
