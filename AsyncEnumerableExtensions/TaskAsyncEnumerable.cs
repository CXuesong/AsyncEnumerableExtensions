using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncEnumerableExtensions
{
    internal class TaskAsyncEnumerable<T> : IAsyncEnumerable<T>
    {

        private readonly Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator;

        public TaskAsyncEnumerable(Func<IAsyncEnumerableSink<T>, Task> sourceTask)
        {
            if (sourceTask == null) throw new ArgumentNullException(nameof(sourceTask));
            generator = (sink, ct) => sourceTask(sink);
        }

        public TaskAsyncEnumerable(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> sourceTask)
        {
            this.generator = sourceTask ?? throw new ArgumentNullException(nameof(sourceTask));
        }

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new Enumerator(generator);
        }

        private class Enumerator : IAsyncEnumerator<T>
        {

            private readonly Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator;
            private Task generatorTask;
            private AsyncEnumerableBuffer<T> buffer;
            private CancellationTokenSource taskCompletionTokenSource;
            private CancellationToken lastMoveNextCancellationToken = CancellationToken.None;
            private CancellationTokenSource lastCombinedCancellationTokenSource;

            public Enumerator(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator)
            {
                Debug.Assert(generator != null);
                this.generator = generator;
            }

            /// <inheritdoc />
            public void Dispose()
            {
                if (generatorTask == null) return;
                // Notify the cancellation.
                if (lastCombinedCancellationTokenSource != taskCompletionTokenSource)
                    lastCombinedCancellationTokenSource.Dispose();
                taskCompletionTokenSource.Cancel();
                taskCompletionTokenSource.Dispose();
                // Do cleanup.
                buffer.Terminate();
                lastCombinedCancellationTokenSource = null;
                taskCompletionTokenSource = null;
                lastMoveNextCancellationToken = CancellationToken.None;
                buffer = null;
                generatorTask = null;
            }

            private void PropagateGeneratorException()
            {
                if (generatorTask.Exception != null)
                {
                    if (generatorTask.Exception.InnerExceptions.Count == 1)
                        ExceptionDispatchInfo.Capture(generatorTask.Exception.InnerExceptions[0]).Throw();
                    else
                        ExceptionDispatchInfo.Capture(generatorTask.Exception).Throw();
                }
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
                if (buffer.TryTake(out var cur))
                {
                    Current = cur;
                    return true;
                }
                // Currently no items available.
                if (generatorTask.IsCompleted)
                {
                    PropagateGeneratorException();
                    return false;
                }
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
                    PropagateGeneratorException();
                    return false;
                }
            }

            /// <inheritdoc />
            public T Current { get; private set; }
        }

    }

    internal sealed class AsyncEnumerableBuffer<T> : IAsyncEnumerableSink<T>
    {

        private TaskCompletionSource<bool> onYieldTcs;
        private TaskCompletionSource<bool> onQueueExhaustedTcs;
        private Queue<T> queue = new Queue<T>();
        private readonly object syncLock = new object();

        private static readonly Task CompletedTask = Task.FromResult(true);
        private static readonly Task ObjectDisposedTask;

        static AsyncEnumerableBuffer()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetException(new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>)));
            ObjectDisposedTask = tcs.Task;
        }

        public void Yield(T item)
        {
            TaskCompletionSource<bool> localTask;
            lock (syncLock)
            {
                if (queue == null) throw new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>));
                queue.Enqueue(item);
                localTask = onYieldTcs;
                // Note: We need to set onYieldTcs to null BEFORE calling TrySetResult,
                // because it will directly invoke continuation methods on caller's stack.
                onYieldTcs = null;
            }
            localTask?.TrySetResult(true);
        }

        /// <inheritdoc />
        public bool Yield(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            TaskCompletionSource<bool> localTask;
            var anyItem = false;
            lock (syncLock)
            {
                if (queue == null) throw new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>));
                foreach (var i in items)
                {
                    anyItem = true;
                    queue.Enqueue(i);
                }
                localTask = onYieldTcs;
                onYieldTcs = null;
            }
            localTask?.TrySetResult(true);
            return anyItem;
        }

        public Task Wait(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.Delay(-1, cancellationToken);
            lock (syncLock)
            {
                if (queue == null) return ObjectDisposedTask;
                if (queue.Count == 0) return CompletedTask;
                if (onQueueExhaustedTcs == null)
                {
                    onQueueExhaustedTcs = new TaskCompletionSource<bool>();
                }
                if (cancellationToken.CanBeCanceled)
                    return Task.WhenAny(onQueueExhaustedTcs.Task, Task.Delay(-1, cancellationToken)).Unwrap();
                return onQueueExhaustedTcs.Task;
            }
        }

        internal bool TryTake(out T item)
        {
            TaskCompletionSource<bool> localTask;
            lock (syncLock)
            {
                if (queue == null) throw new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>));
                if (queue.Count > 0)
                {
                    item = queue.Dequeue();
                    return true;
                }
                localTask = onQueueExhaustedTcs;
                onQueueExhaustedTcs = null;
            }
            localTask?.TrySetResult(true);
            item = default(T);
            return false;
        }
        
        internal async Task<T> Take(CancellationToken cancellationToken)
        {
            while (true)
            {
                Task onYield;
                TaskCompletionSource<bool> localTask;
                lock (syncLock)
                {
                    if (queue == null) throw new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>));
                    if (queue.Count > 0) return queue.Dequeue();
                    if (onYieldTcs == null)
                        onYieldTcs = new TaskCompletionSource<bool>();
                    onYield = onYieldTcs.Task;
                    localTask = onQueueExhaustedTcs;
                    onQueueExhaustedTcs = null;
                }
                localTask?.TrySetResult(true);
                // Wait for the generator yields.
                var completedTask = await Task.WhenAny(onYield, Task.Delay(-1, cancellationToken)).ConfigureAwait(false);
                if (completedTask != onYield) cancellationToken.ThrowIfCancellationRequested();
            }
        }

        internal void Terminate()
        {
            lock (syncLock)
            {
                onQueueExhaustedTcs?.TrySetException(new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>)));
                onYieldTcs?.TrySetException(new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>)));
                onQueueExhaustedTcs = null;
                onYieldTcs = null;
                queue = null;
            }
        }

    }

}
