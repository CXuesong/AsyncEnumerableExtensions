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

        /// <summary>
        /// Creates an instance of <see cref="IAsyncEnumerable{T}"/> from an asynchronous generator method.
        /// </summary>
        /// <typeparam name="T">Type of the items.</typeparam>
        /// <param name="generator">Generator method.</param>
        /// <returns>The encapsulated <see cref="IAsyncEnumerable{T}"/>.</returns>
        /// <remarks>
        /// <para>The <paramref name="generator"/> parameter should use <see cref="IAsyncEnumerableSink{T}"/>
        /// passed in to yield the items.</para>
        /// <para>In this overload, the asynchronous generator accepts a <see cref="CancellationToken"/>
        /// which is cancelled when the <see cref="IAsyncEnumerator{T}"/> is disposed.</para>
        /// </remarks>
        /// <see cref="IAsyncEnumerableSink{T}.Yield(T)"/>
        /// <see cref="IAsyncEnumerableSink{T}.Wait"/>
        /// <see cref="AsyncEnumerableSinkExtensions.YieldAndWait{T}(IAsyncEnumerableSink{T},T)"/>
        public static IAsyncEnumerable<T> FromAsyncGenerator<T>(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            return new TaskAsyncEnumerable<T>(generator);
        }

        /// <summary>
        /// Creates an instance of <see cref="IAsyncEnumerable{T}"/> from an asynchronous generator method.
        /// </summary>
        /// <typeparam name="T">Type of the items.</typeparam>
        /// <param name="generator">Generator method.</param>
        /// <returns>The encapsulated <see cref="IAsyncEnumerable{T}"/>.</returns>
        /// <remarks>
        /// <para>The <paramref name="generator"/> parameter should use <see cref="IAsyncEnumerableSink{T}"/>
        /// passed in to yield the items. </para>
        /// </remarks>
        /// <see cref="IAsyncEnumerableSink{T}.Yield(T)"/>
        /// <see cref="IAsyncEnumerableSink{T}.Wait"/>
        /// <see cref="AsyncEnumerableSinkExtensions.YieldAndWait{T}(IAsyncEnumerableSink{T},T)"/>
        public static IAsyncEnumerable<T> FromAsyncGenerator<T>(Func<IAsyncEnumerableSink<T>, Task> generator)
        {
            if (generator == null) throw new ArgumentNullException(nameof(generator));
            return new TaskAsyncEnumerable<T>(generator);
        }

    }
}
