using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ObservableCollections.Tests;

/// <summary>
/// 専用の SynchronizationContext を持つスレッド。UI スレッドの代替。
/// Post された通知はそのスレッドのキューに溜まるだけで、Pump を呼ぶまで発火しない。
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
    /// このスレッド上で action を実行し、完了を待つ。
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
            throw new InvalidOperationException($"{thread.Name} で例外が発生した。", error);
        }
    }

    /// <summary>
    /// メッセージ ループ相当。このスレッド上で溜まっている通知を順に発火する。
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
