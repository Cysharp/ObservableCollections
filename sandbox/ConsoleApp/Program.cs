using ObservableCollections;
using System;

namespace ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            var oc = new ObservableList<int>();

            oc.AddRange(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }.AsEnumerable());

        }
    }
}
