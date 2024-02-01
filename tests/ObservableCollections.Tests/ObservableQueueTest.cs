using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ObservableCollections.Tests
{
    public class ObservableQueueTests
    {
        [Fact]
        public void View()
        {
            var queue = new ObservableQueue<int>();
            var view = queue.CreateView(x => new ViewContainer<int>(x));

            queue.Enqueue(10);
            queue.Enqueue(50);
            queue.Enqueue(30);
            queue.Enqueue(20);
            queue.Enqueue(40);

            void Equal(params int[] expected)
            {
                queue.Should().Equal(expected);
                view.Select(x => x.Value).Should().Equal(expected);
                view.Select(x => x.View).Should().Equal(expected.Select(x => new ViewContainer<int>(x)));
            }

            Equal(10, 50, 30, 20, 40);

            queue.EnqueueRange(new[] { 1, 2, 3, 4, 5 });
            Equal(10, 50, 30, 20, 40, 1, 2, 3, 4, 5);

            queue.Dequeue().Should().Be(10);
            Equal(50, 30, 20, 40, 1, 2, 3, 4, 5);

            queue.TryDequeue(out var q).Should().BeTrue();
            q.Should().Be(50);
            Equal(30, 20, 40, 1, 2, 3, 4, 5);

            queue.DequeueRange(4);
            Equal(2, 3, 4, 5);

            queue.Clear();

            Equal();
        }

        [Fact]
        public void Filter()
        {
            var queue = new ObservableQueue<int>();
            var view = queue.CreateView(x => new ViewContainer<int>(x));
            var filter = new TestFilter<int>((x, v) => x % 3 == 0);

            queue.Enqueue(10);
            queue.Enqueue(50);
            queue.Enqueue(30);
            queue.Enqueue(20);
            queue.Enqueue(40);

            view.AttachFilter(filter);
            filter.CalledWhenTrue.Select(x => x.Item1).Should().Equal(30);
            filter.CalledWhenFalse.Select(x => x.Item1).Should().Equal(10, 50, 20, 40);

            view.Select(x => x.Value).Should().Equal(30);

            filter.Clear();

            queue.Enqueue(33);
            queue.EnqueueRange(new[] { 98 });

            filter.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Add, 33), (ChangedKind.Add, 98));
            filter.Clear();

            queue.Dequeue();
            queue.DequeueRange(2);
            filter.CalledOnCollectionChanged.Select(x => (x.changedKind, x.value)).Should().Equal((ChangedKind.Remove, 10), (ChangedKind.Remove, 50), (ChangedKind.Remove, 30));
        }
        
        [Fact]
        public void FilterAndInvokeAddEvent()
        {
            var queue = new ObservableQueue<int>();
            var view = queue.CreateView(x => new ViewContainer<int>(x));
            var filter = new TestFilter<int>((x, v) => x % 3 == 0);

            queue.Enqueue(10);
            queue.Enqueue(50);
            queue.Enqueue(30);
            queue.Enqueue(20);
            queue.Enqueue(40);

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
