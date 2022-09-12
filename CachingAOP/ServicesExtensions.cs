using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace CachingAOP
{
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
                if (registration.Item1 != null)
                {
                    services.Remove(registration.Item1);
                }

                if (registration.Item2 != null && registration.Item2.Name != registration.Item3.Name)
                {
                    // Check service lifetime as well
                    // Add support for delegate scenario
                    services.AddProxiedScoped(registration.Item2, registration.Item3);
                }
                else
                {
                    services.AddProxiedScoped(registration.Item3);
                }
            }
        }

        public static void AddProxiedScoped<TInterface, TImplementation>
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

        public static void AddProxiedScoped
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

        public static void AddProxiedScoped<TImplementation>
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

        public static void AddProxiedScoped
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
    }
}
