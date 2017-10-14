using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsyncEnumerableExtensions;
using Xunit;

namespace UnitTestProject1
{
    public class UnitTest1
    {
        [Fact]
        public async void NormalGeneratorTest()
        {
            var finished = false;
            async Task Generator(IAsyncEnumerableSink<int> sink)
            {
                // Yield some items.
                await sink.YieldAndWait(10);
                await sink.YieldAndWait(20);
                // Do some work.
                await Task.Delay(100);
                // Yield some more items.
                await sink.YieldAndWait(new[] {30, 40});
                // Ditto.
                await Task.Delay(100);
                await sink.YieldAndWait(50);
                finished = true;
            }

            var array = await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).ToArray();
            Assert.True(finished);
            Assert.Equal(new[] {10, 20, 30, 40, 50}, array);
        }

        [Fact]
        public async void CancellationTest()
        {
            async Task Generator(IAsyncEnumerableSink<int> sink, CancellationToken token)
            {
                int value = 1;
                NEXT:
                value *= 2;
                await sink.YieldAndWait(value);
                await Task.Delay(100, token);
                goto NEXT;
            }

            var array = await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).Take(5).ToArray();
            Assert.Equal(new[] {2, 4, 8, 16, 32}, array);
        }

        [Fact]
        public void EmptyGeneratorTest()
        {
            async Task Generator(IAsyncEnumerableSink<int> sink)
            {
                await Task.Delay(100);
            }
            
            Assert.Empty(AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).ToEnumerable());
        }

        [Fact]
        public async void GeneratorExceptionTest()
        {
            async Task Generator(IAsyncEnumerableSink<int> sink)
            {
                await Task.Delay(100);
                await sink.YieldAndWait(10);
                await sink.YieldAndWait(20);
                throw new InvalidDataException();
            }

            Assert.Equal(new[] {10, 20}, await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).Take(2).ToArray());
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
                await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).ToArray());
        }

    }
}
