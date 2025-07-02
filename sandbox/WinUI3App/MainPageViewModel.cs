using ObservableCollections;

namespace WinUI3App;

public class MainPageViewModel
{
    public MainPageViewModel(SampleService sampleService)
    {
        var view = sampleService.Items.CreateView(x => x);
        Items = view.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);
    }

    public NotifyCollectionChangedSynchronizedViewList<Item> Items { get; private set; }
}
