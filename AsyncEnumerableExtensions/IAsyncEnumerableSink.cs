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
        /// <param name="item"></param>
        void Yield(T item);

        /// <summary>
        /// Yields new items to the receiver. 
        /// </summary>
        /// <param name="items">The new items generated.</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
        bool Yield(IEnumerable<T> items);

        /// <summary>
        /// Asynchronously waits for the consumer to exhaust all the yielded items.
        /// </summary>
        /// <param name="cancellationToken">The token used to cancel waiting.</param>
        /// <returns>A task that completes when the yielded item has been consumed.</returns>
        Task Wait(CancellationToken cancellationToken);
    }

    public static class AsyncEnumerableSinkExtensions
    {
        private static readonly Task<bool> CompletedTask = Task.FromResult(true);

        /// <summary>
        /// Asynchronously waits for the consumer to exhaust all the yielded items.
        /// </summary>
        /// <returns>A task that completes when the yielded item has been consumed.</returns>
        public static Task Wait<T>(this IAsyncEnumerableSink<T> sink)
        {
            return sink.Wait(CancellationToken.None);
        }

        /// <summary>
        /// Yields a new item to the receiver, and waits for the consumer to consume it.
        /// </summary>
        /// <param name="sink">The sink to receive the new item.</param>
        /// <param name="item">The new item generated.</param>
        /// <returns>A task that completes when the yielded item has been consumed.</returns>
        public static Task YieldAndWait<T>(this IAsyncEnumerableSink<T> sink, T item)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            sink.Yield(item);
            return sink.Wait();
        }

        /// <summary>
        /// Yields new items to the receiver, and waits for the consumer to consume all of them.
        /// </summary>
        /// <param name="sink">The sink to receive the new item.</param>
        /// <param name="items">The new item generated.</param>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is <c>null</c>.</exception>
        /// <returns>A task that completes when the yielded item has been consumed.</returns>
        public static Task YieldAndWait<T>(this IAsyncEnumerableSink<T> sink, IEnumerable<T> items)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (sink.Yield(items))
                return sink.Wait();
            return CompletedTask;
        }

    }

}