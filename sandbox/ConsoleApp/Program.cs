using System;
using System.Linq;
using ObservableCollections;
  
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
    public string Value { get; set; }
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
        ChangedKind changedKind, 
        int value, 
        ViewModel view,
        in NotifyCollectionChangedEventArgs<int> eventArgs)
    {
        switch (changedKind)
        {
            case ChangedKind.Add:
                view.Value += " Add";
                break;
            case ChangedKind.Remove:
                view.Value += " Remove";
                break;
            case ChangedKind.Move:
                view.Value += $" Move {eventArgs.OldStartingIndex} {eventArgs.NewStartingIndex}";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(changedKind), changedKind, null);
        }
    }
}