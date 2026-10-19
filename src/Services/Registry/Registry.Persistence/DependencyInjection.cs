using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Registry.Persistence.Repositories;

namespace Registry.Persistence;

public static class DependencyInjection
{
    public const string ConnectionName = "registrydb";

    public static IHostApplicationBuilder AddPersistenceServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<RegistryDbContext>((serviceProvider, options) => options
            .UseNpgsql(builder.Configuration.GetConnectionString(ConnectionName))
            .AddInterceptors(serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>()));
        builder.EnrichNpgsqlDbContext<RegistryDbContext>(); // Aspire: tracing, health check, retries

        builder.Services.AddScoped<McpServerRepository>();
        return builder;
    }
}

/// <summary>
/// Lets <c>dotnet ef</c> build the model without starting the API host (which needs RabbitMQ and Keycloak).
/// Adding a migration never connects, so the connection string is a placeholder.
/// </summary>
public sealed class RegistryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RegistryDbContext>
{
    public RegistryDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<RegistryDbContext>().UseNpgsql("Host=localhost;Database=registrydb").Options);
}
