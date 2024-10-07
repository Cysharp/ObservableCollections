# ObservableCollections
[![GitHub Actions](https://github.com/Cysharp/ObservableCollections/workflows/Build-Debug/badge.svg)](https://github.com/Cysharp/ObservableCollections/actions) [![Releases](https://img.shields.io/github/release/Cysharp/ObservableCollections.svg)](https://github.com/Cysharp/ObservableCollections/releases)

ObservableCollections is a high performance observable collections(`ObservableList<T>`, `ObservableDictionary<TKey, TValue>`, `ObservableHashSet<T>`, `ObservableQueue<T>`, `ObservableStack<T>`, `ObservableRingBuffer<T>`, `ObservableFixedSizeRingBuffer<T>`) with synchronized views and Observe Extension for [R3](https://github.com/Cysharp/R3).

.NET has [`ObservableCollection<T>`](https://docs.microsoft.com/en-us/dotnet/api/system.collections.objectmodel.observablecollection-1), however it has many lacks of features. It based `INotifyCollectionChanged`, `NotifyCollectionChangedEventHandler` and `NotifyCollectionChangedEventArgs`. There are no generics so everything boxed, allocate memory every time. Also `NotifyCollectionChangedEventArgs` holds all values to `IList` even if it is single value, this also causes allocations. `ObservableCollection<T>` has no Range feature so a lot of wastage occurs when adding multiple values, because it is a single value notification.  Also, it is not thread-safe is hard to do linkage with the notifier.

ObservableCollections introduces there generics version, `NotifyCollectionChangedEventHandler<T>` and `NotifyCollectionChangedEventArgs<T>`, it using latest C# features(`in`, `readonly ref struct`, `ReadOnlySpan<T>`). Also, Sort and Reverse will now be notified.

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
    public readonly SortOperation<T> SortOperation;
}
```

Also, use the interface `IObservableCollection<T>` instead of `INotifyCollectionChanged`. This is guaranteed to be thread-safe and can produce a View that is fully synchronized with the collection.

```csharp
public interface IObservableCollection<T> : IReadOnlyCollection<T>
{
    event NotifyCollectionChangedEventHandler<T>? CollectionChanged;
    object SyncRoot { get; }
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform);
}
```
 
SynchronizedView helps to separate between Model and View (ViewModel). We will use ObservableCollections as the Model and generate SynchronizedView as the View (ViewModel). This architecture can be applied not only to WPF, but also to Blazor, Unity, etc.

![image](https://user-images.githubusercontent.com/46207/131979264-2463403b-0fba-474b-8f49-277c2abe1b05.png)

The View retains the transformed values. The transform function is called only once during Add, so costly objects that are linked can also be instantiated. Additionally, it has a feature to dynamically show or hide values using filters.

Observable Collections themselves do not implement `INotifyCollectionChanged`, so they cannot be bound on XAML platforms and the like. However, they can be converted to collections that implement `INotifyCollectionChanged` using `ToNotifyCollectionChanged()`, making them suitable for binding.

![image](https://github.com/user-attachments/assets/b5590bb8-16d6-4f9c-be07-1288a6801e68)

ObservableCollections has not just a simple list, there are many more data structures. `ObservableList<T>`, `ObservableDictionary<TKey, TValue>`, `ObservableHashSet<T>`, `ObservableQueue<T>`, `ObservableStack<T>`, `ObservableRingBuffer<T>`, `ObservableFixedSizeRingBuffer<T>`. `RingBuffer`, especially `FixedSizeRingBuffer`, can be achieved with efficient performance when there is rotation (e.g., displaying up to 1000 logs, where old ones are deleted when new ones are added). Of course, the AddRange allows for efficient batch processing of large numbers of additions.

If you want to handle each change event with Rx, you can monitor it with the following method by combining it with [R3](https://github.com/Cysharp/R3):

```csharp
Observable<CollectionAddEvent<T>> IObservableCollection<T>.ObserveAdd()
Observable<CollectionRemoveEvent<T>> IObservableCollection<T>.ObserveRemove()
Observable<CollectionReplaceEvent<T>> IObservableCollection<T>.ObserveReplace() 
Observable<CollectionMoveEvent<T>> IObservableCollection<T>.ObserveMove() 
Observable<CollectionResetEvent<T>> IObservableCollection<T>.ObserveReset()
Observable<CollectionResetEvent<T>> IObservableCollection<T>.ObserveReset()
Observable<Unit> IObservableCollection<T>.ObserveClear<T>()
Observable<(int Index, int Count)> IObservableCollection<T>.ObserveReverse<T>()
Observable<(int Index, int Count, IComparer<T>? Comparer)> IObservableCollection<T>.ObserveSort<T>()
Observable<int> IObservableCollection<T>.ObserveCountChanged<T>()
```

Getting Started
---
For .NET, use NuGet. For Unity, please read [Unity](#unity) section.

> dotnet add package [ObservableCollections](https://www.nuget.org/packages/ObservableCollections)

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

While it is possible to manually handle the `CollectionChanged` event as shown in the example above, you can also create a `SynchronizedView` as a collection that holds a separate synchronized value.

```csharp
var list = new ObservableList<int>();
var view = list.CreateView(x => x.ToString() + "$");

list.Add(10);
list.Add(20);
list.AddRange(new[] { 30, 40, 50 });
list[1] = 60;
list.RemoveAt(3);

foreach (var v in view)
{
    // 10$, 60$, 30$, 50$
    Console.WriteLine(v);
}

// Dispose view is unsubscribe collection changed event.
view.Dispose();
```

The view can modify the objects being enumerated by attaching a Filter.

```csharp
var list = new ObservableList<int>();
using var view = list.CreateView(x => x.ToString() + "$");

list.Add(1);
list.Add(20);
list.AddRange(new[] { 30, 31, 32 });

// attach filter
view.AttachFilter(x => x % 2 == 0);

foreach (var v in view)
{
    // 20$, 30$, 32$
    Console.WriteLine(v);
}

// attach other filter(removed previous filter)
view.AttachFilter(x => x % 2 == 1);

foreach (var v in view)
{
    // 1$, 31$
    Console.WriteLine(v);
}

// Count shows filtered length
Console.WriteLine(view.Count); // 2
```

The View only allows iteration and Count; it cannot be accessed via an indexer. If indexer access is required, you need to convert it using `ToViewList()`. Additionally, `ToNotifyCollectionChanged()` converts it to a synchronized view that implements `INotifyCollectionChanged`, which is necessary for XAML binding, in addition to providing indexer access.

```csharp
// Queue <-> List Synchronization
var queue = new ObservableQueue<int>();

queue.Enqueue(1);
queue.Enqueue(10);
queue.Enqueue(100);
queue.Enqueue(1000);
queue.Enqueue(10000);

using var view = queue.CreateView(x => x.ToString() + "$");

using var viewList = view.ToViewList();

Console.WriteLine(viewList[2]); // 100$
```

In the case of ObservableList, calls to `Sort` and `Reverse` can also be synchronized with the view.

```csharp
var list = new ObservableList<int> { 1, 301, 20, 50001, 4000 };
using var view = list.CreateView(x => x.ToString() + "$");

view.AttachFilter(x => x % 2 == 0);

foreach (var v in view)
{
    // 20$, 4000$
    Console.WriteLine(v);
}

// Reverse operations on the list will affect the view
list.Reverse();

foreach (var v in view)
{
    // 4000$, 20$
    Console.WriteLine(v);
}

// remove filter
view.ResetFilter();

// The reverse operation is also reflected in the values hidden by the filter
foreach (var v in view)
{
    // 4000$, 50001$, 20$, 301$, 1$
    Console.WriteLine(v);
}

// also affect Sort Operations    
list.Sort();
foreach (var v in view)
{
    // 1$, 20$, 301$, 4000$, 50001$
    Console.WriteLine(v);
}

// you can use custom comparer
list.Sort(new DescendantComaprer());
foreach (var v in view)
{
    // 50001$, 4000$, 301$, 20$, 1$
    Console.WriteLine(v);
}

class DescendantComaprer : IComparer<int>
{
    public int Compare(int x, int y)
    {
        return y.CompareTo(x);
    }
}
```

Reactive Extensions with R3
---
Once the R3 extension package is installed, you can subscribe to `ObserveAdd`, `ObserveRemove`, `ObserveReplace`, `ObserveMove`, `ObserveReset`, `ObserveClear`, `ObserveReverse`, `ObserveSort` events as Rx, allowing you to compose events individually.

> dotnet add package [ObservableCollections.R3](https://www.nuget.org/packages/ObservableCollections.R3)

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

Note that `ObserveReset` is used to subscribe to Clear, Reverse, and Sort operations in bulk.

Since it is not supported by dotnet/reactive, please use the Rx library [R3](https://github.com/Cysharp/R3).

Blazor
---
In the case of Blazor, `StateHasChanged` is called and re-enumeration occurs in response to changes in the collection. It's advisable to use the `CollectionStateChanged` event for this purpose.

```csharp
public partial class Index : IDisposable
{
    ObservableList<int> list;
    public ISynchronizedView<int, int> ItemsView { get; set; }
    int count = 0;

    protected override void OnInitialized()
    {
        list = new ObservableList<int>();
        ItemsView = list.CreateView(x => x);

        ItemsView.CollectionStateChanged += action =>
        {
            InvokeAsync(StateHasChanged);
        };
    }

    void OnClick()
    {
        list.Add(count++);
    }

    public void Dispose()
    {
        ItemsView.Dispose();
    }
}

// .razor, iterate view
@page "/"

<button @onclick=OnClick>button</button>

<table>
	@foreach (var item in ItemsView)
	{
		<tr>
			<td>@item</td>
		</tr>
	}
</table>
```

WPF/Avalonia/WinUI (XAML based UI platforms)
---
Because of data binding in WPF, it is important that the collection is Observable. ObservableCollections high-performance `IObservableCollection<T>` cannot be bind to WPF. Call `ToNotifyCollectionChanged()` to convert it to `INotifyCollectionChanged`. Also, although ObservableCollections and Views are thread-safe, the WPF UI does not support change notifications from different threads. To`ToNotifyCollectionChanged(IColllectionEventDispatcher)` allows multi thread changed.

```csharp
// WPF simple sample.

ObservableList<int> list;
public INotifyCollectionChangedSynchronizedViewList<int> ItemsView { get; set; }

public MainWindow()
{
    InitializeComponent();
    this.DataContext = this;

    list = new ObservableList<int>();

    // for ui synchronization safety of viewmodel
    ItemsView = list.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

    // if collection is changed only from ui-thread, can use this overload
    // ItemsView = list.ToNotifyCollectionChanged();
}

protected override void OnClosed(EventArgs e)
{
    ItemsView.Dispose();
}
```

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

`ToNotifyCollectionChanged()` can also be called without going through a View. In this case, it's guaranteed that no filters will be applied, making it faster. If you want to apply filters, please generate a View before calling it. Additionally, `ObservableList` has a variation called `ToNotifyCollectionChangedSlim()`. This option doesn't generate a list for the View and shares the actual data, making it the fastest and most memory-efficient option. However, range operations such as `AddRange`, `InsertRange` and `RemoveRange` are not supported by WPF (or Avalonia), so they will throw runtime exceptions.

Views and ToNotifyCollectionChanged are internally connected by events, so they need to be `Dispose` to release those connections.

Standard Views are readonly. If you want to reflect the results of binding back to the original collection, use `CreateWritableView` to generate an `IWritableSynchronizedView`, and then use `ToWritableNotifyCollectionChanged` to create an `INotifyCollectionChanged` collection from it.

```csharp
public delegate T WritableViewChangedEventHandler<T, TView>(TView newView, T originalValue, ref bool setValue);

public interface IWritableSynchronizedView<T, TView> : ISynchronizedView<T, TView>
{
    INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter);
    INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter, ICollectionEventDispatcher? collectionEventDispatcher);
}
```

`ToWritableNotifyCollectionChanged` accepts a delegate called `WritableViewChangedEventHandler`. `newView` receives the newly bound value. If `setValue` is true, it sets a new value to the original collection, triggering notification propagation. The View is also regenerated. If `T originalValue` is a reference type, you can prevent such propagation by setting `setValue` to `false`.

```csharp
var list = new ObservableList<Person>()
{
    new (){ Age = 10, Name = "John" },
    new (){ Age = 22, Name = "Jeyne" },
    new (){ Age = 30, Name = "Mike" },
};
var view = list.CreateWritableView(x => x.Name);
view.AttachFilter(x => x.Age >= 20);

