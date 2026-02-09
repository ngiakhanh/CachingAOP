using System.Reflection;
using System.Runtime.CompilerServices;

namespace CachingAOP;
public class CacheProxy : BaseDispatchProxy<CacheAttribute>
{
    private CacheService? _cacheService;

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

    protected override object InvokeSyncInternal(
        MethodInfo targetMethod,
        CacheAttribute attribute,
        object?[]? args)
    {
        _cacheService ??= _serviceProvider.GetRequiredService<CacheService>();
        var isVoid = targetMethod.ReturnType == typeof(void);
        object? result;
        if (attribute.Revoke)
        {
            _cacheService.RemoveAsync().GetAwaiter().GetResult();
        }

        if (!isVoid)
        {
            var cacheKey = GenerateCacheKey(targetMethod.Name, args ?? []);
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

    protected override async Task InvokeAsyncWithoutResultInternal(
        MethodInfo targetMethod,
        CacheAttribute attribute,
        object?[]? args)
    {
        _cacheService ??= _serviceProvider.GetRequiredService<CacheService>();
        if (attribute.Revoke)
        {
            await _cacheService.RemoveAsync();
        }

        await (Task) targetMethod.Invoke(_proxied, args)!;
    }

    protected override async Task<TResult> InvokeAsyncWithResultInternal<TResult>(
        MethodInfo targetMethod,
        CacheAttribute attribute,
        object?[]? args)
    {
        TResult result;
        _cacheService ??= _serviceProvider.GetRequiredService<CacheService>();
        if (attribute.Revoke)
        {
            await _cacheService.RemoveAsync();
        }

        var cacheKey = GenerateCacheKey(targetMethod.Name, args ?? []);
        var cacheResult = await _cacheService.GetAsync(cacheKey);
        if (cacheResult.Item1)
        {
            result = (TResult)cacheResult.Item2;
        }
        else
        {
            result = await (Task<TResult>) targetMethod.Invoke(_proxied, args)!;
            await _cacheService.SetAsync(cacheKey, result);
        }
        return result;
    }
}
