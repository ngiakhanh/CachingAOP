using System.Reflection;
using System.Runtime.ExceptionServices;

namespace CachingAOP;

public abstract class BaseDispatchProxy<T> : DispatchProxy where T : class
{
    protected T _proxied;
    protected IServiceProvider _serviceProvider;

    protected readonly struct CacheMetadata
    {
        public readonly CacheAttribute? Attribute;
        public readonly MethodCallType? CallType;
        public readonly Type? ResultTypeOfTask;
        public CacheMetadata(CacheAttribute? attribute, MethodCallType? callType, Type? resultTypeOfTask)
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

    public void SetProxied(T proxied)
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
            return InvokeInternal(targetMethod, args);
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException ?? ex).Throw();
            throw;
        }
    }

    protected abstract object? InvokeInternal(MethodInfo? targetMethod, object?[]? args);
}