using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Threading;

namespace UnitTestProject1
{
    public static class LinqFacade
    {

#if CLR_FEATURE_ASYNC_STREAM
        public static ValueTask<TSource[]> ToArrayAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default(CancellationToken))
            => AsyncEnumerable.ToArrayAsync(source, cancellationToken);
#else
        public static Task<TSource[]> ToArrayAsync<TSource>(this IAsyncEnumerable<TSource> source, CancellationToken cancellationToken = default(CancellationToken))
            => AsyncEnumerable.ToArray(source, cancellationToken);
#endif

    }
}
