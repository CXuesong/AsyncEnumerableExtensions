# AsyncEnumerableExtensions

>   The package is available on NuGet as `CXuesong.AsyncEnumerableExtensions`.
>
>   As of May, 2019, there is `IAsyncEnumerable` on .NET Standard 2.1 / .NET Core 3.0. Thus this package uses built-in `IAsyncEnumerable` instead of the one in `Ix.Async` on these supporting platform. Note that prior platform versions will still target to `Ix.Async`.

Some rudimentary utilities to flavor [`Ix.Async`](https://github.com/Reactive-Extensions/Rx.NET), such as…

Building your asynchronous sequence, i.e. `IAsyncEnumerable<T>` implementation with asynchronous generator methods!

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

    // Replace ToArray with ToArrayAsync on .NET Core 3.0 / .NET Standard 2.1
    var array = await AsyncEnumerableFactory.FromAsyncGenerator<int>(Generator).ToArray();
    Assert.True(finished);
    Assert.Equal(new[] {10, 20, 30, 40, 50}, array);
}
```

For more usage examples, including cancellation support, see [`UnitTestProject1/UnitTest1.cs`](UnitTestProject1/UnitTest1.cs).