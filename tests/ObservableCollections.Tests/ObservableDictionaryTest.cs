using FluentAssertions;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

        
    }
}
