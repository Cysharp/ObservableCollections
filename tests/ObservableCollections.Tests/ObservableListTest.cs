using FluentAssertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace ObservableCollections.Tests
{
    public class ObservableListTest
    {
        [Fact]
        public void View()
        {
            var reference = new ObservableCollection<int>();
            var list = new ObservableList<int>();
            var view = list.CreateView(x => new ViewContainer<int>(x));

            list.Add(10); reference.Add(10); // 0
            list.Add(50); reference.Add(50); // 1
            list.Add(30); reference.Add(30); // 2
            list.Add(20); reference.Add(20); // 3
            list.Add(40); reference.Add(40); // 4

            void Equal(params int[] expected)
            {
                reference.Should().Equal(expected);
                list.Should().Equal(expected);
                view.Select(x => x.Value).Should().Equal(expected);
                view.Select(x => x.View).Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            void Equal2(params int[] expected)
            {
                list.Should().Equal(expected);
                view.Select(x => x.Value).Should().Equal(expected);
                view.Select(x => x.View).Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            Equal(10, 50, 30, 20, 40);

            reference.Move(3, 1);
            list.Move(3, 1);
            Equal(10, 20, 50, 30, 40);

            reference.Insert(2, 99);
            list.Insert(2, 99);
            Equal(10, 20, 99, 50, 30, 40);

            reference.RemoveAt(2);
            list.RemoveAt(2);
            Equal(10, 20, 50, 30, 40);

            reference[3] = 88;
            list[3] = 88;
            Equal(10, 20, 50, 88, 40);

            reference.Clear();
            list.Clear();
            Equal(new int[0]);

            list.AddRange(new[] { 100, 200, 300 });
            Equal2(100, 200, 300);

            list.InsertRange(1, new[] { 400, 500, 600 });
            Equal2(100, 400, 500, 600, 200, 300);

            list.RemoveRange(2, 2);
            Equal2(100, 400, 200, 300);
        }

        [Fact]
        public void ViewSorted()
        {
            var list = new ObservableList<int>();
            var view1 = list.CreateSortedView(x => x, x => new ViewContainer<int>(x), comparer: Comparer<int>.Default);
            var view2 = list.CreateSortedView(x => x, x => new ViewContainer<int>(x), viewComparer: Comparer<ViewContainer<int>>.Default);
            var view3 = list.CreateSortedView(x => x, x => new ViewContainer<int>(x), x => x, ascending: true);
            var view4 = list.CreateSortedView(x => x, x => new ViewContainer<int>(x), x => x, ascending: false);

            list.Add(10); // 0
            list.Add(50); // 1
            list.Add(30); // 2
            list.Add(20); // 3
            list.Add(40); // 4

            void Equal(params int[] expected)
            {
                list.Should().Equal(expected);

                var sorted = expected.OrderBy(x => x).ToArray();
                view1.Select(x => x.Value).Should().Equal(sorted);
                view2.Select(x => x.View).Should().Equal(sorted.Select(x => new ViewContainer<int>(x)));
                view3.Select(x => x.Value).Should().Equal(sorted);
                view4.Select(x => x.Value).Should().Equal(expected.OrderByDescending(x => x).ToArray());
            }

            Equal(10, 50, 30, 20, 40);

            list.Move(3, 1);
            Equal(10, 20, 50, 30, 40);

            list.Insert(2, 99);
            Equal(10, 20, 99, 50, 30, 40);

            list.RemoveAt(2);
            Equal(10, 20, 50, 30, 40);

            list[3] = 88;
            Equal(10, 20, 50, 88, 40);

            list.Clear();
            Equal(new int[0]);

            list.AddRange(new[] { 100, 200, 300 });
            Equal(100, 200, 300);

            list.InsertRange(1, new[] { 400, 500, 600 });
            Equal(100, 400, 500, 600, 200, 300);

            list.RemoveRange(2, 2);
            Equal(100, 400, 200, 300);
        }

        [Fact]
        public void Freezed()
        {
            var list = new FreezedList<int>(new[] { 10, 20, 50, 30, 40, 60 });

            var view = list.CreateSortableView(x => new ViewContainer<int>(x));

            view.Sort(x => x, true);
            view.Select(x => x.Value).Should().Equal(10, 20, 30, 40, 50, 60);
            view.Select(x => x.View).Should().Equal(10, 20, 30, 40, 50, 60);

            view.Sort(x => x, false);
            view.Select(x => x.Value).Should().Equal(60, 50, 40, 30, 20, 10);
            view.Select(x => x.View).Should().Equal(60, 50, 40, 30, 20, 10);
        }

        [Fact]
        public void FilterTest()
        {
            var list = new ObservableList<int>();
            var view1 = list.CreateView(x => new ViewContainer<int>(x));
            var view2 = list.CreateSortedView(x => x, x => new ViewContainer<int>(x), comparer: Comparer<int>.Default);
            var view3 = list.CreateSortedView(x => x, x => new ViewContainer<int>(x), viewComparer: Comparer<ViewContainer<int>>.Default);
            list.AddRange(new[] { 10, 21, 30, 44, 45, 66, 90 });

            var filter1 = new TestFilter<int>((x, v) => x % 2 == 0);
            var filter2 = new TestFilter<int>((x, v) => x % 2 == 0);
            var filter3 = new TestFilter<int>((x, v) => x % 2 == 0);
            view1.AttachFilter(filter1);
            view2.AttachFilter(filter2);
            view3.AttachFilter(filter3);

            filter1.CalledWhenTrue.Select(x => x.Item1).Should().Equal(10, 30, 44, 66, 90);
            filter2.CalledWhenTrue.Select(x => x.Item1).Should().Equal(10, 30, 44, 66, 90);
            filter3.CalledWhenTrue.Select(x => x.Item1).Should().Equal(10, 30, 44, 66, 90);

            filter1.CalledWhenFalse.Select(x => x.Item1).Should().Equal(21, 45);
            filter2.CalledWhenFalse.Select(x => x.Item1).Should().Equal(21, 45);
            filter3.CalledWhenFalse.Select(x => x.Item1).Should().Equal(21, 45);

            view1.Select(x => x.Value).Should().Equal(10, 30, 44, 66, 90);
            view2.Select(x => x.Value).Should().Equal(10, 30, 44, 66, 90);
            view3.Select(x => x.Value).Should().Equal(10, 30, 44, 66, 90);

            filter1.Clear();
            filter2.Clear();
            filter3.Clear();

            list.Add(100);
            list.AddRange(new[] { 101 });
            filter1.CalledWhenTrue.Select(x => x.Item1).Should().Equal(100);
            filter2.CalledWhenTrue.Select(x => x.Item1).Should().Equal(100);
            filter3.CalledWhenTrue.Select(x => x.Item1).Should().Equal(100);
            filter1.CalledWhenFalse.Select(x => x.Item1).Should().Equal(101);
            filter2.CalledWhenFalse.Select(x => x.Item1).Should().Equal(101);
            filter3.CalledWhenFalse.Select(x => x.Item1).Should().Equal(101);

            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 100), (ChangedKind.Add, 101));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 100), (ChangedKind.Add, 101));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 100), (ChangedKind.Add, 101));

            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            list.Insert(0, 1000);
            list.InsertRange(0, new[] { 999 });

            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 1000), (ChangedKind.Add, 999));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 1000), (ChangedKind.Add, 999));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 1000), (ChangedKind.Add, 999));
            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            list.RemoveAt(0);
            list.RemoveRange(0, 1);

            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 999), (ChangedKind.Remove, 1000));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 999), (ChangedKind.Remove, 1000));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 999), (ChangedKind.Remove, 1000));
            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            list[0] = 9999;

            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 10), (ChangedKind.Add, 9999));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 10), (ChangedKind.Add, 9999));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 10), (ChangedKind.Add, 9999));
            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            list.Move(3, 0);
            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Move, 44));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Move, 44));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Move, 44));
            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            list.Clear();
            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 44), (ChangedKind.Remove, 9999), (ChangedKind.Remove, 21), (ChangedKind.Remove, 30), (ChangedKind.Remove, 45), (ChangedKind.Remove, 66), (ChangedKind.Remove, 90), (ChangedKind.Remove, 100), (ChangedKind.Remove, 101));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 21), (ChangedKind.Remove, 30), (ChangedKind.Remove, 44), (ChangedKind.Remove, 45), (ChangedKind.Remove, 66), (ChangedKind.Remove, 90), (ChangedKind.Remove, 100), (ChangedKind.Remove, 101), (ChangedKind.Remove, 9999));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 21), (ChangedKind.Remove, 30), (ChangedKind.Remove, 44), (ChangedKind.Remove, 45), (ChangedKind.Remove, 66), (ChangedKind.Remove, 90), (ChangedKind.Remove, 100), (ChangedKind.Remove, 101), (ChangedKind.Remove, 9999));
        }

        [Fact]
        public void FilterAndInvokeAddEvent()
        {
            var list = new ObservableList<int>();
            var view1 = list.CreateView(x => new ViewContainer<int>(x));
            list.AddRange(new[] { 10, 21, 30, 44 });

            var filter1 = new TestFilter<int>((x, v) => x % 2 == 0);
            view1.AttachFilter(filter1, true);
            
            filter1.CalledOnCollectionChanged[0].changedKind.Should().Be(ChangedKind.Add);
            filter1.CalledOnCollectionChanged[0].value.Should().Be(10);
            filter1.CalledOnCollectionChanged[1].changedKind.Should().Be(ChangedKind.Add);
            filter1.CalledOnCollectionChanged[1].value.Should().Be(21);
            filter1.CalledOnCollectionChanged[2].changedKind.Should().Be(ChangedKind.Add);
            filter1.CalledOnCollectionChanged[2].value.Should().Be(30);
            filter1.CalledOnCollectionChanged[3].changedKind.Should().Be(ChangedKind.Add);
            filter1.CalledOnCollectionChanged[3].value.Should().Be(44);

            filter1.CalledWhenTrue.Count.Should().Be(3);
            filter1.CalledWhenFalse.Count.Should().Be(1);
        }   
    }
}