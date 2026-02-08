using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace CachingAOP;
public class CacheProxy<T> : BaseDispatchProxy<T> where T : class
{
    private CacheService? _cacheService;

    private readonly ConcurrentDictionary<MethodInfo, CacheMetadata> _metadataCache = new();
    private readonly ConcurrentDictionary<Type, Func<CacheProxy<T>, MethodInfo, object[], string, CacheAttribute, object>> _compiledDelegates = new();

    private string GenerateCacheKey(string name, object?[] args)
    {
        if (args.Length == 0)
            return name;

        var handler = new DefaultInterpolatedStringHandler(
            literalLength: args.Length * 2,
            formattedCount: args.Length + 1);

        handler.AppendFormatted(name);
        foreach (var arg in args)
        {
            handler.AppendLiteral("--");
            handler.AppendFormatted(arg);
        }

        return handler.ToStringAndClear();
    }
        
    protected override object? InvokeInternal(MethodInfo? targetMethod, object?[]? args)
    {
        var metadata = _metadataCache.GetOrAdd(targetMethod, GetMethodMetadata);

        if (metadata.Attribute is not null)
        {
            var cacheKey = GenerateCacheKey(targetMethod.Name, args ?? []);
            _cacheService ??= _serviceProvider.GetRequiredService<CacheService>();

            return metadata.CallType switch
            {
                MethodCallType.AsyncWithResult => InvokeAsyncWithResultDirect(
                    targetMethod, args, cacheKey, metadata.Attribute, metadata.ResultTypeOfTask!),
                MethodCallType.AsyncWithoutResult => InvokeAsyncWithoutResult(
                    targetMethod, args, cacheKey, metadata.Attribute),
                _ => InvokeSync(targetMethod, args, cacheKey, metadata.Attribute)
            };
        }

        return targetMethod.Invoke(_proxied, args);
    }

    private CacheMetadata GetMethodMetadata(MethodInfo targetMethod)
    {
        var implementationType = _proxied.GetType();
        var implementationMethod = implementationType.GetMethod(
            targetMethod.Name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            targetMethod.GetParameters().Select(p => p.ParameterType).ToArray(),
            null);
        var cacheAttribute = 
            implementationMethod?
                .GetCustomAttributes(typeof(CacheAttribute), false)
                .FirstOrDefault() as CacheAttribute ?? 
            targetMethod
                .GetCustomAttributes(typeof(CacheAttribute), false)
                .FirstOrDefault() as CacheAttribute;
        if (cacheAttribute is null)
        {
            return new CacheMetadata(null, null, null);
        }

        var returnType = targetMethod.ReturnType;
        var isTaskWithResult = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>);
        var isTaskWithoutResult = returnType == typeof(Task);

        var callType = isTaskWithResult 
            ? MethodCallType.AsyncWithResult 
            : isTaskWithoutResult 
                ? MethodCallType.AsyncWithoutResult 
                : MethodCallType.Sync;

        var resultTypeOfTask = isTaskWithResult ? returnType.GetGenericArguments()[0] : null;

        return new CacheMetadata(cacheAttribute, callType, resultTypeOfTask);
    }

    private object InvokeSync(
        MethodInfo targetMethod, 
        object?[]? args, 
        string cacheKey, 
        CacheAttribute attribute)
    {
        var isVoid = targetMethod.ReturnType == typeof(void);

        object? result;
        if (attribute.Revoke)
        {
            _cacheService.RemoveAsync().GetAwaiter().GetResult();
        }

        if (!isVoid)
        {
            var cacheResult = _cacheService.GetAsync(cacheKey).GetAwaiter().GetResult();
            if (cacheResult.Item1)
            {
                result = cacheResult.Item2;
            }
            else
            {
                result = targetMethod.Invoke(_proxied, args);
                _cacheService.SetAsync(cacheKey, result).GetAwaiter().GetResult();
            }
        }
        else
        {
            result = targetMethod.Invoke(_proxied, args);
        }

        return result;
    }

    private async Task InvokeAsyncWithoutResult(
        MethodInfo targetMethod,
        object?[]? args,
        string cacheKey,
        CacheAttribute attribute)
    {
        if (attribute.Revoke)
        {
            await _cacheService.RemoveAsync();
        }

        try
        {
            await (Task) targetMethod.Invoke(_proxied, args)!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            throw;
        }
    }

    private object InvokeAsyncWithResultDirect(
        MethodInfo targetMethod, 
        object?[]? args, 
        string cacheKey,
        CacheAttribute attribute, 
        Type resultType)
    {
        var compiledDelegate = _compiledDelegates.GetOrAdd(resultType, _ =>
        {
            var methodInfo = typeof(CacheProxy<T>)
                .GetMethod(nameof(InvokeAsyncWithResult), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

            var instanceParam = Expression.Parameter(typeof(CacheProxy<T>), "instance");
            var targetMethodParam = Expression.Parameter(typeof(MethodInfo), "targetMethod");
            var argsParam = Expression.Parameter(typeof(object[]), "args");
            var cacheKeyParam = Expression.Parameter(typeof(string), "cacheKey");
            var cacheAttributeParam = Expression.Parameter(typeof(CacheAttribute), "cacheAttribute");

            var callExpr = Expression.Call(
                instanceParam,
                methodInfo,
                targetMethodParam,
                argsParam,
                cacheKeyParam,
                cacheAttributeParam);

            var lambda = Expression.Lambda<
                Func<CacheProxy<T>, MethodInfo, object[], string, CacheAttribute, object>>(
                callExpr, 
                instanceParam, 
                targetMethodParam, 
                argsParam, 
                cacheKeyParam, 
                cacheAttributeParam);
            return lambda.Compile();
        });

        return compiledDelegate(this, targetMethod, args ?? [], cacheKey, attribute);
    }

    private async Task<TResult> InvokeAsyncWithResult<TResult>(
        MethodInfo targetMethod,
        object?[]? args,
        string cacheKey,
        CacheAttribute attribute)
    {
        TResult result;
        if (attribute.Revoke)
        {
            await _cacheService.RemoveAsync();
        }
        var cacheResult = await _cacheService.GetAsync(cacheKey);
        if (cacheResult.Item1)
        {
            result = (TResult)cacheResult.Item2;
        }
        else
        {
            try
            {
                result = await (Task<TResult>) targetMethod.Invoke(_proxied, args)!;
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
                throw;
            }

            await _cacheService.SetAsync(cacheKey, result);
        }
        return result;
    }
}