IList<string?> bindable = view.ToWritableNotifyCollectionChanged((string? newView, Person original, ref bool setValue) =>
{
    if (setValue)
    {
        // default setValue == true is Set operation
        original.Name = newView;

        // You can modify setValue to false, it does not set original collection to new value.
        // For mutable reference types, when there is only a single,
        // bound View and to avoid recreating the View, setting false is effective.
        // Otherwise, keeping it true will set the value in the original collection as well,
        // and change notifications will be sent to lower-level Views(the delegate for View generation will also be called anew).
        setValue = false;
        return original;
    }
    else
    {
        // default setValue == false is Add operation
        return new Person { Age = null, Name = newView };
    }
});

bindable[1] = "Bob"; // change Mike(filtered view's [1]) to Bob.
bindable.Add("Ken");

// Show Views
foreach (var item in view)
{
    Console.WriteLine(item);
}

Console.WriteLine("---");

// Show Originals
foreach (var item in list)
{
    Console.WriteLine((item.Age, item.Name));
}

public class Person
{
    public int? Age { get; set; }
    public string? Name { get; set; }
}
```

Unity
---
In Unity projects, you can installing `ObservableCollections` with [NugetForUnity](https://github.com/GlitchEnzo/NuGetForUnity). If R3 integration is required, similarly install `ObservableCollections.R3` via NuGetForUnity.

In Unity, ObservableCollections and Views are useful as CollectionManagers, since they need to convert T to Prefab for display. Since View objects are generated only once, it's possible to complement GameObjects tied to the collection.

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

            // add to root
            item.transform.SetParent(root.transform);

            return item.gameObject;
        });
        view.ViewChanged += View_ViewChanged;
    }

    void View_ViewChanged(in SynchronizedViewChangedEventArgs<int, string> eventArgs)
    {
        // hook remove event
        if (NotifyCollectionChangedAction.Remove)
        {
            GameObject.Destroy(eventArgs.OldItem.View);
        }

        // hook for Filter attached, clear, etc...
        // if (NotifyCollectionChangedAction.Reset) { }
    }

    void OnDestroy()
    {
        view.Dispose();
    }
}
```

