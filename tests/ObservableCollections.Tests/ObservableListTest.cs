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
    }
}
