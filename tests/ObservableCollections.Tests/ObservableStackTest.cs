using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ObservableCollections.Tests
{
    public class ObservableStackTests
    {
        [Fact]
        public void View()
        {
            var stack = new ObservableStack<int>();
            var view = stack.CreateView(x => new ViewContainer<int>(x));

            stack.Push(10);
            stack.Push(50);
            stack.Push(30);
            stack.Push(20);
            stack.Push(40);

            void Equal(params int[] expected)
            {
                stack.Should().Equal(expected);
                view.Select(x => x.Value).Should().Equal(expected);
                view.Select(x => x.View).Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            Equal(40, 20, 30, 50, 10);

            stack.PushRange(new[] { 1, 2, 3, 4, 5 });
            Equal(5, 4, 3, 2, 1, 40, 20, 30, 50, 10);

            stack.Pop().Should().Be(5);
            Equal(4, 3, 2, 1, 40, 20, 30, 50, 10);

            stack.TryPop(out var q).Should().BeTrue();
            q.Should().Be(4);
            Equal(3, 2, 1, 40, 20, 30, 50, 10);

            stack.PopRange(4);
            Equal(20, 30, 50, 10);

            stack.Clear();

            Equal();
        }

        [Fact]
        public void Filter()
        {
            var stack = new ObservableStack<int>();
            var view = stack.CreateView(x => new ViewContainer<int>(x));
            var filter = new TestFilter<int>((x, v) => x % 3 == 0);

            stack.Push(10);
            stack.Push(50);
            stack.Push(30);
            stack.Push(20);
            stack.Push(40);

            view.AttachFilter(filter);
            filter.CalledWhenTrue.Select(x => x.Item1).Should().Equal(30);
            filter.CalledWhenFalse.Select(x => x.Item1).Should().Equal(40, 20, 50, 10);

            view.Select(x => x.Value).Should().Equal(30);

            filter.Clear();

            stack.Push(33);
            stack.PushRange(new[] { 98 });

            filter.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 33), (ChangedKind.Add, 98));
            filter.Clear();

            stack.Pop();
            stack.PopRange(2);
            filter.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 98), (ChangedKind.Remove, 33), (ChangedKind.Remove, 40));
        }
    }
}
