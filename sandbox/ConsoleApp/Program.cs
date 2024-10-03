using System;
using System.Collections.Specialized;
using R3;
using System.Linq;
using ObservableCollections;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks.Sources;

var l = new ObservableList<int>();
var view = l.CreateWritableView(x => x.ToString());
view.AttachFilter(x => x % 2 == 0);
IList<string> notify = view.ToWritableNotifyCollectionChanged((string newView, int originalValue, ref bool setValue) =>
{
    setValue = false;
    return int.Parse(newView);
});

l.Add(0);
l.Add(1);
l.Add(2);
l.Add(3);
l.Add(4);
l.Add(5);

notify[1] = "99999";

foreach (var item in view)
{
    Console.WriteLine(item);
}


//var buffer = new ObservableFixedSizeRingBuffer<int>(5);

//var view = buffer.CreateView(value => value);
//view.AttachFilter(value => value % 2 == 1); // when filtered, mismatch...!

////{
//// INotifyCollectionChangedSynchronizedViewList created from ISynchronizedView with a filter.
//var collection = view.ToNotifyCollectionChanged();

//// Not disposed here.
////}

//buffer.AddFirst(1);
//buffer.AddFirst(1);
//buffer.AddFirst(2);
//buffer.AddFirst(3);
//buffer.AddFirst(5);
//buffer.AddFirst(8); // Argument out of range
//buffer.AddFirst(13);

//foreach (var item in collection)
//{
//    Console.WriteLine(item);
//}

//Console.WriteLine("---");

//foreach (var item in view)
//{
//    Console.WriteLine(item);
//}

//Console.WriteLine("---");

//foreach (var item in buffer)
//{
//    Console.WriteLine(item);
//}