Reference
---
ObservableCollections provides these collections.

```csharp
class ObservableList<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>, IReadOnlyObservableList<T>
class ObservableDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IObservableCollection<KeyValuePair<TKey, TValue>> where TKey : notnull
class ObservableHashSet<T> : IReadOnlySet<T>, IReadOnlyCollection<T>, IObservableCollection<T> where T : notnull
class ObservableQueue<T> : IReadOnlyCollection<T>, IObservableCollection<T>
class ObservableStack<T> : IReadOnlyCollection<T>, IObservableCollection<T>
class ObservableRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
class RingBuffer<T> : IList<T>, IReadOnlyList<T>
class ObservableFixedSizeRingBuffer<T> : IList<T>, IReadOnlyList<T>, IObservableCollection<T>
class AlternateIndexList<T> : IEnumerable<T>
```

The `IObservableCollection<T>` is the base interface for all, containing the `CollectionChanged` event and the `CreateView` method.

```csharp
public delegate void NotifyCollectionChangedEventHandler<T>(in NotifyCollectionChangedEventArgs<T> e);

public interface IObservableCollection<T> : IReadOnlyCollection<T>
{
    object SyncRoot { get; }
    event NotifyCollectionChangedEventHandler<T>? CollectionChanged;
    ISynchronizedView<T, TView> CreateView<TView>(Func<T, TView> transform);
}
```

