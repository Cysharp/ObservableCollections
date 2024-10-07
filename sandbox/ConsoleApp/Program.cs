using System;
using System.Collections.Specialized;
using R3;
using System.Linq;
using ObservableCollections;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks.Sources;
using System.Reflection.Emit;

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

// bindable[0] = "takoyaki";

foreach (var item in view)
{
    Console.WriteLine(item);
}

Console.WriteLine("---");

foreach (var item in list)
{
    Console.WriteLine((item.Age, item.Name));
}

public class Person
{
    public int? Age { get; set; }
    public string? Name { get; set; }
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