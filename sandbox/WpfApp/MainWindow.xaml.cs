using ObservableCollections;
using System;
using System.Collections.Generic;
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

namespace WpfApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableList<int> list;
        public ISynchronizedView<int, int> ItemsView { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;




            list = new ObservableList<int>();
            list.AddRange(new[] { 1, 10, 188 });
            ItemsView = list.CreateSortedView(x => x, x => x, comparer: Comparer<int>.Default).WithINotifyCollectionChanged();


            BindingOperations.EnableCollectionSynchronization(ItemsView, new object());
        }

        int adder = 99;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                list.Add(adder++);
            });
        }
    }
}
