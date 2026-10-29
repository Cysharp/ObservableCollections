using System;
using System.Collections;
using System.Collections.Specialized;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ObservableCollections
{
    /// <summary>
    /// Decides where and when the change notifications of a view are raised.
    /// </summary>
    /// <remarks>
    /// A view that is given a dispatcher defers its visible content together with the notifications: a change becomes
    /// visible when its event is invoked, so that a subscriber always sees the content that matches the notification.
    /// </remarks>
    public interface ICollectionEventDispatcher
    {
        /// <summary>
        /// Invokes <paramref name="ev"/>, either synchronously or later on another thread.
        /// </summary>
        /// <remarks>
        /// Every posted event must eventually be invoked exactly once. Until then, the view keeps the change and its
        /// visible content lags behind the source. An event that is never invoked, for example because the dispatcher
        /// has been shut down, is kept for as long as the view is alive, and so are all the changes after it. Dispose
        /// the view before its dispatcher stops.
        /// </remarks>
        void Post(CollectionEventDispatcherEventArgs ev);
    }

    /// <summary>
    /// Raises the events through a <see cref="SynchronizationContext"/>.
    /// An event is raised synchronously when it occurs within the context, and is posted to the context otherwise.
    /// </summary>
    /// <remarks>
    /// The context must run every posted callback on one dedicated thread, such as a UI thread, and only that thread
    /// may have it as <see cref="SynchronizationContext.Current"/>. Being on that thread is taken as being within the
    /// context, as the UI frameworks themselves tell thread access. A context that runs callbacks on pool threads,
    /// such as the one of Blazor Server, does not satisfy this; an event may then be raised synchronously outside the
    /// context. For such a context, implement <see cref="ICollectionEventDispatcher"/> with the dispatcher of the
    /// framework.
    /// <para>
    /// The thread is known only when the dispatcher is created within the context. When it is created on another
    /// thread, only an event that occurs while the very same context instance is current is raised synchronously.
    /// Some frameworks, WPF for example, install a new context instance for every operation, so create the dispatcher
    /// on the UI thread to raise the changes made on the UI thread synchronously.
    /// </para>
    /// </remarks>
    public class SynchronizationContextCollectionEventDispatcher : ICollectionEventDispatcher
    {
        static readonly Lazy<ICollectionEventDispatcher> current = new(() =>
        {
            var current = SynchronizationContext.Current;
            if (current == null)
            {
                throw new InvalidOperationException("SynchronizationContext.Current is null");
            }

            return new SynchronizationContextCollectionEventDispatcher(current);
        });

        public static ICollectionEventDispatcher Current => current.Value;

        readonly SynchronizationContext synchronizationContext;
        static readonly SendOrPostCallback invokeCallback = InvokeEvent;

        // The thread that synchronizationContext dispatches to, or 0 when the dispatcher was created on another thread.
        // Managed thread IDs are always positive. It relies on the context running on one dedicated thread, see remarks.
        readonly int boundThreadId;

        public SynchronizationContextCollectionEventDispatcher(SynchronizationContext synchronizationContext)
        {
            this.synchronizationContext = synchronizationContext;

            if (SynchronizationContext.Current == synchronizationContext)
            {
                boundThreadId = Environment.CurrentManagedThreadId;
            }
        }

        public void Post(CollectionEventDispatcherEventArgs ev)
        {
            // compare threads, not only instances. WPF installs a new DispatcherSynchronizationContext for every
            // dispatcher operation, so SynchronizationContext.Current on the bound thread is usually another instance.
            if (Environment.CurrentManagedThreadId == boundThreadId
                || SynchronizationContext.Current == synchronizationContext)
            {
                // on the bound thread, send the event synchronously
                invokeCallback(ev);
            }
            else
            {
                // another thread(it may have its own SynchronizationContext), post the event asynchronously
                synchronizationContext.Post(invokeCallback, ev);
            }
        }

        static void InvokeEvent(object? state)
        {
            var ev = (CollectionEventDispatcherEventArgs)state!;
            ev.Invoke();
        }
    }

    internal class InlineCollectionEventDispatcher : ICollectionEventDispatcher
    {
        public static readonly ICollectionEventDispatcher Instance = new InlineCollectionEventDispatcher();

        InlineCollectionEventDispatcher()
        {
        }

        public void Post(CollectionEventDispatcherEventArgs ev)
        {
            ev.Invoke();
        }
    }

    public class CollectionEventDispatcherEventArgs : NotifyCollectionChangedEventArgs
    {
        // +state, init;
        public object Collection { get; set; } = default!;
        public bool IsInvokeCollectionChanged { get; set; }
        public bool IsInvokePropertyChanged { get; set; }
        internal Action<CollectionEventDispatcherEventArgs> Invoker { get; set; } = default!;

        /// <summary>Set when the event is enqueued into DeferredViewList, to tell whether it is still pending.</summary>
        internal long DeferredSequence { get; set; }

        // an exception thrown while a newer event raised this one ahead of its own callback.
        // both are accessed inside the gate of the collection.
        ExceptionDispatchInfo? catchUpException;
        bool isOwnCallbackCompleted;

        /// <summary>
        /// Keeps the exception that a subscriber threw while a newer event raised this one, so that the own
        /// callback of this event rethrows it, as it would have thrown without being raised ahead.
        /// Returns false when the own callback has completed already, then the caller must throw it.
        /// </summary>
        internal bool TryDeferCatchUpException(Exception exception)
        {
            if (isOwnCallbackCompleted) return false;

            catchUpException = ExceptionDispatchInfo.Capture(exception);
            return true;
        }

        /// <summary>
        /// Called by the own callback of this event when a newer event has raised it already.
        /// Returns the exception to rethrow, if any.
        /// </summary>
        internal ExceptionDispatchInfo? CompleteOwnCallback()
        {
            isOwnCallbackCompleted = true;

            var exception = catchUpException;
            catchUpException = null;
            return exception;
        }

        public void Invoke()
        {
            Invoker.Invoke(this);
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action) : base(action)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList? changedItems) : base(action, changedItems)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? changedItem) : base(action, changedItem)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList newItems, IList oldItems) : base(action, newItems, oldItems)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList? changedItems, int startingIndex) : base(action, changedItems, startingIndex)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? changedItem, int index) : base(action, changedItem, index)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? newItem, object? oldItem) : base(action, newItem, oldItem)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList newItems, IList oldItems, int startingIndex) : base(action, newItems, oldItems, startingIndex)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, IList? changedItems, int index, int oldIndex) : base(action, changedItems, index, oldIndex)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? changedItem, int index, int oldIndex) : base(action, changedItem, index, oldIndex)
        {
        }

        public CollectionEventDispatcherEventArgs(NotifyCollectionChangedAction action, object? newItem, object? oldItem, int index) : base(action, newItem, oldItem, index)
        {
        }
    }
}
