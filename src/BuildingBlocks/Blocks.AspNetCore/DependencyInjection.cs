using Blocks.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.AspNetCore;

public static class DependencyInjection
{
    public static IServiceCollection AddApiDefaults(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IClaimsProvider, HttpContextClaimsProvider>();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }

    /// <summary>Call first in the pipeline so every later middleware and endpoint is covered.</summary>
    public static WebApplication UseApiDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
