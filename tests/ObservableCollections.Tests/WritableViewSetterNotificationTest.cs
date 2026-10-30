using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace ObservableCollections.Tests;

/// <summary>
/// Whenever the setter of a writable view changes the visible content, it must come with a notification.
/// When it writes to the source, the Replace from the source is that notification; when it does not,
/// the setter itself has to raise one.
///
/// T and TView are different types so that the shortcut that skips the converter
/// (typeof(T) == typeof(TView)) does not interfere.
/// </summary>
public class WritableViewSetterNotificationTest
{
    static int ToOriginal(string newView, int original, ref bool setValue)
    {
        return int.Parse(newView.Substring(1));
    }

    static int RejectSourceWrite(string newView, int original, ref bool setValue)
    {
        setValue = false;
        return original;
    }

    static ObservableList<int> CreateSource()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        return list;
    }

    /// <summary>
    /// When the converter rejects the write to the source (setValue == false), no Replace comes from the source.
    /// If the setter only rewrote the visible content without notifying, subscribers would have no way to
    /// learn about the change.
    /// Verifies that the notification is raised even when no dispatcher is specified.
    /// </summary>
    [Fact]
    public void ConverterRejectedWriteRaisesReplaceWithoutDispatcher()
    {
        var list = CreateSource();

        using var view = list.CreateWritableView(x => $"${x}");
        using var bindable = view.ToWritableNotifyCollectionChanged(RejectSourceWrite);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable[1] = "$99";

        list.Should().Equal(new[] { 1, 2, 3 }); // Rejected, so the source is unchanged
        bindable.Should().Equal(new[] { "$1", "$99", "$3" });

        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        events[0].NewStartingIndex.Should().Be(1);
        events[0].NewItems![0].Should().Be("$99");
        events[0].OldItems![0].Should().Be("$2");
    }

    /// <summary>
    /// Verifies that, on a view without a filter (NonFilteredSynchronizedViewList), a write that the converter
    /// rejects (setValue == false) leaves the source unchanged, but still changes the visible content and raises a
    /// Replace for it.
    /// </summary>
    [Fact]
    public void ConverterRejectedWriteRaisesReplaceOnNonFilteredView()
    {
        var list = CreateSource();

        using var bindable = list.ToWritableNotifyCollectionChanged(x => $"${x}", RejectSourceWrite);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable[1] = "$99";

        list.Should().Equal(new[] { 1, 2, 3 });
        bindable.Should().Equal(new[] { "$1", "$99", "$3" });

        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        events[0].NewStartingIndex.Should().Be(1);
    }

    /// <summary>
    /// With a dispatcher, the notification for a rejected write is deferred like any other notification,
    /// and the visible content does not change until it is raised.
    /// </summary>
    [Fact]
    public void ConverterRejectedWriteIsDeferredWithDispatcher()
    {
        var list = CreateSource();

        var dispatcher = new QueuedCollectionEventDispatcher();
        using var bindable = list.ToWritableNotifyCollectionChanged(x => $"${x}", RejectSourceWrite, dispatcher);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable[1] = "$99";

        bindable.Should().Equal(new[] { "$1", "$2", "$3" }); // Not notified yet, so unchanged
        events.Should().BeEmpty();

        dispatcher.Pump();

        bindable.Should().Equal(new[] { "$1", "$99", "$3" });
        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Replace);
        events[0].NewStartingIndex.Should().Be(1);
    }

    /// <summary>
    /// When the write to the source fails, its Replace notification never arrives.
    /// If the setter had rewritten the visible content beforehand, an unnotified change would be left behind.
    /// Here a subscriber registered before the view throws an exception, so that the source's notification
    /// never reaches the view.
    /// (The source itself has already been rewritten at this point; its divergence from the view is the separate
    /// issue #142, where an exception from a subscriber stops the change from reaching the later subscribers.)
    /// </summary>
    [Fact]
    public void FailedSourceWriteDoesNotChangeVisibleContentSilently()
    {
        var list = CreateSource();
        list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> _) => throw new InvalidOperationException("boom");

        var dispatcher = new QueuedCollectionEventDispatcher();
        using var bindable = list.ToWritableNotifyCollectionChanged(x => $"${x}", ToOriginal, dispatcher);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable.Invoking(x => x[1] = "$99").Should().Throw<InvalidOperationException>();

        dispatcher.Pump();

        events.Should().BeEmpty();
        bindable.Should().Equal(new[] { "$1", "$2", "$3" });
    }

    /// <summary>
    /// Verifies that, on a filtered view, a setter whose write to the source fails leaves the visible content
    /// unchanged and raises no notification. Here the index in the view and the index in the source differ.
    /// </summary>
    [Fact]
    public void FailedSourceWriteDoesNotChangeVisibleContentSilentlyOnFilteredView()
    {
        var list = new ObservableList<int>();
        list.Add(1);
        list.Add(2);
        list.Add(3);
        list.Add(4);

        // This has to be registered before the view; otherwise the view's handler runs first.
        list.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> _) => throw new InvalidOperationException("boom");

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        var dispatcher = new QueuedCollectionEventDispatcher();
        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal, dispatcher);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        // The view is ["$2", "$4"]. Its [1] is source index 3.
        bindable.Invoking(x => x[1] = "$99").Should().Throw<InvalidOperationException>();

        dispatcher.Pump();

        events.Should().BeEmpty();
        bindable.Should().Equal(new[] { "$2", "$4" });
    }

    /// <summary>
    /// When the setter writes a value that the filter rejects, the Replace of the source takes the element out of the
    /// filtered view, and that Remove is the notification of the change.
    /// To remove the element, the view has to find the old one. So the setter must not overwrite the old view in the
    /// parent before the source write, and it must not change the visible content by itself either.
    /// Covers both raising immediately and deferring with a dispatcher.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetterTakingElementOutOfFilterRemovesIt(bool withDispatcher)
    {
        var list = new ObservableList<int>();
        list.Add(2);
        list.Add(4);
        list.Add(6);

        using var view = list.CreateWritableView(x => $"${x}");
        view.AttachFilter(x => x % 2 == 0);

        var dispatcher = withDispatcher ? new QueuedCollectionEventDispatcher() : null;
        using var bindable = view.ToWritableNotifyCollectionChanged(ToOriginal, dispatcher);

        var events = new List<NotifyCollectionChangedEventArgs>();
        bindable.CollectionChanged += (_, e) => events.Add(e);

        bindable[0] = "$3";

        list.Should().Equal(new[] { 3, 4, 6 });

        if (dispatcher != null)
        {
            // Deferred, so nothing has changed yet.
            bindable.Should().Equal(new[] { "$2", "$4", "$6" });
            events.Should().BeEmpty();

            dispatcher.Pump();
        }

        bindable.Should().Equal(new[] { "$4", "$6" });

        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Remove);
        events[0].OldStartingIndex.Should().Be(0);
        events[0].OldItems![0].Should().Be("$2");
    }
}
