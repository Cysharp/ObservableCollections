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

viewModels.AttachFilter(new HogeFilter());

models.Add(100);

foreach (var x in viewModels)
{
    System.Console.WriteLine(x);
}

class ViewModel
{
    public int Id { get; set; }
    public string Value { get; set; } = default!;
}

class HogeFilter : ISynchronizedViewFilter<int>
{
    public bool IsMatch(int value)
    {
        return value % 2 == 0;
    }

    public void OnCollectionChanged(in SynchronizedViewChangedEventArgs<int, ViewModel> eventArgs)
    {
        switch (eventArgs.Action)
        {
            case NotifyCollectionChangedAction.Add:
                eventArgs.NewItem.View.Value += " Add";
                break;
            case NotifyCollectionChangedAction.Remove:
                eventArgs.OldItem.View.Value += " Remove";
                break;
            case NotifyCollectionChangedAction.Move:
                eventArgs.NewItem.View.Value += $" Move {eventArgs.OldStartingIndex} {eventArgs.NewStartingIndex}";
                break;
            case NotifyCollectionChangedAction.Replace:
                eventArgs.NewItem.View.Value += $" Replace {eventArgs.NewStartingIndex}";
                break;
            case NotifyCollectionChangedAction.Reset:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(eventArgs.Action), eventArgs.Action, null);
        }
    }
}