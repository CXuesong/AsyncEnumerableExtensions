using System;
using System.Linq;
using System.Threading.Tasks;
using AsyncEnumerableExtensions;
using Xunit;

namespace UnitTestProject1
{
    public class UnitTest1
    {
        [Fact]
        public async void Test1()
        {
            var finished = false;
            async Task Generator(IAsyncEnumerableSink<int> sink)
            {
                await sink.YieldAndWait(10);
                await sink.YieldAndWait(20);
                await Task.Delay(100);
                await sink.YieldAndWait(30);
                await sink.YieldAndWait(40);
                await Task.Delay(100);
                await sink.YieldAndWait(50);
                finished = true;
            }

            var array = await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).ToArray();
            Assert.True(finished);
            Assert.Equal(new[] {10, 20, 30, 40, 50}, array);
        }

        [Fact]
        public async void Test2()
        {
            async Task Generator(IAsyncEnumerableSink<int> sink)
            {
                int value = 1;
                NEXT:
                value *= 2;
                await sink.YieldAndWait(value);
                goto NEXT;
            }

            var array = await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).Take(5).ToArray();
            Assert.Equal(new[] {2, 4, 8, 16, 32}, array);
        }

    }
}
