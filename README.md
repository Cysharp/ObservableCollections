# ObservableCollections
[![GitHub Actions](https://github.com/Cysharp/ObservableCollections/workflows/Build-Debug/badge.svg)](https://github.com/Cysharp/ObservableCollections/actions) [![Releases](https://img.shields.io/github/release/Cysharp/ObservableCollections.svg)](https://github.com/Cysharp/ObservableCollections/releases)

ObservableCollections is a high performance observable collections(`ObservableList<T>`, `ObservableDictionary<TKey, TValue>`, `ObservableHashSet<T>`, `ObservableQueue<T>`, `ObservableStack<T>`, `ObservableRingBuffer<T>`, `ObservableFixedSizeRingBuffer<T>`) with synchronized views and Observe Extension for [R3](https://github.com/Cysharp/R3).

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

If you want to handle each change event with Rx, you can monitor it with the following method by combining it with [R3](https://github.com/Cysharp/R3):

```csharp
Observable<CollectionAddEvent<T>> IObservableCollection<T>.ObserveAdd()
Observable<CollectionRemoveEvent<T>> IObservableCollection<T>.ObserveRemove()
Observable<CollectionReplaceEvent<T>> IObservableCollection<T>.ObserveReplace() 
Observable<CollectionMoveEvent<T>> IObservableCollection<T>.ObserveMove() 
Observable<CollectionResetEvent<T>> IObservableCollection<T>.ObserveReset()
```

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

The basic idea behind using ObservableCollections is to create a View. In order to automate this pipeline, the view can be sortable, filtered, and have side effects on the values when they are changed.

Reactive Extensions with R3
---
Once the R3 extension package is installed, you can subscribe to `ObserveAdd`, `ObserveRemove`, `ObserveReplace`, `ObserveMove`, and `ObserveReset` events as Rx, allowing you to compose events individually.

PM> Install-Package [ObservableCollections.R3](https://www.nuget.org/packages/ObservableCollections.R3)

```csharp
using R3;
using ObservableCollections;

var list = new ObservableList<int>();
list.ObserveAdd()
    .Subscribe(x =>
    {
        Console.WriteLine(x);
    });

list.Add(10);
list.Add(20);
list.AddRange(new[] { 10, 20, 30 });
```

Since it is not supported by dotnet/reactive, please use the Rx library [R3](https://github.com/Cysharp/R3).

Blazor
---
Since Blazor re-renders the whole thing by StateHasChanged, you may think that Observable collections are unnecessary. However, when you split it into Components, it is beneficial for Component confidence to detect the change and change its own State.

The View selector in ObservableCollections is also useful for converting data to a View that represents a Cell, for example, when creating something like a table.

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
        if (Items is IObservableCollection<T> observableCollection)
        {
            // Note: If the table has the ability to sort columns, then it will be automatically sorted using SortedView.
            view = observableCollection.CreateView(DataTemplate);
        }
        else
        {
            // It is often the case that Items is not Observable.
            // In that case, FreezedList is provided to create a View with the same API for normal collections.
            var freezedList = new FreezedList<T>(Items);
            view = freezedList.CreateView(DataTemplate);
        }

        // View also has a change notification. 
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

WPF/Avalonia
---
Because of data binding in WPF, it is important that the collection is Observable. ObservableCollections high-performance `IObservableCollection<T>` cannot be bind to WPF. Call `ToNotifyCollectionChanged()` to convert it to `INotifyCollectionChanged`. Also, although ObservableCollections and Views are thread-safe, the WPF UI does not support change notifications from different threads. To`ToNotifyCollectionChanged(IColllectionEventDispatcher)` allows multi thread changed.

```csharp
// WPF simple sample.

ObservableList<int> list;
public INotifyCollectionChangedSynchronizedView<int> ItemsView { get; set; }

public MainWindow()
{
    InitializeComponent();
    this.DataContext = this;

    list = new ObservableList<int>();

    // for ui synchronization safety of viewmodel
    ItemsView = list.CreateView(x => x).ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    // if collection is changed only from ui-thread, can use this overload
    // ItemsView = list.CreateView(x => x).ToNotifyCollectionChanged();
}

protected override void OnClosed(EventArgs e)
{
    ItemsView.Dispose();
}
```

> WPF can not use SortedView because SortedView can not provide sort event to INotifyCollectionChanged.

`SynchronizationContextCollectionEventDispatcher.Current` is default implementation of `IColllectionEventDispatcher`, it is used `SynchronizationContext.Current` for dispatche ui thread. You can create custom `ICollectionEventDispatcher` to use custom dispatcher object. For example use WPF Dispatcher:

```csharp
public class WpfDispatcherCollection(Dispatcher dispatcher) : ICollectionEventDispatcher
{
    public void Post(CollectionEventDispatcherEventArgs ev)
    {
        dispatcher.InvokeAsync(() =>
        {
            // notify in dispatcher
            ev.Invoke();
        });
    }
}
```

Unity
---
In Unity projects, you can installing `ObservableCollections` with [NugetForUnity](https://github.com/GlitchEnzo/NuGetForUnity). If R3 integration is required, similarly install `ObservableCollections.R3` via NuGetForUnity.

In Unity, ObservableCollections and Views are useful as CollectionManagers, since they need to convert T to Prefab for display.

Since we need to have side effects on GameObjects, we will prepare a filter and apply an action on changes.

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

        public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<int, GameObject> eventArgs)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Add)
            {
                eventArgs.NewView.transform.SetParent(root.transform);
            }
            else if (NotifyCollectionChangedAction.Remove)
            {
                GameObject.Destroy(eventArgs.OldView);
            }
        }

        public bool IsMatch(int value, GameObject view)
        {
            return true;
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

It is also possible to manage Order by managing indexes inserted from eventArgs, but it is very difficult with many caveats. If you don't have major performance issues, you can foreach the View itself on CollectionStateChanged (like Blazor) and reorder the transforms. If you have such a architecture, you can also use SortedView.

View/SortedView
---
View can create from `IObservableCollection<T>`, it completely synchronized and thread-safe.

```csharp
public interface IObservableCollection<T> : IReadOnlyCollection<T>
{
    // snip...
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
}
```

When reverse = true, foreach view as reverse order(Dictionary, etc. are not supported).

`ISynchronizedView<T, TView>` is `IReadOnlyCollection` and hold both value and view(transformed value when added).

```csharp
public interface ISynchronizedView<T, TView> : IReadOnlyCollection<(T Value, TView View)>, IDisposable
{
    object SyncRoot { get; }

    event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
    event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

    void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
    void ResetFilter(Action<T, TView>? resetAction);
    INotifyCollectionChangedSynchronizedView<T, TView> ToNotifyCollectionChanged();
}
```



see [filter](#filter) section.



```csharp
var view = transform(value);
if (filter.IsMatch(value, view))
{
    filter.WhenTrue(value, view);
}
else
{
    filter.WhenFalse(value, view);
}
AddToCollectionInnerStructure(value, view);
filter.OnCollectionChanged(ChangeKind.Add, value, view, eventArgs);
RoutingCollectionChanged(eventArgs);
CollectionStateChanged();
```


```csharp
public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
    where TKey : notnull

public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer)
    where TKey : notnull

public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView, TCompare>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, Func<T, TCompare> compareSelector, bool ascending = true)
    where TKey : notnull
```

> Notice: foreach ObservableCollections and Views are thread-safe but it uses lock at iterating. In other words, the obtained Enumerator must be Dispose. foreach and LINQ are guaranteed to be Dispose, but be careful when you extract the Enumerator by yourself.

Filter
---

```csharp
public interface ISynchronizedViewFilter<T, TView>
{
    bool IsMatch(T value, TView view);
    void WhenTrue(T value, TView view);
    void WhenFalse(T value, TView view);
    void OnCollectionChanged(in SynchronizedViewChangedEventArgs<T, TView> eventArgs);
}

public readonly struct SynchronizedViewChangedEventArgs<T, TView>
{
    public readonly NotifyCollectionChangedAction Action = action;
    public readonly T NewValue = newValue;
    public readonly T OldValue = oldValue;
    public readonly TView NewView = newView;
    public readonly TView OldView = oldView;
    public readonly int NewViewIndex = newViewIndex;
    public readonly int OldViewIndex = oldViewIndex;
}
```


Collections
---

```csharp
public sealed partial class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IObservableCollection<KeyValuePair<TKey, TValue>> where TKey : notnull
public sealed partial class ObservableFixedSizeRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
public sealed partial class ObservableHashSet<T> : IReadOnlySet<T>, IReadOnlyCollection<T>, IObservableCollection<T> where T : notnull

public sealed partial class ObservableHashSet<T> : IReadOnlySet<T>, IReadOnlyCollection<T>, IObservableCollection<T>
        where T : notnull

public sealed partial class ObservableList<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>

public sealed partial class ObservableQueue<T> : IReadOnlyCollection<T>, IObservableCollection<T>
public sealed partial class ObservableRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>

public sealed partial class ObservableStack<T> : IReadOnlyCollection<T>, IObservableCollection<T>

public sealed class RingBuffer<T> : IList<T>, IReadOnlyList<T>
```

Freezed
---


```csharp
public sealed class FreezedList<T> : IReadOnlyList<T>, IFreezedCollection<T>
public sealed class FreezedDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>, IFreezedCollection<KeyValuePair<TKey, TValue>> where TKey : notnull


public interface IFreezedCollection<T>
{
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
    ISortableSynchronizedView<T, TView> CreateSortableView<TView>(Func<T, TView> transform);
}

public static ISortableSynchronizedView<T, TView> CreateSortableView<T, TView>(this IFreezedCollection<T> source, Func<T, TView> transform, IComparer<T> initialSort)
public static ISortableSynchronizedView<T, TView> CreateSortableView<T, TView>(this IFreezedCollection<T> source, Func<T, TView> transform, IComparer<TView> initialViewSort)
public static ISortableSynchronizedView<T, TView> CreateSortableView<T, TView, TCompare>(this IFreezedCollection<T> source, Func<T, TView> transform, Func<T, TCompare> initialCompareSelector, bool ascending = true)
public static void Sort<T, TView, TCompare>(this ISortableSynchronizedView<T, TView> source, Func<T, TCompare> compareSelector, bool ascending = true)
```

License
---
This library is licensed under the MIT License.
