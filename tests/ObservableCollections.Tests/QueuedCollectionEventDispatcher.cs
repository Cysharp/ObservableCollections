using System.Collections.Generic;

namespace ObservableCollections.Tests;

/// <summary>
/// A stand-in for a UI thread dispatcher. Posted notifications only pile up in the queue and are not
/// raised until Pump is called.
/// It does not depend on SynchronizationContext, so it is not affected by the test execution order or
/// the state of the thread.
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
    /// The equivalent of a message loop. Raises the queued notifications in order.
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
