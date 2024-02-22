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

            filter.CalledOnCollectionChanged.Select(x => (x.changedAction, x.value)).Should().Equal((NotifyCollectionChangedAction.Add, 33), (NotifyCollectionChangedAction.Add, 98));
            filter.Clear();

            stack.Pop();
            stack.PopRange(2);
            filter.CalledOnCollectionChanged.Select(x => (x.changedAction, x.value)).Should().Equal((NotifyCollectionChangedAction.Remove, 98), (NotifyCollectionChangedAction.Remove, 33), (NotifyCollectionChangedAction.Remove, 40));
        }
        
        [Fact]
        public void FilterAndInvokeAddEvent()
        {
            var stack = new ObservableStack<int>();
            var view = stack.CreateView(x => new ViewContainer<int>(x));
            var filter = new TestFilter<int>((x, v) => x % 3 == 0);

            stack.Push(10);
            stack.Push(50);
            stack.Push(30);
            stack.Push(20);
            stack.Push(40);

            view.AttachFilter(filter, true);
            filter.CalledOnCollectionChanged.Count.Should().Be(5);
            filter.CalledOnCollectionChanged[4].changedAction.Should().Be(NotifyCollectionChangedAction.Add);
            filter.CalledOnCollectionChanged[4].value.Should().Be(10);
            filter.CalledOnCollectionChanged[3].changedAction.Should().Be(NotifyCollectionChangedAction.Add);
            filter.CalledOnCollectionChanged[3].value.Should().Be(50);
            filter.CalledOnCollectionChanged[2].changedAction.Should().Be(NotifyCollectionChangedAction.Add);
            filter.CalledOnCollectionChanged[2].value.Should().Be(30);
            filter.CalledOnCollectionChanged[1].changedAction.Should().Be(NotifyCollectionChangedAction.Add);
            filter.CalledOnCollectionChanged[1].value.Should().Be(20);
            filter.CalledOnCollectionChanged[0].changedAction.Should().Be(NotifyCollectionChangedAction.Add);
            filter.CalledOnCollectionChanged[0].value.Should().Be(40);

            filter.CalledWhenTrue.Count.Should().Be(1);
            filter.CalledWhenFalse.Count.Should().Be(4);
        }   
        
    }
}
