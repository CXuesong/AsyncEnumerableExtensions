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
            async Task Generator(IAsyncEnumerableSink<int> sink)
            {
                await sink.Yield(10);
                await sink.Yield(20);
                await Task.Delay(100);
                await sink.Yield(30);
                await sink.Yield(40);
                await Task.Delay(100);
                await sink.Yield(50);
            }

            Assert.Equal(new[] {10, 20, 30, 40, 50},
                await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).ToArray());
        }
    }
}
