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

        /// <summary>
        /// Ensures that a view created over a stack that already has elements enumerates in the same
        /// order as the source (from the top), and that it stays in sync with the source afterwards.
        /// </summary>
        [Fact]
        public void ViewCreatedFromNonEmptySource()
        {
            var stack = new ObservableStack<int>(new[] { 10, 50, 30, 20, 40 });
            var view = stack.CreateView(x => new ViewContainer<int>(x));

            void Equal(params int[] expected)
            {
                stack.Should().Equal(expected);
                view.Select(x => x.Value).Should().Equal(expected);
            }

            Equal(40, 20, 30, 50, 10);

            stack.Push(1);
            Equal(1, 40, 20, 30, 50, 10);

            stack.PushRange(new[] { 2, 3 });
            Equal(3, 2, 1, 40, 20, 30, 50, 10);

            stack.Pop().Should().Be(3);
            Equal(2, 1, 40, 20, 30, 50, 10);

            stack.PopRange(2);
            Equal(40, 20, 30, 50, 10);

            stack.Clear();
            Equal();
        }

        /// <summary>
        /// Ensures that a <c>Pop</c> against a stack whose view was created from a non-empty source
        /// removes the element that was actually popped, both in the view and in the
        /// <see cref="INotifyCollectionChanged"/> notification raised for it.
        /// </summary>
        [Fact]
        public void PopFromViewCreatedFromNonEmptySource()
        {
            var stack = new ObservableStack<int>(new[] { 1, 2, 3 });
            var view = stack.CreateView(x => x);
            using var notify = view.ToNotifyCollectionChanged();

            var removedFromSource = -1;
            stack.CollectionChanged += (in NotifyCollectionChangedEventArgs<int> e) => removedFromSource = e.OldItem;

            var removedFromNotify = -1;
            notify.CollectionChanged += (sender, e) => removedFromNotify = (int)e.OldItems![0]!;

            stack.Pop().Should().Be(3);

            removedFromSource.Should().Be(3);
            removedFromNotify.Should().Be(3);
            stack.Should().Equal(2, 1);
            view.Should().Equal(2, 1);
            notify.Should().Equal(2, 1);
        }
    }
}
