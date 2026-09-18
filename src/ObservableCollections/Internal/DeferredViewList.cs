using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Internal
{
    /// <summary>The reason why an index of the published list can not be translated.</summary>
    internal enum UntrackableReason
    {
        None,

        /// <summary>A pending change has removed the element itself.</summary>
        Removed,

        /// <summary>A pending reset has replaced the whole content, so no index can be translated.</summary>
        Reset,
    }

    /// <summary>
    /// Holds the list as the subscribers see it, when notifications are deferred by ICollectionEventDispatcher.
    /// Applying changes at the same time as raising the notification keeps the content of the notification
    /// and the state of the list consistent.
    /// Not thread-safe, the caller must synchronize.
    /// </summary>
    internal sealed class DeferredViewList<TView>
    {
        /// <summary>Returned by ToWriterIndex when the index can not be tracked.</summary>
        public const int UntrackableIndex = -1;

        readonly List<TView> published;
        readonly Queue<PendingChange> pending = new();

        // each pending change gets an increasing sequence and they are applied in that order,
        // so an event is still pending exactly while its sequence is above the applied one
        long lastEnqueuedSequence;
        long lastAppliedSequence;

        public DeferredViewList(IEnumerable<TView> initialItems)
        {
            published = initialItems.ToList();
        }

        public int PendingCount => pending.Count;

        public int Count => published.Count;

        public TView this[int index] => published[index];

        public IEnumerable<TView> Items => published;

        public bool Contains(TView item) => published.Contains(item);

        public int IndexOf(TView item) => published.IndexOf(item);

        public void Enqueue(CollectionEventDispatcherEventArgs ev, TView[]? resetSnapshot)
        {
            ev.DeferredSequence = ++lastEnqueuedSequence;
            pending.Enqueue(new PendingChange(ev, resetSnapshot));
        }

        /// <summary>
        /// Applies the change without raising a notification.
        /// Only allowed when there is no subscriber and no pending change.
        /// </summary>
        public void ApplyWithoutNotification(CollectionEventDispatcherEventArgs ev, TView[]? resetSnapshot)
        {
            Apply(new PendingChange(ev, resetSnapshot));
        }

        /// <summary>
        /// Applies the oldest pending change, as long as <paramref name="ev"/> is still pending.
        /// A dispatcher may raise some events synchronously and others asynchronously, so an event must not be
        /// applied before the older ones are applied and raised. The caller raises <paramref name="applied"/> and
        /// calls again until it is <paramref name="ev"/> itself.
        /// Returns false when <paramref name="ev"/> has been applied already.
        /// </summary>
        public bool TryApplyNext(CollectionEventDispatcherEventArgs ev, out CollectionEventDispatcherEventArgs applied)
        {
            // an event is pending exactly while it is newer than the last applied one. an event that was
            // never enqueued has a sequence of 0, which is never newer, so it is reported as applied.
            if (ev.DeferredSequence <= lastAppliedSequence)
            {
                applied = null!;
                return false;
            }

            var change = pending.Dequeue();
            lastAppliedSequence = change.Args.DeferredSequence;
            Apply(change);
            applied = change.Args;
            return true;
        }

        /// <summary>
        /// Translates an index of the published list into an index of the writer-side list,
        /// by replaying the pending changes. Returns UntrackableIndex when it can not be tracked.
        /// </summary>
        /// <param name="index">An index of the published list.</param>
        /// <param name="isInsertionPoint">
        /// true when the index means "before the element at this position" instead of the element itself.
        /// </param>
        /// <param name="reason">Why it can not be tracked. None when it can.</param>
        public int ToWriterIndex(int index, bool isInsertionPoint, out UntrackableReason reason)
        {
            reason = UntrackableReason.None;

            foreach (var change in pending)
            {
                var e = change.Args;
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewStartingIndex <= index)
                        {
                            index += e.NewItems!.Count;
                        }
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        {
                            var start = e.OldStartingIndex;
                            var count = e.OldItems!.Count;
                            if (start + count <= index)
                            {
                                index -= count;
                            }
                            else if (start <= index)
                            {
                                // the element itself has been removed
                                if (!isInsertionPoint)
                                {
                                    reason = UntrackableReason.Removed;
                                    return UntrackableIndex;
                                }
                                index = start;
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Move:
                        {
                            var from = e.OldStartingIndex;
                            var to = e.NewStartingIndex;
                            if (from == index && !isInsertionPoint)
                            {
                                index = to;
                            }
                            else
                            {
                                if (from < index) index--;
                                if (to <= index) index++;
                            }
                        }
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        reason = UntrackableReason.Reset;
                        return UntrackableIndex;
                    default: // Replace does not move any element
                        break;
                }
            }

            return index;
        }

        void Apply(in PendingChange change)
        {
            var e = change.Args;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        var items = e.NewItems!;
                        for (var i = 0; i < items.Count; i++)
                        {
                            published.Insert(e.NewStartingIndex + i, (TView)items[i]!);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    published.RemoveRange(e.OldStartingIndex, e.OldItems!.Count);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    {
                        var items = e.NewItems!;
                        for (var i = 0; i < items.Count; i++)
                        {
                            published[e.NewStartingIndex + i] = (TView)items[i]!;
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Move:
                    {
                        // the writer side calculates NewStartingIndex after the removal, so apply in the same order
                        var moved = published[e.OldStartingIndex];
                        published.RemoveAt(e.OldStartingIndex);
                        published.Insert(e.NewStartingIndex, moved);
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    published.Clear();
                    if (change.ResetSnapshot != null)
                    {
                        published.AddRange(change.ResetSnapshot);
                    }
                    break;
                default:
                    break;
            }
        }

        readonly struct PendingChange
        {
            public readonly CollectionEventDispatcherEventArgs Args;

            /// <summary>Reset does not carry items, so the writer-side content is captured when it is posted.</summary>
            public readonly TView[]? ResetSnapshot;

            public PendingChange(CollectionEventDispatcherEventArgs args, TView[]? resetSnapshot)
            {
                Args = args;
                ResetSnapshot = resetSnapshot;
            }
        }
    }
}
