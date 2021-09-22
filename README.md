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
// Basic sample, use like ObservableCollection<T>.
// CollectionChanged observes all collection modification
var list = new ObservableList<int>();
list.CollectionChanged += List_CollectionChanged;

list.Add(10);
list.Add(20);
list.AddRange(new[] { 10, 20, 30 });

static void List_CollectionChanged(in NotifyCollectionChangedEventArgs<int> e)
{
    switch (e.Action)
    {
        case NotifyCollectionChangedAction.Add:
            if (e.IsSingleItem)
            {
                Console.WriteLine(e.NewItem);
            }
            else
            {
                foreach (var item in e.NewItems)
                {
                    Console.WriteLine(item);
                }
            }
            break;
        // Remove, Replace, Move, Reset
        default:
            break;
    }
}
```

Handling all `CollectionChanged` event manually is hard. We recommend to use `SynchronizedView` that transform element and handling all collection changed event for view synchronize.

```csharp
var list = new ObservableList<int>();
var view = list.CreateView(x => x.ToString() + "$");

list.Add(10);
list.Add(20);
list.AddRange(new[] { 30, 40, 50 });
list[1] = 60;
list.RemoveAt(3);

foreach (var (_, v) in view)
{
    // 10$, 60$, 30$, 50$
    Console.WriteLine(v);
}

// Dispose view is unsubscribe collection changed event.
view.Dispose();
```

Blazor
---



```csharp
public partial class DataTable<T> : ComponentBase, IDisposable
{
    [Parameter, EditorRequired]
    public IReadOnlyList<T> Items { get; set; } = default!;

    [Parameter, EditorRequired]
    public Func<T, Cell[]> DataTemplate { get; set; } = default!;

    ISynchronizedView<T, Cell[]> view = default!;

    protected override void OnInitialized()
    {
        // TODO: CreateSortedView
        if (Items is IObservableCollection<T> observableCollection)
        {
            view = observableCollection.CreateView(DataTemplate);
        }
        else
        {
            var freezedList = new FreezedList<T>(Items);
            view = freezedList.CreateView(DataTemplate);
        }

        // TODO: what do.
        view.CollectionStateChanged += async _ =>
        {
            await InvokeAsync(StateHasChanged);
        };
    }
    
    public void Dispose()
    {
        // unsubscribe.
        view.Dispose();
    }
}

// .razor, iterate view
@foreach (var (row, cells) in view)
{
    <tr>
        @foreach (var item in cells)
        {
            <td>
                <CellView Item="item" />
            </td>
        }
    </tr>                    
}
```

WPF
---

```csharp
// WPF simple sample.

ObservableList<int> list;
public ISynchronizedView<int, int> ItemsView { get; set; }

public MainWindow()
{
    InitializeComponent();
    this.DataContext = this;

    list = new ObservableList<int>();
    ItemsView = list.CreateSortedView(x => x, x => x, comparer: Comparer<int>.Default).WithINotifyCollectionChanged();

    BindingOperations.EnableCollectionSynchronization(ItemsView, new object()); // for ui synchronization safety of viewmodel
}

protected override void OnClosed(EventArgs e)
{
    ItemsView.Dispose();
}
```

Unity
---

```csharp
// Unity, with filter sample.

public class SampleScript : MonoBehaviour
{
    public Button prefab;
    public GameObject root;
    ObservableRingBuffer<int> collection;
    ISynchronizedView<GameObject> view;

    void Start()
    {
        collection = new ObservableRingBuffer<int>();
        view = collection.CreateView(x =>
        {
            var item = GameObject.Instantiate(prefab);
            item.GetComponentInChildren<Text>().text = x.ToString();
            return item.gameObject;
        });
        view.AttachFilter(new GameObjectFilter(root));
    }

    void OnDestroy()
    {
        view.Dispose();
    }

    public class GameObjectFilter : ISynchronizedViewFilter<int, GameObject>
    {
        readonly GameObject root;

        public GameObjectFilter(GameObject root)
        {
            this.root = root;
        }

        public void OnCollectionChanged(ChangedKind changedKind, int value, GameObject view, in NotifyCollectionChangedEventArgs<int> eventArgs)
        {
            if (changedKind == ChangedKind.Add)
            {
                view.transform.SetParent(root.transform);
            }
            else if (changedKind == ChangedKind.Remove)
            {
                GameObject.Destroy(view);
            }
        }

        public bool IsMatch(int value, GameObject view)
        {
            return value % 2 == 0;
        }

        public void WhenTrue(int value, GameObject view)
        {
            view.SetActive(true);
        }

        public void WhenFalse(int value, GameObject view)
        {
            view.SetActive(false);
        }
    }
}
```

TODO: write more usage...

View/SoretedView
---


Filter
---

Collections
---

Freezed
---


License
---
This library is licensed under the MIT License.