The notification event `NotifyCollectionChangedEventArgs<T>` has the following definition:

```csharp
/// <summary>
/// Contract:
///     IsSingleItem ? (NewItem, OldItem) : (NewItems, OldItems)
///     Action.Add
///         NewItem, NewItems, NewStartingIndex
///     Action.Remove
///         OldItem, OldItems, OldStartingIndex
///     Action.Replace
///         NewItem, NewItems, OldItem, OldItems, (NewStartingIndex, OldStartingIndex = samevalue)
///     Action.Move
///         NewStartingIndex, OldStartingIndex
///     Action.Reset
///         SortOperation(IsClear, IsReverse, IsSort)
/// </summary>
[StructLayout(LayoutKind.Auto)]
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
    public readonly SortOperation<T> SortOperation;
}
```

This is the interface for View:

```csharp
public delegate void NotifyViewChangedEventHandler<T, TView>(in SynchronizedViewChangedEventArgs<T, TView> e);

public enum RejectedViewChangedAction
{
    Add, Remove, Move
}

public interface ISynchronizedView<T, TView> : IReadOnlyCollection<TView>, IDisposable
{
    object SyncRoot { get; }
    ISynchronizedViewFilter<T> Filter { get; }
    IEnumerable<(T Value, TView View)> Filtered { get; }
    IEnumerable<(T Value, TView View)> Unfiltered { get; }
    int UnfilteredCount { get; }

    event NotifyViewChangedEventHandler<T, TView>? ViewChanged;
    event Action<RejectedViewChangedAction, int, int>? RejectedViewChanged; // int index, int oldIndex(when RejectedViewChangedAction is Move)
    event Action<NotifyCollectionChangedAction>? CollectionStateChanged;

    void AttachFilter(ISynchronizedViewFilter<T> filter);
    void ResetFilter();
    ISynchronizedViewList<TView> ToViewList();
    INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged();
    INotifyCollectionChangedSynchronizedViewList<TView> ToNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher);
}
```

