using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ObservableCollections.Tests
{
    public class RingBufferTest
    {
        [Fact]
        public void All()
        {
            var list = new RingBuffer<int>();

            // befin from last...
            list.AddLast(1); list.Should().Equal(1);
            list.AddLast(2); list.Should().Equal(1, 2);
            list.AddLast(3); list.Should().Equal(1, 2, 3);
            list.AddLast(4); list.Should().Equal(1, 2, 3, 4);
            list.AddLast(5); list.Should().Equal(1, 2, 3, 4, 5);
            list.AddLast(6); list.Should().Equal(1, 2, 3, 4, 5, 6);

            list.RemoveLast().Should().Be(6);
            list.RemoveLast().Should().Be(5);
            list.Should().Equal(1, 2, 3, 4);
            list.Reverse().Should().Equal(4, 3, 2, 1);

            list.RemoveFirst().Should().Be(1);
            list.RemoveFirst().Should().Be(2);
            list.Should().Equal(3, 4);

            list.AddFirst(99);
            list.AddLast(88);
            list.Should().Equal(99, 3, 4, 88);

            // Adding Loop
            list.AddLast(5);
            list.AddLast(6);
            list.AddLast(7);
            list.AddLast(8);
            list.Should().Equal(99, 3, 4, 88, 5, 6, 7, 8);
            list.Reverse().Should().Equal(8, 7, 6, 5, 88, 4, 3, 99);


            // copy
            {
                var newArray = new int[10];
                list.CopyTo(newArray, 0);
                newArray.Should().Equal(99, 3, 4, 88, 5, 6, 7, 8, 0, 0);
            }
            {
                var newArray = new int[10];
                list.CopyTo(newArray, 1);
                newArray.Should().Equal(0, 99, 3, 4, 88, 5, 6, 7, 8, 0);
            }

            list.Clear();

            // befin from first...
            list.AddFirst(1);
            list.AddFirst(2);
            list.AddFirst(3);
            list.AddFirst(4);
            list.AddFirst(5);
            list.AddFirst(6);

            list.Should().Equal(6, 5, 4, 3, 2, 1);
            list.Reverse().Should().Equal(1, 2, 3, 4, 5, 6);

            list.RemoveLast().Should().Be(1);
            list.RemoveLast().Should().Be(2);
            list.Should().Equal(6, 5, 4, 3);

            list.RemoveFirst().Should().Be(6);
            list.RemoveFirst().Should().Be(5);
            list.Should().Equal(4, 3);

            list.AddFirst(99);
            list.AddLast(88);
            list.Should().Equal(99, 4, 3, 88);

            list.AddFirst(5);
            list.AddFirst(6);
            list.AddFirst(7);
            list.AddFirst(8);
            list.Should().Equal(8, 7, 6, 5, 99, 4, 3, 88);

            // set, get
            list[0].Should().Be(8);
            list[1].Should().Be(7);
            list[2].Should().Be(6);
            list[3].Should().Be(5);
            list[4].Should().Be(99);
            list[5].Should().Be(4);
            list[6].Should().Be(3);
            list[7].Should().Be(88);

            list[0] = 999;
            list[4] = 1099;
            list[7] = 888;

            // ensure capacity
            list.AddFirst(9);
            list.Should().Equal(9, 999, 7, 6, 5, 1099, 4, 3, 888);
            list.Reverse().Should().Equal(888, 3, 4, 1099, 5, 6, 7, 999, 9);

            list.AddFirst(199);
            list.AddLast(299);
            list.Should().Equal(199, 9, 999, 7, 6, 5, 1099, 4, 3, 888, 299);
            list.Reverse().Should().Equal(299, 888, 3, 4, 1099, 5, 6, 7, 999, 9, 199);

            // copy
            {
                var newArray = new int[15];
                list.CopyTo(newArray, 0);
                newArray.Should().Equal(199, 9, 999, 7, 6, 5, 1099, 4, 3, 888, 299, 0, 0, 0, 0);
            }
            {
                var newArray = new int[15];
                list.CopyTo(newArray, 2);
                newArray.Should().Equal(0, 0, 199, 9, 999, 7, 6, 5, 1099, 4, 3, 888, 299, 0, 0);
            }

        }

        [Fact]
        public void Iteration()
        {
            var empty = new RingBuffer<int>();
            empty.ToArray().Should().BeEmpty();


            for (int i = 0; i < 10; i++)
            {
                var buffer = new RingBuffer<int>();
                for (int j = 0; j < i; j++)
                {
                    buffer.AddLast(j);
                }
                buffer.ToArray().Should().Equal(Enumerable.Range(0, i).ToArray());
            }

            for (int i = 0; i < 10; i++)
            {
                var buffer = new RingBuffer<int>();
                for (int j = 0; j < i; j++)
                {
                    buffer.AddFirst(j);
                }
                buffer.ToArray().Should().Equal(Enumerable.Range(0, i).Reverse().ToArray());
            }
        }

        [Fact]
        public void RandomIteration()
        {
            var buffer = new RingBuffer<int>();
            buffer.AddFirst(10);
            buffer.AddLast(20);
            buffer.AddLast(30);
            buffer.AddFirst(40);

            buffer.ToArray().Should().Equal(40, 10, 20, 30);
        }

        [Fact]
        public void BinarySearchTest()
        {
            var empty = new RingBuffer<int>(new int[] { });
            var emptyL = new List<int>();
            var single = new RingBuffer<int>(new[] { 10 });
            var singleL = new List<int>(new[] { 10 });
            var buffer = new RingBuffer<int>(new[]
            {
                1, 4, 5, 6, 10, 14, 15,17, 20, 33
            });
            var multiL = new List<int>(new[]
            {
                1, 4, 5, 6, 10, 14, 15,17, 20, 33
            });

            empty.BinarySearch(99).Should().BeLessThan(0);
            empty.BinarySearch(99).Should().Be(emptyL.BinarySearch(99));
            {
                single.BinarySearch(10).Should().Be(0);
                var x1 = single.BinarySearch(4);
                x1.Should().BeLessThan(0);
                (~x1).Should().Be(0);
                x1.Should().Be(single.BinarySearch(4));

                var x2 = single.BinarySearch(40);
                x2.Should().BeLessThan(0);
                (~x2).Should().Be(1);
                x2.Should().Be(single.BinarySearch(40));
            }

            {
                for (int i = 0; i < 50; i++)
                {
                    buffer.BinarySearch(i).Should().Be(multiL.BinarySearch(i));
                }
            }





        }
    }
}
