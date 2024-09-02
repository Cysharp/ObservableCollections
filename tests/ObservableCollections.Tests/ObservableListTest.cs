using FluentAssertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
                view.Filtered.Select(x => x.View).Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            void Equal2(params int[] expected)
            {
                list.Should().Equal(expected);
                view.Select(x => x.Value).Should().Equal(expected);
                view.Filtered.Select(x => x.View).Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
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

        //[Fact]
        //public void FilterTest()
        //{
        //    var list = new ObservableList<int>();
        //    var view1 = list.CreateView(x => new ViewContainer<int>(x));
        //    list.AddRange(new[] { 10, 21, 30, 44, 45, 66, 90 });

        //    var filter1 = new TestFilter<int>((x, v) => x % 2 == 0);
        //    var filter2 = new TestFilter<int>((x, v) => x % 2 == 0);
        //    var filter3 = new TestFilter<int>((x, v) => x % 2 == 0);
        //    view1.AttachFilter(filter1);
        //    view2.AttachFilter(filter2);
        //    view3.AttachFilter(filter3);

        //    filter1.CalledWhenTrue.Select(x => x.Item1).Should().Equal(10, 30, 44, 66, 90);
        //    filter2.CalledWhenTrue.Select(x => x.Item1).Should().Equal(10, 30, 44, 66, 90);
        //    filter3.CalledWhenTrue.Select(x => x.Item1).Should().Equal(10, 30, 44, 66, 90);

        //    filter1.CalledWhenFalse.Select(x => x.Item1).Should().Equal(21, 45);
        //    filter2.CalledWhenFalse.Select(x => x.Item1).Should().Equal(21, 45);
        //    filter3.CalledWhenFalse.Select(x => x.Item1).Should().Equal(21, 45);

        //    view1.Select(x => x.Value).Should().Equal(10, 30, 44, 66, 90);
        //    view2.Select(x => x.Value).Should().Equal(10, 30, 44, 66, 90);
        //    view3.Select(x => x.Value).Should().Equal(10, 30, 44, 66, 90);

        //    filter1.Clear();
        //    filter2.Clear();
        //    filter3.Clear();

        //    list.Add(100);
        //    list.AddRange(new[] { 101 });
        //    filter1.CalledWhenTrue.Select(x => x.Item1).Should().Equal(100);
        //    filter2.CalledWhenTrue.Select(x => x.Item1).Should().Equal(100);
        //    filter3.CalledWhenTrue.Select(x => x.Item1).Should().Equal(100);
        //    filter1.CalledWhenFalse.Select(x => x.Item1).Should().Equal(101);
        //    filter2.CalledWhenFalse.Select(x => x.Item1).Should().Equal(101);
        //    filter3.CalledWhenFalse.Select(x => x.Item1).Should().Equal(101);

        //    filter1.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex)).Should().Equal((NotifyCollectionChangedAction.Add, 100, 7), (NotifyCollectionChangedAction.Add, 101, 8));
        //    filter2.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex)).Should().Equal((NotifyCollectionChangedAction.Add, 100, 7), (NotifyCollectionChangedAction.Add, 101, 8));
        //    filter3.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex)).Should().Equal((NotifyCollectionChangedAction.Add, 100, 7), (NotifyCollectionChangedAction.Add, 101, 8));

        //    foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

        //    list.Insert(0, 1000);
        //    list.InsertRange(0, new[] { 999 });

        //    filter1.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex)).Should().Equal((NotifyCollectionChangedAction.Add, 1000, 0), (NotifyCollectionChangedAction.Add, 999, 0));
        //    filter2.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex)).Should().Equal((NotifyCollectionChangedAction.Add, 1000, 9), (NotifyCollectionChangedAction.Add, 999, 9)); // sorted index
        //    filter3.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex)).Should().Equal((NotifyCollectionChangedAction.Add, 1000, 9), (NotifyCollectionChangedAction.Add, 999, 9)); // sorted index
        //    foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

        //    list.RemoveAt(0);
        //    list.RemoveRange(0, 1);

        //    filter1.CalledOnCollectionChanged.Select(x => (x.Action, x.OldValue, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Remove, 999, 0), (NotifyCollectionChangedAction.Remove, 1000, 0));
        //    filter2.CalledOnCollectionChanged.Select(x => (x.Action, x.OldValue, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Remove, 999, 9), (NotifyCollectionChangedAction.Remove, 1000, 9));
        //    filter3.CalledOnCollectionChanged.Select(x => (x.Action, x.OldValue, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Remove, 999, 9), (NotifyCollectionChangedAction.Remove, 1000, 9));
        //    foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

        //    list[0] = 9999;

        //    filter1.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Replace, 9999, 0, 0));
        //    filter2.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Replace, 9999, 8, 0));
        //    filter3.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Replace, 9999, 8, 0));
        //    foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

        //    list.Move(3, 0);
        //    filter1.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Move, 44, 0, 3));
        //    filter2.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Move, 44, 2, 2));
        //    filter3.CalledOnCollectionChanged.Select(x => (x.Action, x.NewValue, x.NewViewIndex, x.OldViewIndex)).Should().Equal((NotifyCollectionChangedAction.Move, 44, 2, 2));
        //    foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

        //    list.Clear();
        //    filter1.CalledOnCollectionChanged.Select(x => x.Action).Should().Equal(NotifyCollectionChangedAction.Reset);
        //    filter2.CalledOnCollectionChanged.Select(x => x.Action).Should().Equal(NotifyCollectionChangedAction.Reset);
        //    filter3.CalledOnCollectionChanged.Select(x => x.Action).Should().Equal(NotifyCollectionChangedAction.Reset);
        //}

        //[Fact]
        //public void FilterAndInvokeAddEvent()
        //{
        //    var list = new ObservableList<int>();
        //    var view1 = list.CreateView(x => new ViewContainer<int>(x));
        //    list.AddRange(new[] { 10, 21, 30, 44 });

        //    var filter1 = new TestFilter<int>((x, v) => x % 2 == 0);
        //    view1.AttachFilter((x, v) => x % 2 == 0));

        //    filter1.CalledOnCollectionChanged[0].Action.Should().Be(NotifyCollectionChangedAction.Add);
        //    filter1.CalledOnCollectionChanged[0].NewValue.Should().Be(10);
        //    filter1.CalledOnCollectionChanged[1].Action.Should().Be(NotifyCollectionChangedAction.Add);
        //    filter1.CalledOnCollectionChanged[1].NewValue.Should().Be(21);
        //    filter1.CalledOnCollectionChanged[2].Action.Should().Be(NotifyCollectionChangedAction.Add);
        //    filter1.CalledOnCollectionChanged[2].NewValue.Should().Be(30);
        //    filter1.CalledOnCollectionChanged[3].Action.Should().Be(NotifyCollectionChangedAction.Add);
        //    filter1.CalledOnCollectionChanged[3].NewValue.Should().Be(44);

        //    filter1.CalledWhenTrue.Count.Should().Be(3);
        //    filter1.CalledWhenFalse.Count.Should().Be(1);
        //}
    }
}