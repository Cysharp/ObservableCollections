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
    // long enough for any test, short enough to fail instead of hanging the run when the thread deadlocks
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    readonly QueuedSynchronizationContext context;
    readonly BlockingCollection<Action> work = new();
    readonly Thread thread;

    /// <param name="name">The name of the thread.</param>
    /// <param name="renewContextPerOperation">
    /// Mimics WPF: every Invoke and every pumped callback runs under a new SynchronizationContext instance.
    /// </param>
    public TestUiThread(string name, bool renewContextPerOperation = false)
    {
        context = new QueuedSynchronizationContext(renewContextPerOperation);

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
        foreach (var action in work.GetConsumingEnumerable())
        {
            SynchronizationContext.SetSynchronizationContext(context.CreateOperationContext());
            action();
        }
    }

    /// <summary>
    /// Executes the action on this thread and waits for it to complete.
    /// Throws TimeoutException when it does not complete in time, so that a deadlock fails the test instead of
    /// hanging the whole run.
    /// </summary>
    public void Invoke(Action action)
    {
        if (Thread.CurrentThread == thread)
        {
            throw new InvalidOperationException($"Invoke is called on {thread.Name} itself, which would wait for itself forever.");
        }

        // not disposed when it times out, the stuck action may still set it later
        var done = new ManualResetEventSlim();
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

        if (!done.Wait(Timeout))
        {
            throw new TimeoutException($"The action on {thread.Name} did not complete within {Timeout}.");
        }

        done.Dispose();

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

        // a deadlocked thread is a background thread, so leave it behind instead of hanging the run
        if (thread.Join(Timeout))
        {
            work.Dispose();
        }
    }
}
