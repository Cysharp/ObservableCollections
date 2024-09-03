using System;
using System.Collections.Specialized;
using R3;
using System.Linq;
using ObservableCollections;
using System.Collections;
using System.Collections.Generic;




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


view.ViewChanged += View_ViewChanged;

void View_ViewChanged(in SynchronizedViewChangedEventArgs<int, string> eventArgs)
{
    if (eventArgs.Action == NotifyCollectionChangedAction.Add)
    {
     // eventArgs.OldItem.View.   
    }

    throw new NotImplementedException();
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