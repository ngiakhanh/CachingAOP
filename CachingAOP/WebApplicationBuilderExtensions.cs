namespace CachingAOP;

public static class WebApplicationBuilderExtensions
{
    public static WebApplication BuildWithCache(this WebApplicationBuilder webApplicationBuilder)
    {
        webApplicationBuilder.Services.AddCaching();
        return webApplicationBuilder.Build();
    }

    public static WebApplication BuildWithProxyCache(this WebApplicationBuilder webApplicationBuilder)
    {
        webApplicationBuilder.Services.AddProxyCacheFromAssemblies<CacheAttribute, CacheProxy>();
        //or
        //webApplicationBuilder.Services.AddCacheFromAssemblies(typeof(CacheAttribute), typeof(CacheProxy));
        return webApplicationBuilder.Build();
    }
}