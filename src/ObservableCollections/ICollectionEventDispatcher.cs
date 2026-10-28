using System;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;

namespace ObservableCollections
{
    public interface ICollectionEventDispatcher
    {
        void Post(CollectionEventDispatcherEventArgs ev);
    }

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
        static readonly SendOrPostCallback probeBoundThreadCallback = ProbeBoundThread;

        // The thread that synchronizationContext dispatches to, or 0 while it is not known yet.
        // Managed thread IDs are always positive.
        int boundThreadId;

        public SynchronizationContextCollectionEventDispatcher(SynchronizationContext synchronizationContext)
        {
            this.synchronizationContext = synchronizationContext;

            if (SynchronizationContext.Current == synchronizationContext)
            {
                boundThreadId = Environment.CurrentManagedThreadId;
            }
            else
            {
                // created off the bound thread, so ask the context which thread it dispatches to.
                // until the answer arrives, every event is posted, which only delays it.
                synchronizationContext.Post(probeBoundThreadCallback, this);
            }
        }

        public void Post(CollectionEventDispatcherEventArgs ev)
        {
            // compare threads, not instances. WPF installs a new DispatcherSynchronizationContext for every dispatcher
            // operation, so SynchronizationContext.Current on the bound thread is usually another instance.
            if (Environment.CurrentManagedThreadId == Volatile.Read(ref boundThreadId)
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

        static void ProbeBoundThread(object? state)
        {
            var self = (SynchronizationContextCollectionEventDispatcher)state!;
            Volatile.Write(ref self.boundThreadId, Environment.CurrentManagedThreadId);
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
