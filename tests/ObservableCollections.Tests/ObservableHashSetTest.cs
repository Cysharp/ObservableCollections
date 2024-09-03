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
    public class ObservableHashSetTest
    {
        [Fact]
        public void View()
        {
            var set = new ObservableHashSet<int>();
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

            var set = new ObservableHashSet<int>();
            set.AddRange(Range(20));
        }
    }
}
