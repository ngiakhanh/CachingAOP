using System.Reflection;

namespace CachingAOP;

public abstract class BaseDispatchProxy<T> : DispatchProxy where T : class
{
    protected T _decorated;
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

    public void SetDecorated(T decorated)
    {
        _decorated = decorated;
    }

    protected enum MethodCallType
    {
        Sync,
        AsyncWithoutResult,
        AsyncWithResult
    }
}