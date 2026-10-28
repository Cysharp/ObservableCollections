namespace ObservableCollections.Tests;

/// <summary>
/// A dispatcher that raises every notification immediately, like a UI thread dispatcher on its own thread.
/// Unlike passing no dispatcher, passing this one still makes the view defer its visible content.
/// </summary>
internal sealed class SynchronousCollectionEventDispatcher : ICollectionEventDispatcher
{
    public static readonly SynchronousCollectionEventDispatcher Instance = new();

    SynchronousCollectionEventDispatcher()
    {
    }

    public void Post(CollectionEventDispatcherEventArgs ev)
    {
        ev.Invoke();
    }
}
