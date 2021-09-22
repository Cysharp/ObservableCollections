using ObservableCollections;
using System;
using System.Collections.Specialized;


// Basic sample, use like ObservableCollection<T>.
// CollectionChanged observes all collection modification
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