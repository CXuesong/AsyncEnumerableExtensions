using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEnumerableExtensions
{
    public interface IAsyncEnumerableSink<in T>
    {
        Task Yield(T value);
    }

    public class TaskAsyncEnumerable<T> : IAsyncEnumerable<T>
    {

        private readonly Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator;
        private readonly bool acceptsCancellationToken;

        public TaskAsyncEnumerable(Func<IAsyncEnumerableSink<T>, Task> sourceTask)
        {
            if (sourceTask == null) throw new ArgumentNullException(nameof(sourceTask));
            generator = (sink, ct) => sourceTask(sink);
            acceptsCancellationToken = false;
        }

        public TaskAsyncEnumerable(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> sourceTask)
        {
            this.generator = sourceTask ?? throw new ArgumentNullException(nameof(sourceTask));
            acceptsCancellationToken = true;
        }

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new Enumerator(generator, acceptsCancellationToken);
        }

        private class Enumerator : IAsyncEnumerator<T>
        {

            private readonly Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator;
            private readonly bool acceptsCancellationToken;
            private Task generatorTask;
            private AsyncEnumerableBuffer<T> buffer;
            private CancellationTokenSource taskCompletionTokenSource;
            private CancellationToken lastMoveNextCancellationToken = CancellationToken.None;
            private CancellationTokenSource lastCombinedCancellationTokenSource;

            public Enumerator(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator, bool acceptsCancellationToken)
            {
                Debug.Assert(generator != null);
                this.generator = generator;
                this.acceptsCancellationToken = acceptsCancellationToken;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                // Notify the cancellation.
                taskCompletionTokenSource.Cancel();
                if (lastCombinedCancellationTokenSource != taskCompletionTokenSource)
                    lastCombinedCancellationTokenSource.Dispose();
                // Wait for the generator for a while before disposal.
                if (acceptsCancellationToken && !generatorTask.IsCompleted) generatorTask.Wait(200);
                taskCompletionTokenSource.Dispose();
                // Final cleanup.
                buffer.Dispose();
                lastCombinedCancellationTokenSource = null;
                taskCompletionTokenSource = null;
                lastMoveNextCancellationToken = CancellationToken.None;
                buffer = null;
                generatorTask = null;
            }

            /// <inheritdoc />
            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                if (generatorTask == null)
                {
                    buffer = new AsyncEnumerableBuffer<T>();
                    generatorTask = generator(buffer, cancellationToken);
                    taskCompletionTokenSource = new CancellationTokenSource();
                    var forgetit = generatorTask.ContinueWith((_, cts) =>
                            ((CancellationTokenSource)cts).Cancel(), taskCompletionTokenSource,
                        taskCompletionTokenSource.Token);
                    lastCombinedCancellationTokenSource = taskCompletionTokenSource;
                }
                cancellationToken.ThrowIfCancellationRequested();
                // Fast route
                if (buffer.TryTake(out var value))
                {
                    Current = value;
                    return true;
                }
                if (generatorTask.IsCompleted) return false;
                // Slow route
                if (cancellationToken != lastMoveNextCancellationToken)
                {
                    lastMoveNextCancellationToken = cancellationToken;
                    if (lastCombinedCancellationTokenSource != taskCompletionTokenSource)
                        lastCombinedCancellationTokenSource.Dispose();
                    lastCombinedCancellationTokenSource = cancellationToken.CanBeCanceled
                        ? CancellationTokenSource.CreateLinkedTokenSource(taskCompletionTokenSource.Token, cancellationToken)
                        : taskCompletionTokenSource;
                }
                try
                {
                    Current = await buffer.Take(lastCombinedCancellationTokenSource.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested) throw;
                    return false;
                }
            }

            /// <inheritdoc />
            public T Current { get; private set; }
        }

    }

    internal sealed class AsyncEnumerableBuffer<T> : IAsyncEnumerableSink<T>, IDisposable
    {

        private readonly SemaphoreSlim itemSemaphore = new SemaphoreSlim(0, 1);
        private readonly SemaphoreSlim spaceSemaphore = new SemaphoreSlim(1, 1);
        private T item;

        public async Task Yield(T value)
        {
            await spaceSemaphore.WaitAsync().ConfigureAwait(false);
            item = value;
            itemSemaphore.Release();
        }

        public bool TryTake(out T value)
        {
            if (itemSemaphore.Wait(0))
            {
                value = item;
                spaceSemaphore.Release();
                return true;
            }
            value = default(T);
            return false;
        }

        public async Task<T> Take(CancellationToken cancellationToken)
        {
            await itemSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            var localItem = item;
            spaceSemaphore.Release();
            return localItem;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            itemSemaphore.Dispose();
            spaceSemaphore.Dispose();
        }
    }

}
