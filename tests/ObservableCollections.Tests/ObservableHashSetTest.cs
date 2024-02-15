using FluentAssertions;
using System;
using System.Collections.Generic;
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
                view.Select(x => x.View.Value).Should().BeEquivalentTo(expected);
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
        public void Filter()
        {
            var set = new ObservableHashSet<int>();
            var view = set.CreateView(x => new ViewContainer<int>(x));
            var filter = new TestFilter<int>((x, v) => x % 3 == 0);

            set.Add(10);
            set.Add(50);
            set.Add(30);
            set.Add(20);
            set.Add(40);

            view.AttachFilter(filter);
            filter.CalledWhenTrue.Select(x => x.Item1).Should().Equal(30);
            filter.CalledWhenFalse.Select(x => x.Item1).Should().Equal(10, 50, 20, 40);

            view.Select(x => x.Value).Should().Equal(30);

            filter.Clear();

            set.Add(33);
            set.AddRange(new[] { 98 });

            filter.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 33), (ChangedKind.Add, 98));
            filter.Clear();

            set.Remove(10);
            set.RemoveRange(new[] { 50, 30 });
            filter.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 10), (ChangedKind.Remove, 50), (ChangedKind.Remove, 30));
        }
        
        [Fact]
        public void FilterAndInvokeAddEvent()
        {
            var set = new ObservableHashSet<int>();
            var view = set.CreateView(x => new ViewContainer<int>(x));
            var filter = new TestFilter<int>((x, v) => x % 3 == 0);

            set.Add(10);
            set.Add(50);
            set.Add(30);
            set.Add(20);
            set.Add(40);

            view.AttachFilter(filter, true);
            filter.CalledOnCollectionChanged.Count.Should().Be(5);
            filter.CalledOnCollectionChanged[0].changedKind.Should().Be(ChangedKind.Add);
            filter.CalledOnCollectionChanged[0].value.Should().Be(10);
            filter.CalledOnCollectionChanged[1].changedKind.Should().Be(ChangedKind.Add);
            filter.CalledOnCollectionChanged[1].value.Should().Be(50);
            filter.CalledOnCollectionChanged[2].changedKind.Should().Be(ChangedKind.Add);
            filter.CalledOnCollectionChanged[2].value.Should().Be(30);
            filter.CalledOnCollectionChanged[3].changedKind.Should().Be(ChangedKind.Add);
            filter.CalledOnCollectionChanged[3].value.Should().Be(20);
            filter.CalledOnCollectionChanged[4].changedKind.Should().Be(ChangedKind.Add);
            filter.CalledOnCollectionChanged[4].value.Should().Be(40);

            filter.CalledWhenTrue.Count.Should().Be(1);
            filter.CalledWhenFalse.Count.Should().Be(4);
        }   
    }
}
