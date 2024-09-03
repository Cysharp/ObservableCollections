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
        public ReactiveCommand<Unit> ClearCommand { get; } = new ReactiveCommand<Unit>();

        public ViewModel()
        {
            observableList.Add(1);
            observableList.Add(2);

            //ItemsView = observableList.CreateView(x => x).ToNotifyCollectionChanged(SynchronizationContextCollectionEventDispatcher.Current);

            ItemsView = observableList.CreateView(x => x).ToNotifyCollectionChanged();

            //var test = ItemsView.ToArray();
            //INotifyCollectionChangedSynchronizedView<int>
            // ItemsView = observableList.CreateView(x => x).ToNotifyCollectionChanged();

            // BindingOperations.EnableCollectionSynchronization(ItemsView, new object());

            AddCommand.Subscribe(_ =>
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    observableList.Add(Random.Shared.Next());
                });
            });

            // var iii = 10;
            ClearCommand.Subscribe(_ =>
            {
                // observableList.Add(iii++);
                observableList.Clear();
            });
        }
    }
}