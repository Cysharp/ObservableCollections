using System;
using System.Collections.Specialized;
using R3;
using System.Linq;
using ObservableCollections;
using System.Collections;
using System.Collections.Generic;



var buffer = new ObservableList<int>(5);

var view = buffer.CreateView(value => value);
view.AttachFilter(value => value % 2 == 1); // when filtered, mismatch...!

//{
// INotifyCollectionChangedSynchronizedViewList created from ISynchronizedView with a filter.
var collection = view.ToNotifyCollectionChanged();

// Not disposed here.
//}

buffer.Insert(0, 1);
buffer.Insert(0, 1);
buffer.Insert(0, 2);
buffer.Insert(0, 3);
buffer.Insert(0, 5);
buffer.RemoveAt(buffer.Count - 1);

buffer.Insert(0, 8);
buffer.Insert(0, 13);


buffer.Move(1, 5);

foreach (var item in view)
{
    Console.WriteLine(item);
}
Console.WriteLine("---");

foreach (var item in collection)
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