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
    }
}
