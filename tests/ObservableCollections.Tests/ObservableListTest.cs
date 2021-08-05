using FluentAssertions;
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

            Equal(10, 50, 30, 20, 40);

            reference.Move(3, 1);
            list.Move(3, 1);
            Equal(10, 20, 50, 30, 40);

            reference.Insert(2, 99);
            list.Insert(2, 99);
            Equal(10, 20, 99, 50, 30, 40);
        }
    }
}
