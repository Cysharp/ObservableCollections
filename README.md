# ObservableCollections 🔬

[![GitHub Actions](https://github.com/Cysharp/ObservableCollections/workflows/Build-Debug/badge.svg)](https://github.com/Cysharp/ObservableCollections/actions) [![Releases](https://img.shields.io/github/release/Cysharp/ObservableCollections.svg)](https://github.com/Cysharp/ObservableCollections/releases) [![Nuget](https://img.shields.io/nuget/v/ObservableCollections?label=nuget%20package)
](https://www.nuget.org/packages/ObservableCollections)

`ObservableCollections` is a .Net library for high performance, generic, observable collections.

Supported collection types:
- `ObservableList<T>`
- `ObservableDictionary<TKey, TValue>`
- `ObservableHashSet<T>`
- `ObservableQueue<T>`
- `ObservableStack<T>`
- `ObservableRingBuffer<T>`
- `ObservableFixedSizeRingBuffer<T>`

## Why a new package?

.Net already has [`ObservableCollection<T>`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.observablecollection-1), so why would a new library be necessary? Some reasons: unnecessary memory allocations due to the lack of generic and poor design choices, no support for ranges or batch operations resulting in unnecessary events when inserting multiple items at once, not thread-safe.

### Too much memory allocated

- `ObservableCollection<T>` is based on `INotifyCollectionChanged`, `NotifyCollectionChangedEventHandler` and `NotifyCollectionChangedEventArgs`. These types are not generic, everything boxed and does allocate every time they are used.
- `NotifyCollectionChangedEventArgs` holds all values in a `IList`, even when only one single item changed, causing too much memory allocation.

## Our solution

### Event arguments never leave the stack

`ObservableCollections` introduces the generic types `NotifyCollectionChangedEventHandler<T>` and `NotifyCollectionChangedEventArgs<T>`, and rely on modern C# and .Net features such as ["in" parameter modifier](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/in-parameter-modifier), [readonly ref struct](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/struct#ref-struct), and [ReadOnlySpan<T>](https://docs.microsoft.com/en-us/dotnet/api/system.readonlyspan-1?view=net-6.0) to safely pass event args by reference and avoid heap allocations.

```csharp
// event arg is passed as readonly argument, zero heap allocation
//                                                          👇
public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);

// ref structs are always allocated on the stack
//              👇
public readonly ref struct NotifyCollectionChangedEventArgs<T>
{
    public readonly NotifyCollectionChangedAction Action;
    public readonly bool IsSingleItem;
    public readonly T NewItem;
    public readonly T OldItem;
    // ReadOnlySpan is always allocated on the stack, is never promoted to the heap, and cannot be boxed
    //                 👇
    public readonly ReadOnlySpan<T> NewItems;
    public readonly ReadOnlySpan<T> OldItems;
    public readonly int NewStartingIndex;
    public readonly int OldStartingIndex;
}
```

### Thread-safe interface to publish/subscribe to changes

`ObservableCollections` is based on the generic interface `IObservableCollection<T>` instead of `INotifyCollectionChanged`. This is guaranteed to be thread-safe and can produce a View that is kept in sync with the collection.

```csharp
public interface IObservableCollection<T> : IReadOnlyCollection<T>
{
    event NotifyCollectionChangedEventHandler<T> CollectionChanged;
    object SyncRoot { get; }
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
}
```

### Synchronized views

The concept of `SynchronizedView` help create a separation between our View and Model (in an MVVM sense). Collections from `ObservableCollections` are used by Model definitions, and a `SynchronizedView` is consumed by Views, through their ViewModels. This architecture applies well to any MVVM context and can be leveraged by WPF, Blazor, Unity, etc.

![image](https://user-images.githubusercontent.com/46207/131979264-2463403b-0fba-474b-8f49-277c2abe1b05.png)

In addition a `SynchronizedView` can be sorted and/or filtered by using `CreateSortedView`.

### Batch insert

`AddRange` can be used to efficiently insert multiple items at once, and will only generate one single event instead of one per inserted item.


## Install

From `dotnet` CLI:

```
$ dotnet add package ObservableCollections
```

From PackageManager CLI
```
PM> Install-Package ObservableCollections
```


## Usage

### Base example: ObservableList and SynchronizedView

In this example we define a simple `ObservableList<int>` and attach an event handler for `CollectionChanged`.

```csharp
var list = new ObservableList<int>();

// CollectionChanged observes all collection modification.
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

Supporting all `CollectionChanged` actions manually can be hard and tedious. Instead we can use a `SynchronizedView` that observes changes and stay in sync with the original collection, while applying some transformation if necessary.

```csharp
var list = new ObservableList<int>();
// Create a view that maps 1, 2, 3,... to "1$", "2$", "3$",...
var view = list.CreateView(x => x.ToString() + "$");

list.Add(10);
list.Add(20);
list.AddRange(new[] { 30, 40, 50 });
list[1] = 60;
list.RemoveAt(3);

foreach (var (_, v) in view)
{
    // Print 10$, 60$, 30$, 50$
    Console.WriteLine(v);
}

// Dispose view to unsubscribe from collection changed events.
view.Dispose();
```

One of the base idea behind ObservableCollections is to use SynchronizedView to automate and simplify the data flow Model => ViewModel => View. A SynchronizedView can be automatically sorted, filtered, or trigger side effects when collection items change.

### Blazor example: Synchronized table cells

Since Blazor re-renders the whole thing based on `StateHasChanged`, you may think observable collections are redundant. However, when splitting a control into smaller components, it can be beneficial to have components react to a collection changes and update they own state.

Creating views from a collection can be very useful when working with tables, for example to convert data to a SynchronizedView to represent a table cell.

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
            // In cases where Items does not implement IObservableCollection, FreezedList can be used as a wrapper.
            // A freezed list offers the same API as other observable collections.
            var freezedList = new FreezedList<T>(Items);
            view = freezedList.CreateView(DataTemplate);
        }

        // SynchronizedView also exposes events for changes.
        view.CollectionStateChanged += async _ =>
        {
            await InvokeAsync(StateHasChanged);
        };
    }

    public void Dispose()
    {
        // On dispose unsubscribe from sync events
        view.Dispose();
    }
}
```

```csharp
// In the .razor file, iterate over 'view'
@foreach (var (row, cells) in view)
{
    <tr>
        @foreach (var item in cells)
        {
            <td>
                <CellView Item="@item" />
            </td>
        }
    </tr>
}
```

### WPF example: Safely work from different threads

Due to the way data binding works in WPF a collection needs to be `IObservable`. In this context `ObservableCollections` high-performance `IObservableCollection<T>` cannot be used directly, and has to be converted by using `WithINotifyCollectionChanged`.

> ⚠️ While observable collections and synchronized views are thread-safe, WPF requires UI changes to be notified from the UI thread. In this case you have to call `BindingOperations.EnableCollectionSynchronization` to bind to the correct thread.

```csharp
ObservableList<int> list;
public ISynchronizedView<int, int> ItemsView { get; set; }

public MainWindow()
{
    InitializeComponent();
    this.DataContext = this;

    list = new ObservableList<int>();

    // Create a SynchronizedView that implements IObservable
    ItemsView = list.CreateView(x => x).WithINotifyCollectionChanged();

    // Bind to the UI thread
    BindingOperations.EnableCollectionSynchronization(ItemsView, new object());
}

protected override void OnClosed(EventArgs e)
{
    // Unsubscribe from sync events
    ItemsView.Dispose();
}
```

> ⚠️ SortedView cannot be used in the context of WPF as it does not trigger `INotifyCollectionChanged` for sorting event.

### Unity example
---
In the context of Unity, Prefabs have to be created to be rendered. Observable collections and synchronized views are useful as `CollectionManagers`, converting the given data of type `T` to Prefab.

Using a filter makes it convenient to update `GameObject`. That way we can react to change events and update our game objects as side effects.

```csharp
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

        // Bind a GameObjectFilter to our SynchronizedView. The filter will take care of updating the GameObject.
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

## SynchronizedView

A `SynchronizedView` is created from `IObservableCollection<T>.CreateView`. It is completely synchronized and thread-safe.

```csharp
public interface IObservableCollection<T> : IReadOnlyCollection<T>
{
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform, bool reverse = false);
}
```

When `reverse = true` the SynchronizedView is enumerated in reverse order (only for sorted collections: no effect for dictionaries and sets).

`ISynchronizedView<T, TView>` implements `IReadOnlyCollection` and holds two collections:
1. the original values
2. the transformed values, aka the "views", as a result of `transform(T value) => TView`


```csharp
public interface ISynchronizedView<T, TView> : IReadOnlyCollection<(T Value, TView View)>, IDisposable
{
    object SyncRoot { get; }

    event NotifyCollectionChangedEventHandler<T>? RoutingCollectionChanged;
    event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

    void AttachFilter(ISynchronizedViewFilter<T, TView> filter);
    void ResetFilter(Action<T, TView>? resetAction);
    INotifyCollectionChangedSynchronizedView<T, TView> WithINotifyCollectionChanged();
}
```

### Sorting

While it is possible to control the ordering by managing indexes inserted from event args, that can be difficult and comes with many caveats. Unless you have some major performance concerns, it is simpler to loop over the SynchronizedView itself when a `CollectionStateChanged` event is trigger. Or even better, use `CreateSortedView`.

```csharp
public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<T> comparer)
    where TKey : notnull

public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, IComparer<TView> viewComparer)
    where TKey : notnull

public static ISynchronizedView<T, TView> CreateSortedView<T, TKey, TView, TCompare>(this IObservableCollection<T> source, Func<T, TKey> identitySelector, Func<T, TView> transform, Func<T, TCompare> compareSelector, bool ascending = true)
    where TKey : notnull
```

> ⚠️ Using `foreach` over observable collections and synchronized views is thread-safe but will use locks during iteration. Because of this the obtained Enumerator **must** be Dispose. `foreach` and LINQ guarantee that Dipose is called properly, but be **very careful** if you extract the Enumerator by hand.

### Filters

```csharp
var view = transform(value);
If (filter.IsMatch(value, view))
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
public interface ISynchronizedViewFilter<T, TView>
{
    bool IsMatch(T value, TView view);
    void WhenTrue(T value, TView view);
    void WhenFalse(T value, TView view);
    void OnCollectionChanged(ChangedKind changedKind, T value, TView view, in NotifyCollectionChangedEventArgs<T> eventArgs);
}

public enum ChangedKind
{
    Add, Remove, Move
}
```

## Observable collections


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

## Freezed collections

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

## License

This library is licensed under the MIT License.
