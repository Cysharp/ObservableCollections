using System;
using System.Collections.Specialized;
using R3;
using System.Linq;
using ObservableCollections;
using System.Collections;
using System.Collections.Generic;


var dict = new ObservableDictionary<int, string>();
var view = dict.CreateView(x => x).ToNotifyCollectionChanged();
dict.Add(key: 1, value: "foo");
dict.Add(key: 2, value: "bar");

foreach (var item in view)
{
    Console.WriteLine(item);
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