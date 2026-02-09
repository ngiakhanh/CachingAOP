using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace CachingAOP;

public abstract class BaseDispatchProxy<T> : DispatchProxy where T : Attribute
{
    protected object _proxied;
    protected IServiceProvider _serviceProvider;
    
    protected readonly ConcurrentDictionary<Type, Func<BaseDispatchProxy<T>, MethodInfo, T, object[], object>> _compiledDelegates = new();
    protected readonly ConcurrentDictionary<MethodInfo, CacheMetadata> _metadataCache = new();

    protected readonly struct CacheMetadata
    {
        public readonly T? Attribute;
        public readonly MethodCallType? CallType;
        public readonly Type? ResultTypeOfTask;
        public CacheMetadata(T? attribute, MethodCallType? callType, Type? resultTypeOfTask)
        {
            Attribute = attribute;
            CallType = callType;
            ResultTypeOfTask = resultTypeOfTask;
        }
    }

    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void SetProxied(object proxied)
    {
        _proxied = proxied;
    }

    protected enum MethodCallType
    {
        Sync,
        AsyncWithoutResult,
        AsyncWithResult
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        try
        {
            var cacheMetadata = _metadataCache.GetOrAdd(targetMethod, GetMethodMetadata);

            return cacheMetadata.Attribute is not null 
                ? InvokeInternal(targetMethod, cacheMetadata, args) 
                : targetMethod.Invoke(_proxied, args);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            throw;
        }
    }

    private object? InvokeInternal(MethodInfo? targetMethod, CacheMetadata cacheMetadata, object?[]? args)
    {
        return cacheMetadata.CallType switch
        {
            MethodCallType.AsyncWithResult => InvokeAsyncWithResultDirect(
                targetMethod, cacheMetadata.Attribute, cacheMetadata.ResultTypeOfTask!, args),
            MethodCallType.AsyncWithoutResult => InvokeAsyncWithoutResult(
                targetMethod, cacheMetadata.Attribute, args),
            _ => InvokeSync(targetMethod, cacheMetadata.Attribute, args)
        };
    }

    private object InvokeSync(
        MethodInfo targetMethod, 
        T attribute,
        object?[]? args)
    {
        return InvokeSyncInternal(targetMethod, attribute, args);
    }

    protected virtual object InvokeSyncInternal(
        MethodInfo targetMethod,
        T attribute,
        object?[]? args)
    {
        return targetMethod.Invoke(_proxied, args);
    }

    private async Task InvokeAsyncWithoutResult(
        MethodInfo targetMethod,
        T attribute,
        object?[]? args)
    {
        try
        {
            await InvokeAsyncWithoutResultInternal(targetMethod, attribute, args)!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            throw;
        }
    }

    protected virtual async Task InvokeAsyncWithoutResultInternal(
        MethodInfo targetMethod,
        T attribute,
        object?[]? args)
    {
        await (Task) targetMethod.Invoke(_proxied, args)!;
    }

    private object InvokeAsyncWithResultDirect(
        MethodInfo targetMethod, 
        T attribute, 
        Type resultType,
        object?[]? args)
    {
        var compiledDelegate = _compiledDelegates.GetOrAdd(resultType, _ =>
        {
            var methodInfo = typeof(BaseDispatchProxy<T>)
                .GetMethod(nameof(InvokeAsyncWithResult), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(resultType);

            var instanceParam = Expression.Parameter(typeof(BaseDispatchProxy<T>), "instance");
            var targetMethodParam = Expression.Parameter(typeof(MethodInfo), "targetMethod");
            var attributeParam = Expression.Parameter(typeof(T), "attribute");
            var argsParam = Expression.Parameter(typeof(object[]), "args");

            var callExpr = Expression.Call(
                instanceParam,
                methodInfo,
                targetMethodParam,
                attributeParam,
                argsParam);

            var lambda = Expression.Lambda<
                Func<BaseDispatchProxy<T>, MethodInfo, T, object[], object>>(
                callExpr, 
                instanceParam,
                targetMethodParam,
                attributeParam,
                argsParam);
            return lambda.Compile();
        });

        return compiledDelegate(this, targetMethod, attribute, args ?? []);
    }

    private async Task<TResult> InvokeAsyncWithResult<TResult>(
        MethodInfo targetMethod,
        T attribute,
        object?[]? args)
    {
        TResult result;
        try
        {
            result = await InvokeAsyncWithResultInternal<TResult>(targetMethod, attribute, args);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            throw;
        }
        return result;
    }

    protected virtual async Task<TResult> InvokeAsyncWithResultInternal<TResult>(
        MethodInfo targetMethod,
        T attribute,
        object?[]? args)
    {
        return await (Task<TResult>) targetMethod.Invoke(_proxied, args)!;
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
                .GetCustomAttributes(typeof(T), false)
                .FirstOrDefault() as T ?? 
            targetMethod
                .GetCustomAttributes(typeof(T), false)
                .FirstOrDefault() as T;
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
}