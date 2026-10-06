using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ObservableCollections.Tests;

/// <summary>
/// A thread with its own SynchronizationContext. A stand-in for a UI thread.
/// Posted notifications only pile up in that thread's queue and are not raised until Pump is called.
/// </summary>
internal sealed class TestUiThread : IDisposable
{
    readonly QueuedSynchronizationContext context = new();
    readonly BlockingCollection<Action> work = new();
    readonly Thread thread;

    public TestUiThread(string name)
    {
        thread = new Thread(Run)
        {
            IsBackground = true,
            Name = name,
        };

        thread.Start();
    }

    public SynchronizationContext Context => context;

    public int PendingCount => context.PendingCount;

    public int ManagedThreadId => thread.ManagedThreadId;

    void Run()
    {
        SynchronizationContext.SetSynchronizationContext(context);

        foreach (var action in work.GetConsumingEnumerable())
        {
            action();
        }
    }

    /// <summary>
    /// Executes the action on this thread and waits for it to complete.
    /// </summary>
    public void Invoke(Action action)
    {
        using var done = new ManualResetEventSlim();
        Exception error = null;

        work.Add(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                done.Set();
            }
        });

        done.Wait();

        if (error != null)
        {
            throw new InvalidOperationException($"An exception occurred on {thread.Name}.", error);
        }
    }

    /// <summary>
    /// The equivalent of a message loop. Raises the notifications queued on this thread in order.
    /// </summary>
    public void Pump()
    {
        Invoke(context.Pump);
    }

    public void Dispose()
    {
        work.CompleteAdding();
        thread.Join();
        work.Dispose();
    }
}
