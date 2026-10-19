using System.Reflection;
using Blocks.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.EntityFrameworkCore;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dispatch interceptor and every <see cref="IDomainEventHandler{TEvent}"/> in <paramref name="handlersAssembly"/>.
    /// Wire the interceptor into the DbContext: <c>options.AddInterceptors(sp.GetRequiredService&lt;DispatchDomainEventsInterceptor&gt;())</c>.
    /// </summary>
    public static IServiceCollection AddDomainEventDispatch(this IServiceCollection services, Assembly handlersAssembly)
    {
        services.AddScoped<DispatchDomainEventsInterceptor>();

        var handlerRegistrations = handlersAssembly.GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
                .Select(contract => (contract, type)));

        foreach (var (contract, implementation) in handlerRegistrations)
            services.AddScoped(contract, implementation);

        return services;
    }
}
