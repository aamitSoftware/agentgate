using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.Mediator;

public static class DependencyInjection
{
    /// <summary>
    /// Registration order is pipeline order (first = outermost): the actor is stamped before validation runs.
    /// Call next to the service's own <c>AddMediator(o => o.ServiceLifetime = ServiceLifetime.Scoped)</c> —
    /// handlers depend on the scoped DbContext, so Mediator's singleton default doesn't fit.
    /// </summary>
    public static IServiceCollection AddPipelineBehaviors(this IServiceCollection services)
    {
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AssignActorBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        return services;
    }
}
