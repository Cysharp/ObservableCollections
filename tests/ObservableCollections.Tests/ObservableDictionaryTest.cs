using FluentAssertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace ObservableCollections.Tests
{
    public class ObservableDictionaryTest
    {
        [Fact]
        public void View()
        {
            var dict = new ObservableDictionary<int, int>();
            var view = dict.CreateView(x => new ViewContainer<int>(x.Value));

            dict.Add(10, -10); // 0
            dict.Add(50, -50); // 1
            dict.Add(30, -30); // 2
            dict.Add(20, -20); // 3
            dict.Add(40, -40); // 4

            void Equal(params int[] expected)
            {
                dict.Select(x => x.Value).OrderByDescending(x => x).Should().Equal(expected);
                view.Select(x => x.Value.Value).OrderByDescending(x => x).Should().Equal(expected);
            }

            Equal(-10, -20, -30, -40, -50);

            dict[99] = -100;
            Equal(-10, -20, -30, -40, -50, -100);

            dict[10] = -5;
            Equal(-5, -20, -30, -40, -50, -100);

            dict.Remove(20);
            Equal(-5, -30, -40, -50, -100);

            dict.Clear();
            Equal(new int[0]);
        }

        [Fact]
        public void ViewSorted()
        {
            var dict = new ObservableDictionary<int, int>();
            var view1 = dict.CreateSortedView(x => x.Key, x => new ViewContainer<int>(x.Value), x => x.Value, true);
            var view2 = dict.CreateSortedView(x => x.Key, x => new ViewContainer<int>(x.Value), x => x.Value, false);

            dict.Add(10, 10); // 0
            dict.Add(50, 50); // 1
            dict.Add(30, 30); // 2
            dict.Add(20, 20); // 3
            dict.Add(40, 40); // 4

            void Equal(params int[] expected)
            {
                dict.Select(x => x.Value).OrderBy(x => x).Should().Equal(expected);
                view1.Select(x => x.Value.Value).Should().Equal(expected);
                view2.Select(x => x.Value.Value).Should().Equal(expected.OrderByDescending(x => x));
            }

            Equal(10, 20, 30, 40, 50);

            dict[99] = 100;
            Equal(10, 20, 30, 40, 50, 100);

            dict[10] = -5;
            Equal(-5, 20, 30, 40, 50, 100);

            dict.Remove(20);
            Equal(-5, 30, 40, 50, 100);

            dict.Clear();
            Equal(new int[0]);
        }

        [Fact]
        public void Freezed()
        {
            var dict = new FreezedDictionary<int, int>(new Dictionary<int, int>
            {
                [10] = 10,
                [50] = 50,
                [30] = 30,
                [20] = 20,
                [40] = 40,
                [60] = 60
            });

            var view = dict.CreateSortableView(x => new ViewContainer<int>(x.Value));

            view.Sort(x => x.Key, true);
            view.Select(x => x.Value.Value).Should().Equal(10, 20, 30, 40, 50, 60);
            view.Select(x => x.View).Should().Equal(10, 20, 30, 40, 50, 60);

            view.Sort(x => x.Key, false);
            view.Select(x => x.Value.Value).Should().Equal(60, 50, 40, 30, 20, 10);
            view.Select(x => x.View).Should().Equal(60, 50, 40, 30, 20, 10);
        }

        [Fact]
        public void FilterTest()
        {
            var dict = new ObservableDictionary<int, int>();
            var view1 = dict.CreateView(x => new ViewContainer<int>(x.Value));
            var view2 = dict.CreateSortedView(x => x.Key, x => new ViewContainer<int>(x.Value), x => x.Value, true);
            var view3 = dict.CreateSortedView(x => new ViewContainer<int>(x.Value), x => x.Value, viewComparer: Comparer<ViewContainer<int>>.Default);
            var filter1 = new TestFilter2<int>((x, v) => x.Value % 2 == 0);
            var filter2 = new TestFilter2<int>((x, v) => x.Value % 2 == 0);
            var filter3 = new TestFilter2<int>((x, v) => x.Value % 2 == 0);

            dict.Add(10, -12); // 0
            dict.Add(50, -53); // 1
            dict.Add(30, -34); // 2
            dict.Add(20, -25); // 3
            dict.Add(40, -40); // 4

            view1.AttachFilter(filter1);
            view2.AttachFilter(filter2);
            view3.AttachFilter(filter3);

            filter1.CalledWhenTrue.Select(x => x.Item1.Value).Should().Equal(-12, -34, -40);
            filter2.CalledWhenTrue.Select(x => x.Item1.Value).Should().Equal(-40, -34, -12);
            filter3.CalledWhenTrue.Select(x => x.Item1.Value).Should().Equal(-40, -34, -12);

            dict.Add(99, -100);
            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Add, -100));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Add, -100));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Add, -100));
            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            dict[10] = -1090;
            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Remove, -12), (ChangedKind.Add, -1090));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Remove, -12), (ChangedKind.Add, -1090));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Remove, -12), (ChangedKind.Add, -1090));
            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            dict.Remove(20);
            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Remove, -25));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Remove, -25));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value)).Should().Equal((ChangedKind.Remove, -25));
            foreach (var item in new[] { filter1, filter2, filter3 }) item.CalledOnCollectionChanged.Clear();

            dict.Clear();
            filter1.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value))
                .OrderBy(x => x.Value)
                .Should().Equal((ChangedKind.Remove, -1090), (ChangedKind.Remove, -100), (ChangedKind.Remove, -53), (ChangedKind.Remove, -40), (ChangedKind.Remove, -34));
            filter2.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value))
                .OrderBy(x => x.Value)
                .Should().Equal((ChangedKind.Remove, -1090), (ChangedKind.Remove, -100), (ChangedKind.Remove, -53), (ChangedKind.Remove, -40), (ChangedKind.Remove, -34));
            filter3.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value.Value))
                .OrderBy(x => x.Value)
                .Should().Equal((ChangedKind.Remove, -1090), (ChangedKind.Remove, -100), (ChangedKind.Remove, -53), (ChangedKind.Remove, -40), (ChangedKind.Remove, -34));
        }
    }
}
