using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace ObservableCollections.Tests;

/// <summary>
/// Verifies the contract that "an observer that only watches the CollectionChanged notifications can fully
/// reconstruct the content of the collection".
/// Applies every received notification to a shadow list and checks each time that the result matches
/// the actual collection.
/// There is no need to trace individual indices by eye; this alone catches both index and Count mismatches.
/// </summary>
internal sealed class NotifyCollectionChangedContractTracker<T>
{
    readonly IReadOnlyList<T> target;
    readonly List<T> shadow;

    public NotifyCollectionChangedContractTracker(NotifyCollectionChangedSynchronizedViewList<T> target)
    {
        this.target = target;

        // A snapshot at the time the subscription starts.
        this.shadow = new List<T>();
        Resync();

        target.CollectionChanged += OnCollectionChanged;
    }

    readonly List<NotifyCollectionChangedAction> actions = new();
    readonly List<string> violations = new();

    public IReadOnlyList<NotifyCollectionChangedAction> Actions => actions;

    public IReadOnlyList<string> Violations => violations;

    void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        actions.Add(e.Action);

        try
        {
            Apply(e);
        }
        catch (Exception ex)
        {
            violations.Add($"{e.Action}: failed to apply the notification: {ex.GetType().Name}: {ex.Message}");
            return;
        }

        if (!shadow.SequenceEqual(target))
        {
            violations.Add($"{e.Action}: the content reconstructed from the notifications [{Join(shadow)}] does not match the actual content [{Join(target)}]");
        }
    }

    void Apply(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                InsertRange(e.NewStartingIndex, e.NewItems);

                // The same access the WinUI 3 ListView makes through IBindableVector.GetAt(index).
                // If the contract is broken, this throws (the crash site of issue #115).
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    _ = target[e.NewStartingIndex + i];
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                shadow.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                break;

            case NotifyCollectionChangedAction.Replace:
                for (var i = 0; i < e.NewItems.Count; i++)
                {
                    shadow[e.NewStartingIndex + i] = (T)e.NewItems[i];
                }
                break;

            case NotifyCollectionChangedAction.Move:
                var moved = shadow.GetRange(e.OldStartingIndex, e.OldItems.Count);
                shadow.RemoveRange(e.OldStartingIndex, e.OldItems.Count);
                shadow.InsertRange(e.NewStartingIndex, moved);
                break;

            case NotifyCollectionChangedAction.Reset:
                // Reset means "reread everything", so resync from the actual collection.
                Resync();
                break;
        }
    }

    void Resync()
    {
        // Read the target element by element, not with ToList / AddRange, which go through CopyTo, so that the
        // tracker does not depend on the CopyTo of the view under test.
        shadow.Clear();
        foreach (var item in target)
        {
            shadow.Add(item);
        }
    }

    void InsertRange(int index, IList items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            shadow.Insert(index + i, (T)items[i]);
        }
    }

    static string Join(IEnumerable<T> source)
    {
        return string.Join(", ", source);
    }
}
