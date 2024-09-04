using Avalonia.Controls;
using ObservableCollections;
using R3;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;

namespace AvaloniaApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = new ViewModel();

        }
    }

    public class ViewModel
    {
        private ObservableList<int> observableList { get; } = new ObservableList<int>();
        public INotifyCollectionChangedSynchronizedViewList<int> ItemsView { get; }
        public ReactiveCommand<Unit> AddCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> AddRangeCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> InsertAtRandomCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> RemoveAtRandomCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ClearCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ReverseCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> SortCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> AttachFilterCommand { get; } = new ReactiveCommand<Unit>();
        public ReactiveCommand<Unit> ResetFilterCommand { get; } = new ReactiveCommand<Unit>();

        public ViewModel()
        {
            observableList.Add(1);
            observableList.Add(2);

            var view = observableList.CreateView(x => x);
            ItemsView = view.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);


            // check for optimize list
            // ItemsView = observableList.ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);


            AddCommand.Subscribe(_ =>
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    observableList.Add(Random.Shared.Next());
                });
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
        }
    }
}