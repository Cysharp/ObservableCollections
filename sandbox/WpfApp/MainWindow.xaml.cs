using ObservableCollections;
using R3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //ObservableList<int> list;
        //public INotifyCollectionChangedSynchronizedView<int> ItemsView { get; set; }




        public MainWindow()
        {
            InitializeComponent();


            R3.WpfProviderInitializer.SetDefaultObservableSystem(x =>
            {
                Trace.WriteLine(x);
            });

            this.DataContext = new ViewModel();

            // Dispatcher.BeginInvoke(


            //list = new ObservableList<int>();
            //list.AddRange(new[] { 1, 10, 188 });
            //ItemsView = list.CreateSortedView(x => x, x => x, comparer: Comparer<int>.Default).ToNotifyCollectionChanged();


            //BindingOperations.EnableCollectionSynchronization(ItemsView, new object());
        }

        //int adder = 99;

        //private void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    ThreadPool.QueueUserWorkItem(_ =>
        //    {
        //        list.Add(adder++);
        //    });
        //}

        //protected override void OnClosed(EventArgs e)
        //{
        //    ItemsView.Dispose();
        //}
    }

    public class ViewModel
    {
        private ObservableList<int> observableList { get; } = new ObservableList<int>();
        public INotifyCollectionChangedSynchronizedViewList<int> ItemsView { get; }
        public ReactiveCommand<Unit> AddCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> AddRangeCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> InsertAtRandomCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> RemoveAtRandomCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> RemoveRangeCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ClearCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ReverseCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> SortCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> AttachFilterCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ResetFilterCommand { get; } = new ReactiveCommand<Unit>();

        private ObservableList<Person> SourceList { get; } = [];
        private IWritableSynchronizedView<Person, Person> writableFilter;
        private ISynchronizedView<Person, Person> notWritableFilter;
        public NotifyCollectionChangedSynchronizedViewList<Person> NotWritableNonFilterView { get; }
        public NotifyCollectionChangedSynchronizedViewList<Person> NotWritableFilterView { get; }
        public NotifyCollectionChangedSynchronizedViewList<Person> WritableNonFilterPersonView { get; }
        public NotifyCollectionChangedSynchronizedViewList<Person> WritableFilterPersonView { get; }
        public ReactiveCommand<Unit> AttachFilterCommand2 { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ResetFilterCommand2 { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> AttachFilterCommand3 { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ResetFilterCommand3 { get; } = new ReactiveCommand<Unit>();

        public ViewModel()
        {
            observableList.Add(1);
            observableList.Add(2);

            var view = observableList.CreateView(x => x);
            //ItemsView = view.ToNotifyCollectionChanged();
            ItemsView = observableList.ToNotifyCollectionChanged();

            // check for optimize list
            // ItemsView = observableList.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);


            AddCommand.Subscribe(_ =>
            {
                // ThreadPool.QueueUserWorkItem(_ =>
                {
                    observableList.Add(Random.Shared.Next());
                }
            });

            AddRangeCommand.Subscribe(_ =>
            {
                var xs = Enumerable.Range(1, 5).Select(_ => Random.Shared.Next()).ToArray();
                observableList.AddRange(xs);
            });

            InsertAtRandomCommand.Subscribe(_ =>
            {
                var from = Random.Shared.Next(0, view.Count);
                observableList.Insert(from, Random.Shared.Next());
            });

            RemoveAtRandomCommand.Subscribe(_ =>
            {
                var from = Random.Shared.Next(0, view.Count);
                observableList.RemoveAt(from);
            });

            RemoveRangeCommand.Subscribe(_ =>
            {
                observableList.RemoveRange(2, 5);
            });

            ClearCommand.Subscribe(_ =>
            {
                observableList.Clear();
            });


            ReverseCommand.Subscribe(_ =>
            {
                observableList.Reverse();
            });

            SortCommand.Subscribe(_ =>
            {
                observableList.Sort();
            });

            AttachFilterCommand.Subscribe(_ =>
            {
                view.AttachFilter(x => x % 2 == 0);
            });

            ResetFilterCommand.Subscribe(_ =>
            {
                view.ResetFilter();
            });

            SourceList.Add(new() { Name = "a", Age = 1 });
            SourceList.Add(new() { Name = "b", Age = 2 });
            SourceList.Add(new() { Name = "c", Age = 3 });
            SourceList.Add(new() { Name = "d", Age = 4 });
            //NotWritable, NonFilter
            NotWritableNonFilterView = SourceList.ToNotifyCollectionChanged();

            //NotWritable, Filter
            notWritableFilter = SourceList.CreateView(x => x);
            NotWritableFilterView = notWritableFilter.ToNotifyCollectionChanged();

            //Writable, NonFilter
            WritableNonFilterPersonView = SourceList.ToWritableNotifyCollectionChanged();

            //WritableNonFilterPersonView = SourceList.ToWritableNotifyCollectionChanged(x => x, (Person newView, Person original, ref bool setValue) =>
            //{
            //    if (setValue)
            //    {
            //        // default setValue == true is Set operation
            //        original.Name = newView.Name;
            //        original.Age = newView.Age;

            //        // You can modify setValue to false, it does not set original collection to new value.
            //        // For mutable reference types, when there is only a single,
            //        // bound View and to avoid recreating the View, setting false is effective.
            //        // Otherwise, keeping it true will set the value in the original collection as well,
            //        // and change notifications will be sent to lower-level Views(the delegate for View generation will also be called anew).
            //        setValue = false;
            //        return original;
            //    }
            //    else
            //    {
            //        // default setValue == false is Add operation
            //        return new Person { Age = newView.Age, Name = newView.Name };
            //    }
            //}, null);

            //Writable, Filter
            writableFilter = SourceList.CreateWritableView(x => x);
            WritableFilterPersonView = writableFilter.ToWritableNotifyCollectionChanged();

            //WritableFilterPersonView = writableFilter.ToWritableNotifyCollectionChanged((Person newView, Person original, ref bool setValue) =>
            //{
            //    if (setValue)
            //    {
            //        // default setValue == true is Set operation
            //        original.Name = newView.Name;
            //        original.Age = newView.Age;

            //        // You can modify setValue to false, it does not set original collection to new value.
            //        // For mutable reference types, when there is only a single,
            //        // bound View and to avoid recreating the View, setting false is effective.
            //        // Otherwise, keeping it true will set the value in the original collection as well,
            //        // and change notifications will be sent to lower-level Views(the delegate for View generation will also be called anew).
            //        setValue = false;
            //        return original;
            //    }
            //    else
            //    {
            //        // default setValue == false is Add operation
            //        return new Person { Age = newView.Age, Name = newView.Name };
            //    }
            //});

            AttachFilterCommand2.Subscribe(_ =>
            {
                notWritableFilter.AttachFilter(x => x.Age % 2 == 0);
            });

            ResetFilterCommand2.Subscribe(_ =>
            {
                notWritableFilter.ResetFilter();
            });

            AttachFilterCommand3.Subscribe(_ =>
            {
                writableFilter.AttachFilter(x => x.Age % 2 == 0);
            });

            ResetFilterCommand3.Subscribe(_ =>
            {
                writableFilter.ResetFilter();
            });
        }
    }
    public class Person
    {
        public int? Age { get; set; }
        public string? Name { get; set; }
    }

    public class WpfDispatcherCollection(Dispatcher dispatcher) : ICollectionEventDispatcher
    {
        public void Post(CollectionEventDispatcherEventArgs ev)
        {
            dispatcher.InvokeAsync(() =>
            {
                ev.Invoke();
            });
        }
    }
}