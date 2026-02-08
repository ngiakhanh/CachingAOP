using System.Reflection;

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
}