using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEnumerableExtensions
{
    /// <summary>
    /// Represents a receiver for yielded items.
    /// </summary>
    /// <typeparam name="T">The type of item.</typeparam>
    public interface IAsyncEnumerableSink<in T>
    {
        /// <summary>
        /// Yields a new item to the receiver. 
        /// </summary>
        /// <param name="value">The new item generated.</param>
        /// <param name="cancellationToken">The token used to cancel the operation.</param>
        /// <returns>A task that completes when the yielded item has been consumed.</returns>
        Task Yield(T value, CancellationToken cancellationToken);
    }

    public static class AsyncEnumerableSinkExtensions
    {
        /// <summary>
        /// Yields a new item to the receiver. 
        /// </summary>
        /// <param name="sink">The sink to receive the new item.</param>
        /// <param name="value">The new item generated.</param>
        /// <returns>A task that completes when the yielded item has been consumed.</returns>
        public static Task Yield<T>(this IAsyncEnumerableSink<T> sink, T value)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            return sink.Yield(value, CancellationToken.None);
        }

    }

}