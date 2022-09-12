using Castle.DynamicProxy;

namespace CachingAOP
{
    public class CacheAsyncInterceptor : AsyncInterceptorBase
    {
        private CacheService _memoryCache;
        public CacheAsyncInterceptor(CacheService memoryCache)
        {
            _memoryCache = memoryCache;
        }

        // Create a cache key using the name of the method and the values
        // of its arguments so that if the same method is called with the`
        // same arguments in the future, we can find out if the results 
        // are cached or not
        private static string GenerateCacheKey(string name,
            object[] arguments)
        {
            if (arguments == null || arguments.Length == 0)
                return name;
            return name + "--" +
                string.Join("--", arguments.Select(a =>
                    a == null ? "**NULL**" : a.ToString()).ToArray());
        }

        protected async override Task InterceptAsync(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task> proceed)
        {
            await proceed(invocation, proceedInfo);
        }

        protected async override Task<TResult> InterceptAsync<TResult>(IInvocation invocation, IInvocationProceedInfo proceedInfo, Func<IInvocation, IInvocationProceedInfo, Task<TResult>> proceed)
        {
            var cacheAttribute = invocation.MethodInvocationTarget
                .GetCustomAttributes(typeof(CacheAttribute), false)
                .FirstOrDefault() as CacheAttribute;

            // If the cache attribute is added to this method, we 
            // need to intercept this call
            if (cacheAttribute != null)
            {
                var cacheKey = GenerateCacheKey(invocation.Method.Name,
                    invocation.Arguments);
                object? result;
                if (_memoryCache.Contains(cacheKey))
                {
                    // The results were already in the cache so return 
                    // them from the cache instead of calling the 
                    // underlying method
                    result = _memoryCache.Get(cacheKey);
                }
                else
                {
                    // Get the result the hard way by calling 
                    // the underlying method
                    result = await proceed(invocation, proceedInfo);
                    // Save the result in the cache
                    _memoryCache.Set(cacheKey, result);
                }
                return (TResult)result;
            }
            else
            {
                // We don't need to cache the results, 
                // nothing to see here
                return await proceed(invocation, proceedInfo);
            }
        }
    }
}
