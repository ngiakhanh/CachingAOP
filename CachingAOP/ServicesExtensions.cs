using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace CachingAOP;

public static class ServicesExtensions
{
    public static void AddCaching(this IServiceCollection services)
    {
        services.AddSingleton<CacheService>();
        services.Scan(x =>
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var referencedAssemblies = entryAssembly.GetReferencedAssemblies().Select(Assembly.Load);
            var assemblies = new List<Assembly> { entryAssembly }.Concat(referencedAssemblies);

            x.FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo(typeof(IAsyncInterceptor)))
                .AsImplementedInterfaces()
                .WithSingletonLifetime();
        });
        var replacedRegistrations = new List<(ServiceDescriptor?, Type, Type)>();

        foreach (var svc in services)
        {
            if (svc.ImplementationType != null)
            {
                if (svc.ServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(
                        m => m.GetCustomAttributes(typeof(CacheAttribute), false).FirstOrDefault() != null) ||
                    svc.ImplementationType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(
                        m => m.GetCustomAttributes(typeof(CacheAttribute), false).FirstOrDefault() != null))
                {
                    var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ImplementationType == svc.ImplementationType && descriptor.ServiceType == svc.ServiceType);
                    replacedRegistrations.Add((serviceDescriptor, svc.ServiceType, svc.ImplementationType));
                }
            }
        }

        foreach (var registration in replacedRegistrations)
        {
            if (registration.Item2 != null && registration.Item2.Name != registration.Item3.Name)
            {
                // Check service lifetime as well
                // Add support for delegate scenario
                services.AddProxiedScope(registration.Item2, registration.Item3);
            }
            else
            {
                services.AddProxiedScope(registration.Item3);
            }
        }
    }

    public static void AddProxiedScope<TInterface, TImplementation>
        (this IServiceCollection services)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        services.TryAddSingleton<ProxyGenerator>();
        // This registers the underlying class
        services.AddScoped<TImplementation>();
        services.AddScoped(typeof(TInterface), serviceProvider =>
        {
            // Get an instance of the Castle Proxy Generator
            var proxyGenerator = serviceProvider
                .GetRequiredService<ProxyGenerator>();
            // Have DI build out an instance of the class that has methods
            // you want to cache (this is a normal instance of that class 
            // without caching added)
            var actual = serviceProvider
                .GetRequiredService<TImplementation>();
            // Find all of the interceptors that have been registered, 
            // including our caching interceptor.  (you might later add a 
            // logging interceptor, etc.)
            var interceptors = serviceProvider
                .GetServices<IAsyncInterceptor>().ToArray();
            // Have Castle Proxy build out a proxy object that implements 
            // your interface, but adds a caching layer on top of the
            // actual implementation of the class.  This proxy object is
            // what will then get injected into the class that has a 
            // dependency on TInterface
            return proxyGenerator.CreateInterfaceProxyWithTarget(
                typeof(TInterface), actual, interceptors);
        });
    }

    public static void AddProxiedScope
        (this IServiceCollection services, Type tInterface, Type implementation)
    {
        services.TryAddSingleton<ProxyGenerator>();
        // This registers the underlying class
        services.AddScoped(implementation);
        services.AddScoped(tInterface, serviceProvider =>
        {
            // Get an instance of the Castle Proxy Generator
            var proxyGenerator = serviceProvider
                .GetRequiredService<ProxyGenerator>();
            // Have DI build out an instance of the class that has methods
            // you want to cache (this is a normal instance of that class 
            // without caching added)
            var actual = serviceProvider
                .GetRequiredService(implementation);
            // Find all of the interceptors that have been registered, 
            // including our caching interceptor.  (you might later add a 
            // logging interceptor, etc.)
            var interceptors = serviceProvider
                .GetServices<IAsyncInterceptor>().ToArray();
            // Have Castle Proxy build out a proxy object that implements 
            // your interface, but adds a caching layer on top of the
            // actual implementation of the class.  This proxy object is
            // what will then get injected into the class that has a 
            // dependency on TInterface
            return proxyGenerator.CreateInterfaceProxyWithTarget(
                tInterface, actual, interceptors);
        });
    }

    public static void AddProxiedScope<TImplementation>
        (this IServiceCollection services)
        where TImplementation : class
    {
        services.TryAddSingleton<ProxyGenerator>();
        // This registers the underlying class
        services.AddScoped(typeof(TImplementation), serviceProvider =>
        {
            // Get an instance of the Castle Proxy Generator
            var proxyGenerator = serviceProvider
                .GetRequiredService<ProxyGenerator>();
            // Find all of the interceptors that have been registered, 
            // including our caching interceptor.  (you might later add a 
            // logging interceptor, etc.)
            var interceptors = serviceProvider
                .GetServices<IAsyncInterceptor>().ToArray();
            // Have Castle Proxy build out a proxy object that implements 
            // your interface, but adds a caching layer on top of the
            // actual implementation of the class.  This proxy object is
            // what will then get injected into the class that has a 
            // dependency on TInterface
            return proxyGenerator.CreateClassProxy<TImplementation>(interceptors);
        });
    }

    public static void AddProxiedScope
        (this IServiceCollection services, Type implementation)
    {
        services.TryAddSingleton<ProxyGenerator>();
        // This registers the underlying class
        services.AddScoped(implementation, serviceProvider =>
        {
            // Get an instance of the Castle Proxy Generator
            var proxyGenerator = serviceProvider
                .GetRequiredService<ProxyGenerator>();
            // Find all of the interceptors that have been registered, 
            // including our caching interceptor.  (you might later add a 
            // logging interceptor, etc.)
            var interceptors = serviceProvider
                .GetServices<IAsyncInterceptor>().ToArray();
            // Have Castle Proxy build out a proxy object that implements 
            // your interface, but adds a caching layer on top of the
            // actual implementation of the class.  This proxy object is
            // what will then get injected into the class that has a 
            // dependency on TInterface
            return proxyGenerator.CreateClassProxy(implementation, null, ProxyGenerationOptions.Default, null, interceptors);
        });
    }

    public static void AddProxyCacheFromAssemblies<TAttribute, TProxy>(this IServiceCollection serviceCollection)
        where TAttribute : Attribute
        where TProxy : BaseDispatchProxy<TAttribute>
    {
        serviceCollection.AddProxyCacheFromAssemblies(typeof(TAttribute), typeof(TProxy));
    }

    public static void AddProxyCacheFromAssemblies(this IServiceCollection serviceCollection, Type attributeType, Type proxyType)
    {
        serviceCollection.TryAddSingleton<CacheService>();
        serviceCollection.AddProxyFromAssemblies(attributeType, proxyType);
    }

    public static void AddProxyFromAssemblies(this IServiceCollection services, Type attributeType, Type proxyType)
    {
        services.Scan(x =>
        {
            var entryAssembly = Assembly.GetEntryAssembly();
            var referencedAssemblies = entryAssembly.GetReferencedAssemblies().Select(Assembly.Load);
            var assemblies = new List<Assembly> { entryAssembly }.Concat(referencedAssemblies);

            x.FromAssemblies(assemblies)
                .AddClasses(classes => classes.AssignableTo(proxyType))
                .AsSelf()
                .WithSingletonLifetime();
        });
        var replacedRegistrations = new List<(ServiceDescriptor?, Type, Type)>();

        foreach (var svc in services)
        {
            if (svc.ImplementationType != null)
            {
                if (svc.ServiceType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(
                        m => m.GetCustomAttributes(attributeType, false).FirstOrDefault() != null) ||
                    svc.ImplementationType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Any(
                        m => m.GetCustomAttributes(attributeType, false).FirstOrDefault() != null))
                {
                    var serviceDescriptor = services.FirstOrDefault(descriptor => 
                        descriptor.ImplementationType == svc.ImplementationType && 
                        descriptor.ServiceType == svc.ServiceType);
                    replacedRegistrations.Add((serviceDescriptor, svc.ServiceType, svc.ImplementationType));
                }
            }
        }

        foreach (var registration in replacedRegistrations)
        {
            if (registration.Item2 != null && registration.Item2.Name != registration.Item3.Name)
            {
                // Check service lifetime as well
                // Add support for delegate scenario
                services.AddProxiedScope(registration.Item2, registration.Item3, proxyType);
            }
            else
            {
                //DispatchProxy does not support class proxying
            }
        }
    }

    public static IServiceCollection AddProxiedScope<TAttribute, TImplementation, TProxy>(
        this IServiceCollection serviceCollection)
        where TAttribute : Attribute
        where TImplementation : class, TAttribute
        where TProxy : BaseDispatchProxy<TAttribute>
    {
        return serviceCollection.AddProxiedScope(typeof(TAttribute), typeof(TImplementation), typeof(TProxy));
    }

    public static IServiceCollection AddProxiedScope
        (this IServiceCollection services, Type attribute, Type implementation, Type proxyType)
    {
        services.AddScoped(implementation);
        // This registers the underlying class
        services.AddScoped(attribute, serviceProvider =>
        {
            // if proxy type is CacheProxy<T> and interface is IWeatherForecastService
            // then make closed type CacheProxy<IWeatherForecastService>
            var closedProxyType = proxyType.IsGenericTypeDefinition
                ? proxyType.MakeGenericType(attribute)
                : proxyType;
            var proxy = DispatchProxy.Create(attribute, closedProxyType);
            var actual = serviceProvider
                .GetRequiredService(implementation);

            var setDecoratedMethod = closedProxyType.GetMethod(
                nameof(BaseDispatchProxy<>.SetProxied), 
                BindingFlags.Instance | BindingFlags.Public);
            setDecoratedMethod?.Invoke(proxy, [actual]);

            var setServiceProviderMethod = closedProxyType.GetMethod(
                nameof(BaseDispatchProxy<>.SetServiceProvider), 
                BindingFlags.Instance | BindingFlags.Public);
            setServiceProviderMethod?.Invoke(proxy, [serviceProvider]);

            return proxy;
        });
        return services;
    }
}