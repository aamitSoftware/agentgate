using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Blocks.Core;

public static class ConfigurationExtensions
{
    /// <summary>Binds the section named after <typeparamref name="TOptions"/> and fails at startup, not at first use.</summary>
    public static IServiceCollection AddAndValidateOptions<TOptions>(this IServiceCollection services, IConfiguration configuration)
        where TOptions : class
    {
        var section = configuration.GetSection(typeof(TOptions).Name);
        if (!section.Exists())
            throw new InvalidOperationException($"Configuration section '{section.Key}' is missing.");

        services.AddOptions<TOptions>()
            .Bind(section)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
