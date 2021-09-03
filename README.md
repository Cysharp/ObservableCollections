# ObservableCollections
[![GitHub Actions](https://github.com/Cysharp/ObservableCollections/workflows/Build-Debug/badge.svg)](https://github.com/Cysharp/ObservableCollections/actions) [![Releases](https://img.shields.io/github/release/Cysharp/ObservableCollections.svg)](https://github.com/Cysharp/ObservableCollections/releases)

ObservableCollections is a high performance observable collections(`ObservableList<T>`, `ObservableDictionary<TKey, TValue>`, `ObservableHashSet<T>`, `ObservableQueue<T>`, `ObservableStack<T>`, `ObservableRingBuffer<T>`, `ObservableFixedSizeRingBuffer<T>`) with synchronized views.

.NET has [`ObservableCollection<T>`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.observablecollection-1), however it has many lacks of features.

It based `INotifyCollectionChanged`, `NotifyCollectionChangedEventHandler` and `NotifyCollectionChangedEventArgs`. There are no generics so everything boxed, allocate memory every time. Also `NotifyCollectionChangedEventArgs` holds all values to `IList` even if it is single value, this also causes allocations. `ObservableCollection<T>` has no Range feature so a lot of wastage occurs when adding multiple values, because it is a single value notification.  Also, it is not thread-safe is hard to do linkage with the notifier.

ObservableCollections introduces generics version of `NotifyCollectionChangedEventHandler` and `NotifyCollectionChangedEventArgs`, it using latest C# features(`in`, `readonly ref struct`, `ReadOnlySpan<T>`).

```csharp
public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);

public readonly ref struct NotifyCollectionChangedEventArgs<T>
{
    public readonly NotifyCollectionChangedAction Action;
    public readonly bool IsSingleItem;
    public readonly T NewItem;
    public readonly T OldItem;
    public readonly ReadOnlySpan<T> NewItems;
    public readonly ReadOnlySpan<T> OldItems;
    public readonly int NewStartingIndex;
    public readonly int OldStartingIndex;
}
```

Also, use the interface `IObservableCollection<T>` instead of `INotifyCollectionChanged`. This is guaranteed to be thread-safe and can produce a View that is fully synchronized with the collection.

```csharp
public interface IObservableCollection<T> : IReadOnlyCollection<T>
{
    event NotifyCollectionChangedEventHandler<T> CollectionChanged;
    object SyncRoot { get; }
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
}

// also exists SortedView
public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer);
public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer);
```
 
SynchronizedView helps to separate between Model and View (ViewModel). We will use ObservableCollections as the Model and generate SynchronizedView as the View (ViewModel). This architecture can be applied not only to WPF, but also to Blazor, Unity, etc.

![image](https://user-images.githubusercontent.com/46207/131979264-2463403b-0fba-474b-8f49-277c2abe1b05.png)

ObservableCollections has not just a simple list, there are many more data structures. `ObservableList<T>`, `ObservableDictionary<TKey, TValue>`, `ObservableHashSet<T>`, `ObservableQueue<T>`, `ObservableStack<T>`, `ObservableRingBuffer<T>`, `ObservableFixedSizeRingBuffer<T>`. `RingBuffer`, especially `FixedSizeRingBuffer`, can be achieved with efficient performance when there is rotation (e.g., displaying up to 1000 logs, where old ones are deleted when new ones are added). Of course, the AddRange allows for efficient batch processing of large numbers of additions.

Getting Started
---
For .NET, use NuGet. For Unity, please read [Unity](#unity) section.

PM> Install-Package [ObservableCollections](https://www.nuget.org/packages/ObservableCollections)

create new `ObservableList<T>`, `ObservableDictionary<TKey, TValue>`, `ObservableHashSet<T>`, `ObservableQueue<T>`, `ObservableStack<T>`, `ObservableRingBuffer<T>`, `ObservableFixedSizeRingBuffer<T>`.






```csharp
// WPF simple sample.

ObservableList<int> list;
public ISynchronizedView<int, int> ItemsView { get; set; }

public MainWindow()
{
    InitializeComponent();
    this.DataContext = this;

    list = new ObservableList<int>();
    list.AddRange(new[] { 1, 10, 188 });
    ItemsView = list.CreateSortedView(x => x, x => x, comparer: Comparer<int>.Default).WithINotifyCollectionChanged();

    BindingOperations.EnableCollectionSynchronization(ItemsView, new object()); // for ui synchronization safety of viewmodel
}

protected override void OnClosed(EventArgs e)
{
    ItemsView.Dispose();
}
```

TODO: write more usage...

View/SoretedView
---


Collections
---


Unity
---


License
---
This library is licensed under the MIT License.