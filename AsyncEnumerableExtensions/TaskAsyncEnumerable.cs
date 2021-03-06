﻿using System;
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

#if CLR_FEATURE_ASYNC_STREAM
        /// <inheritdoc />
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new Enumerator(generator, cancellationToken);
        }
#else
        /// <inheritdoc />
        public IAsyncEnumerator<T> GetEnumerator()
        {
            return new Enumerator(generator);
        }
#endif

        private class Enumerator : IAsyncEnumerator<T>
        {

            private readonly Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator;
            private Task generatorTask;
            private AsyncEnumerableBuffer<T> buffer;
            private CancellationTokenSource taskCompletionTokenSource;
            private CancellationToken lastMoveNextCancellationToken = CancellationToken.None;
            private CancellationTokenSource lastCombinedCancellationTokenSource;

#if CLR_FEATURE_ASYNC_STREAM
            private readonly CancellationToken cancellationToken;
            public Enumerator(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator, CancellationToken cancellationToken)
            {
                Debug.Assert(generator != null);
                this.generator = generator;
                this.cancellationToken = cancellationToken;
            }
#else
            public Enumerator(Func<IAsyncEnumerableSink<T>, CancellationToken, Task> generator)
            {
                Debug.Assert(generator != null);
                this.generator = generator;
            }
#endif

#if CLR_FEATURE_ASYNC_STREAM
            /// <inheritdoc />
            public ValueTask DisposeAsync()
            {
                if (generatorTask == null) return new ValueTask();
#else
            /// <inheritdoc />
            public void Dispose()
            {
                if (generatorTask == null) return;
#endif
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
#if CLR_FEATURE_ASYNC_STREAM
                return new ValueTask();
#endif
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

#if CLR_FEATURE_ASYNC_STREAM
            public async ValueTask<bool> MoveNextAsync()
#else
            /// <inheritdoc />
            public async Task<bool> MoveNext(CancellationToken cancellationToken)
#endif
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

        private static readonly Task completedTask = Task.FromResult(true);
        private static readonly Task objectDisposedTask;

        static AsyncEnumerableBuffer()
        {
            var tcs = new TaskCompletionSource<bool>();
            tcs.SetException(new ObjectDisposedException(nameof(AsyncEnumerableBuffer<T>)));
            objectDisposedTask = tcs.Task;
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
                if (queue == null) return objectDisposedTask;
                if (queue.Count == 0) return completedTask;
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
            item = default;
            return false;
        }

#if NETSTANDARD2_1
        internal async ValueTask<T> Take(CancellationToken cancellationToken)
#else
        internal async Task<T> Take(CancellationToken cancellationToken)
#endif
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