The `Count` of the View returns the filtered value, but if you need the unfiltered value, use `UnfilteredCount`. Also, normal enumeration returns only `TView`, but if you need `T` or want to enumerate pre-filtered values, you can get them with `Filtered` and `Unfiltered`.

The View's notification event `SynchronizedViewChangedEventArgs<T>` has the following definition:

```csharp
public readonly ref struct SynchronizedViewChangedEventArgs<T, TView>
{
    public readonly NotifyCollectionChangedAction Action;
    public readonly bool IsSingleItem;
    public readonly (T Value, TView View) NewItem;
    public readonly (T Value, TView View) OldItem;
    public readonly ReadOnlySpan<T> NewValues;
    public readonly ReadOnlySpan<TView> NewViews;
    public readonly ReadOnlySpan<T> OldValues;
    public readonly ReadOnlySpan<TView> OldViews;
    public readonly int NewStartingIndex;
    public readonly int OldStartingIndex;
    public readonly SortOperation<T> SortOperation;
}
```

When `NotifyCollectionChangedAction` is `Reset`, additional determination can be made with `SortOperation<T>`.

```csharp
public readonly struct SortOperation<T>
{
    public readonly int Index;
    public readonly int Count;
    public readonly IComparer<T>? Comparer;

    public bool IsReverse { get; }
    public bool IsClear { get; }
    public bool IsSort { get; }
}
```

When `IsReverse` is true, you need to use `Index` and `Count`. When `IsSort` is true, you need to use `Index`, `Count`, and `Comparer` values.

For Filter, you can either create one that implements this interface or generate one from a lambda expression using extension methods.

```csharp
public interface ISynchronizedViewFilter<T>
{
    bool IsMatch(T value);
}

public static class SynchronizedViewExtensions
{
    public static void AttachFilter<T, TView>(this ISynchronizedView<T, TView> source, Func<T, bool> filter)
    {
    }
}
```

`ObservableList<T>` has writable view.

```csharp
public sealed partial class ObservableList<T>
{
    public IWritableSynchronizedView<T, TView> CreateWritableView<TView>(Func<T, TView> transform);

    public INotifyCollectionChangedSynchronizedViewList<T> ToWritableNotifyCollectionChanged();
    public INotifyCollectionChangedSynchronizedViewList<T> ToWritableNotifyCollectionChanged(ICollectionEventDispatcher? collectionEventDispatcher);
    public INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged<TView>(Func<T, TView> transform, WritableViewChangedEventHandler<T, TView>? converter);
    public INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged<TView>(Func<T, TView> transform, ICollectionEventDispatcher? collectionEventDispatcher, WritableViewChangedEventHandler<T, TView>? converter);
}

public delegate T WritableViewChangedEventHandler<T, TView>(TView newView, T originalValue, ref bool setValue);

public interface IWritableSynchronizedView<T, TView> : ISynchronizedView<T, TView>
{
    (T Value, TView View) GetAt(int index);
    void SetViewAt(int index, TView view);
    void SetToSourceCollection(int index, T value);
    IWritableSynchronizedViewList<TView> ToWritableViewList(WritableViewChangedEventHandler<T, TView> converter);
    INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter);
    INotifyCollectionChangedSynchronizedViewList<TView> ToWritableNotifyCollectionChanged(WritableViewChangedEventHandler<T, TView> converter, ICollectionEventDispatcher? collectionEventDispatcher);
}

public interface IWritableSynchronizedViewList<TView> : ISynchronizedViewList<TView>
{
    new TView this[int index] { get; set; }
}
```

Here are definitions for other collections:

```csharp
public interface IReadOnlyObservableList<T> :
    IReadOnlyList<T>, IObservableCollection<T>
{
}

public interface IReadOnlyObservableDictionary<TKey, TValue> :
    IReadOnlyDictionary<TKey, TValue>, IObservableCollection<KeyValuePair<TKey, TValue>>
{
}

public interface ISynchronizedViewList<out TView> : IReadOnlyList<TView>, IDisposable
{
}

public interface INotifyCollectionChangedSynchronizedViewList<out TView> : ISynchronizedViewList<TView>, INotifyCollectionChanged, INotifyPropertyChanged
{
}
```

License
---
This library is licensed under the MIT License.
