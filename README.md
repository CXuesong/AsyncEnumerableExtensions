[CXuesong.AsyncEnumerableExtensions](https://www.nuget.org/packages/CXuesong.AsyncEnumerableExtensions) | ![NuGet version (CXuesong.AsyncEnumerableExtensions)](https://img.shields.io/nuget/vpre/CXuesong.AsyncEnumerableExtensions.svg?style=flat-square) ![NuGet version (CXuesong.AsyncEnumerableExtensions)](https://img.shields.io/nuget/dt/CXuesong.AsyncEnumerableExtensions.svg?style=flat-square)

# AsyncEnumerableExtensions

>   As of May, 2019, there is `IAsyncEnumerable` on .NET Standard 2.1 / .NET Core 3.0. Thus this package uses built-in `IAsyncEnumerable` instead of the one in `Ix.Async` on these supporting platform. Note that on .NET Standard 1.1, this package will still target to `Ix.Async`.

Building your asynchronous sequence, i.e. `IAsyncEnumerable<T>` implementation with asynchronous generator methods! This helper package let you write one generator function, and it's compatible with both .NET Standard 1.1 and .NET Standard 2.1.

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

## Use this package with .NET Core 3.0 Preview

When consuming this package with .NET Core 3.0 Preview, be sure to install the `System.Linq.Async` NuGet package with version higher than `4.0.0-preview.6.build.801`, or you may see the following error during run time when calling certain extension methods like `ToList`.

```
  Error Message:
   System.MissingMethodException : Method not found: 'System.Runtime.CompilerServices.ConfiguredCancelableAsyncEnumerable`1<System.__Canon> System.Threading.Tasks.TaskExtensions.WithCancellation(System.Collections.Generic.IAsyncEnumerable`1<System.__Canon>, System.Threading.CancellationToken)'.
  Stack Trace:
     at System.Linq.AsyncEnumerable.<ToListAsync>g__Core|620_0[TSource](IAsyncEnumerable`1 _source, CancellationToken _cancellationToken) in d:\a\1\s\Ix.NET\Source\System.Linq.Async\System\Linq\Operators\ToList.cs:line 25
     …
```

