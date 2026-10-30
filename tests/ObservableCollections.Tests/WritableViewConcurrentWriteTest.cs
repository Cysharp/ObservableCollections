using System;
using System.Collections.Specialized;
using System.Threading;

namespace ObservableCollections.Tests;

/// <summary>
/// A position-based write through a writable view translates the index the caller sees into a source index, and
/// then writes the source. Another thread that writes the source must not be able to move the elements in between,
/// and the two writers must not deadlock.
///
/// The converter runs between the translation and the source write, so the tests write the source from another
/// thread inside the converter.
/// </summary>
public class WritableViewConcurrentWriteTest
{
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    static int ToOriginal(string newView, int original, ref bool setValue)
    {
        return int.Parse(newView.Substring(1));
    }

    /// <summary>
    /// Starts a thread that runs the action and records its exception, so that an exception does not tear down the
    /// test process. Not a task, because waiting for a task may run it inline on the waiting thread.
    /// </summary>
    sealed class Worker
    {
        readonly Thread thread;
        Exception error;

        public Worker(Action action)
        {
            thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            })
            {
                IsBackground = true,
            };

            thread.Start();
        }

        /// <summary>
        /// Waits until the thread has either completed or is blocked, for example on a lock that the caller holds,
        /// and returns true in the latter case. Unlike waiting for a fixed time, this tells the two apart however
        /// slow the machine is.
        /// </summary>
        public bool WaitUntilBlocked()
        {
            SpinWait.SpinUntil(() => (thread.ThreadState & (ThreadState.WaitSleepJoin | ThreadState.Stopped)) != 0, Timeout)
                .Should().BeTrue("the worker must either complete or be blocked");

            return (thread.ThreadState & ThreadState.Stopped) == 0;
        }

        public void Complete()
        {
            thread.Join(Timeout).Should().BeTrue("the worker must not be blocked forever");
            error.Should().BeNull();
        }
    }

    /// <summary>
    /// Creates a writable view over [1, 2, 3, 4], with a filter that keeps the even elements or without a filter.
    /// In both cases the last element of the view is 4.
    /// </summary>
    static (NotifyCollectionChangedSynchronizedViewList<string> Notify, IDisposable Owner) CreateView(
        ObservableList<int> list, bool filtered, WritableViewChangedEventHandler<int, string> converter)
    {
        if (filtered)
        {
            var view = list.CreateWritableView(x => $"${x}");
            view.AttachFilter(x => x % 2 == 0);
            return (view.ToWritableNotifyCollectionChanged(converter), view);
        }

        var notify = list.ToWritableNotifyCollectionChanged(x => $"${x}", converter);
        return (notify, notify);
    }

    /// <summary>
    /// Verifies that the setter replaces the element the caller saw at the index, even when another thread inserts
    /// into the source between the index translation and the source write.
    /// Without holding the source lock in between, the stale source index would replace the neighbor instead.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetterReplacesElementCallerSaw(bool filtered)
    {
        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        Worker worker = null;
        var blocked = false;

        int Converter(string newView, int original, ref bool setValue)
        {
            worker = new Worker(() => list.Insert(0, 0));
            blocked = worker.WaitUntilBlocked();
            return ToOriginal(newView, original, ref setValue);
        }

        var (notify, owner) = CreateView(list, filtered, Converter);
        using var _ = owner;

        notify[notify.Count - 1] = "$40"; // 4, the last element

        worker.Complete();

        blocked.Should().BeTrue("the other writer must wait until the write completes");
        list.Should().Equal(new[] { 0, 1, 2, 3, 40 });
        notify.Should().Equal(filtered
            ? new[] { "$0", "$2", "$40" }
            : new[] { "$0", "$1", "$2", "$3", "$40" });
    }

    /// <summary>
    /// Verifies that, when the converter rejects the source write, the setter changes the view of the element the
    /// caller saw at the index and nothing else, even when another thread inserts into the source in between.
    /// The view of the element is changed in the parent and in the view list using the indexes translated before,
    /// so a stale index would change the view of the neighbor and notify a Replace of the wrong element.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectedSetterChangesViewOfElementCallerSaw(bool filtered)
    {
        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        Worker worker = null;
        var blocked = false;

        int Converter(string newView, int original, ref bool setValue)
        {
            worker = new Worker(() => list.Insert(0, 0));
            blocked = worker.WaitUntilBlocked();
            setValue = false;
            return original;
        }

        var (notify, owner) = CreateView(list, filtered, Converter);
        using var _ = owner;

        notify[notify.Count - 1] = "$40"; // 4, the last element

        worker.Complete();

        blocked.Should().BeTrue("the other writer must wait until the write completes");
        list.Should().Equal(new[] { 0, 1, 2, 3, 4 });
        notify.Should().Equal(filtered
            ? new[] { "$0", "$2", "$40" }
            : new[] { "$0", "$1", "$2", "$3", "$40" });
    }

    /// <summary>
    /// Verifies that Insert inserts right before the element the caller saw at the index, even when another thread
    /// inserts into the source between the index translation and the source write.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InsertGoesBeforeElementCallerSaw(bool filtered)
    {
        var list = new ObservableList<int>(new[] { 1, 2, 3, 4 });

        Worker worker = null;
        var blocked = false;

        int Converter(string newView, int original, ref bool setValue)
        {
            worker = new Worker(() => list.Insert(0, 0));
            blocked = worker.WaitUntilBlocked();
            return ToOriginal(newView, original, ref setValue);
        }

        var (notify, owner) = CreateView(list, filtered, Converter);
        using var _ = owner;

        notify.Insert(notify.Count - 1, "$10"); // right before 4, the last element

        worker.Complete();

        blocked.Should().BeTrue("the other writer must wait until the write completes");
        list.Should().Equal(new[] { 0, 1, 2, 3, 10, 4 });
        notify.Should().Equal(filtered
            ? new[] { "$0", "$2", "$10", "$4" }
            : new[] { "$0", "$1", "$2", "$3", "$10", "$4" });
    }

    /// <summary>
    /// Verifies that a write through a writable view does not deadlock with a write of the source on another
    /// thread. The source raises its change inside its own lock and the view takes its own lock inside it, so a
    /// write through the view must not take the lock of the view before the lock of the source.
    /// Covers the view list setter and a direct call of IWritableSynchronizedView.SetToSourceCollection.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WriteThroughViewDoesNotDeadlockWithSourceWriter(bool viaViewList)
    {
        var list = new ObservableList<int>(new[] { 1, 2, 3 });

        using var entered = new ManualResetEventSlim();
        using var viewWriterStarted = new ManualResetEventSlim();

        Worker viewWriter = null;
        var blocked = false;

        // Subscribes before the view, so that it runs inside the lock of the source before the view takes its own.
        list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                entered.Set();

                // let the other writer go as far as it can, and take the locks it takes before the lock of the source
                viewWriterStarted.Wait(Timeout).Should().BeTrue();
                blocked = viewWriter.WaitUntilBlocked();
            }
        };

        using var view = list.CreateWritableView(x => $"${x}");
        using var notify = view.ToWritableNotifyCollectionChanged(ToOriginal);

        var sourceWriter = new Worker(() => list.Add(4));
        entered.Wait(Timeout).Should().BeTrue();

        viewWriter = new Worker(() =>
        {
            if (viaViewList)
            {
                notify[0] = "$10";
            }
            else
            {
                view.SetToSourceCollection(0, 10);
            }
        });

        viewWriterStarted.Set();

        sourceWriter.Complete();
        viewWriter.Complete();

        blocked.Should().BeTrue("the write through the view must wait for the lock of the source");
        list.Should().Equal(new[] { 10, 2, 3, 4 });
        notify.Should().Equal(new[] { "$10", "$2", "$3", "$4" });
    }
}
