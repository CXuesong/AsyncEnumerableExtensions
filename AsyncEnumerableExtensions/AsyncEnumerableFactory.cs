using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEnumerableExtensions
{
    /// <summary>
    /// Provides static factory methods for <see cref="IAsyncEnumerable{T}"/>.
    /// </summary>
    public static class AsyncEnumerableFactory
    {

        public static IAsyncEnumerable<T> FromAsyncGenerator<T>(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator)
        {
            return new TaskAsyncEnumerable<T>(generator);
        }

        public static IAsyncEnumerable<T> FromAsyncGenerator<T>(Func<IAsyncEnumerableSink<T>, Task> generator)
        {
            return new TaskAsyncEnumerable<T>(generator);
        }

    }
}
