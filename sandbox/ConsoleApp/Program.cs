using System;
using System.Collections.Specialized;
using R3;
using System.Linq;
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








var models = new ObservableList<int>(Enumerable.Range(0, 10));

var viewModels = models.CreateView(x => new ViewModel
{
    Id = x,
    Value = "@" + x
});

viewModels.AttachFilter(new HogeFilter(), true);

models.Add(100);

foreach (var (x, xs) in viewModels)
{
    System.Console.WriteLine(xs.Value);
}

class ViewModel
{
    public int Id { get; set; }
    public string Value { get; set; } = default!;
}

class HogeFilter : ISynchronizedViewFilter<int, ViewModel>
{
    public bool IsMatch(int value, ViewModel view)
    {
        return value % 2 == 0;
    }

    public void WhenTrue(int value, ViewModel view)
    {
        view.Value = $"@{value} (even)";
    }

    public void WhenFalse(int value, ViewModel view)
    {
        view.Value = $"@{value} (odd)";
    }

    public void OnCollectionChanged(
        NotifyCollectionChangedAction changedAction,
        int value,
        ViewModel view,
        in NotifyCollectionChangedEventArgs<int> eventArgs)
    {
        switch (changedAction)
        {
            case NotifyCollectionChangedAction.Add:
                view.Value += " Add";
                break;
            case NotifyCollectionChangedAction.Remove:
                view.Value += " Remove";
                break;
            case NotifyCollectionChangedAction.Move:
                view.Value += $" Move {eventArgs.OldStartingIndex} {eventArgs.NewStartingIndex}";
                break;
            case NotifyCollectionChangedAction.Replace:
                view.Value += $" Replace {eventArgs.OldStartingIndex} {eventArgs.NewStartingIndex}";
                break;
            case NotifyCollectionChangedAction.Reset:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(changedAction), changedAction, null);
        }
    }
}