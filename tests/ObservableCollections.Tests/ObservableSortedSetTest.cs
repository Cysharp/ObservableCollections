using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ObservableCollections.Tests
{
    public class ObservableSortedSetTest
    {
        [Fact]
        public void View()
        {
            var set = new ObservableSortedSet<int>();
            var view = set.CreateView(x => new ViewContainer<int>(x));

            set.Add(10);
            set.Add(50);
            set.Add(30);
            set.Add(20);
            set.Add(40);

            void Equal(params int[] expected)
            {
                set.Should().BeEquivalentTo(expected);
                view.Select(x => x.Value).Should().BeEquivalentTo(expected);
            }

            Equal(10, 50, 30, 20, 40);

            set.AddRange(new[] { 1, 2, 3, 4, 5 });
            Equal(10, 50, 30, 20, 40, 1, 2, 3, 4, 5);

            set.Remove(10);
            Equal(50, 30, 20, 40, 1, 2, 3, 4, 5);

            set.RemoveRange(new[] { 50, 40 });
            Equal(30, 20, 1, 2, 3, 4, 5);

            set.Clear();

            Equal();
        }

        [Fact]
        public void SortedOrder()
        {
            var set = new ObservableSortedSet<int>();

            set.Add(50);
            set.Add(10);
            set.Add(30);
            set.Add(20);
            set.Add(40);

            // SortedSet maintains sorted order
            set.Should().ContainInOrder(10, 20, 30, 40, 50);
        }

        [Fact]
        public void MinMax()
        {
            var set = new ObservableSortedSet<int>();

            set.Add(50);
            set.Add(10);
            set.Add(30);
            set.Add(20);
            set.Add(40);

            set.Min.Should().Be(10);
            set.Max.Should().Be(50);
        }

        [Fact]
        public void CustomComparer()
        {
            // Descending order comparer
            var set = new ObservableSortedSet<int>(Comparer<int>.Create((x, y) => y.CompareTo(x)));

            set.Add(50);
            set.Add(10);
            set.Add(30);
            set.Add(20);
            set.Add(40);

            // Should be in descending order
            set.Should().ContainInOrder(50, 40, 30, 20, 10);
        }

        [Fact]
        public void IndexOutOfRange()
        {
            // https://github.com/Cysharp/ObservableCollections/pull/51
            static IEnumerable<int> Range(int count)
            {
                foreach (var i in Enumerable.Range(0, count))
                {
                    yield return i;
                }
            }

            var set = new ObservableSortedSet<int>();
            set.AddRange(Range(20));
        }
    }
}
