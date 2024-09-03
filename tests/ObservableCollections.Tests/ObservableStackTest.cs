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

        
    }
}
