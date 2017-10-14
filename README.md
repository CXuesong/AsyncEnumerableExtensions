# AsyncEnumerableExtensions

Some rudimentary utilities to flavor [`Ix.Async`](https://github.com/Reactive-Extensions/Rx.NET), such as…

Build your asynchronous sequence, i.e. `IAsyncEnumerable<T>` implementation with asynchronous generator methods!

```c#
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
```

For more usage examples, including cancellation support, see <UnitTestProject1/UnitTest1.cs>.