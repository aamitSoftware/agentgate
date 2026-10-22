using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Review.Persistence.Repositories;

namespace Review.Persistence;

public static class DependencyInjection
{
    public const string ConnectionName = "reviewdb";

    public static IHostApplicationBuilder AddPersistenceServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<ReviewDbContext>((serviceProvider, options) => options
            .UseNpgsql(builder.Configuration.GetConnectionString(ConnectionName))
            .AddInterceptors(serviceProvider.GetRequiredService<DispatchDomainEventsInterceptor>()));
        builder.EnrichNpgsqlDbContext<ReviewDbContext>(); // Aspire: tracing, health check, retries

        builder.Services.AddScoped<ManifestReviewRepository>();
        builder.Services.AddScoped<GrantRepository>();
        return builder;
    }
}

/// <summary>
/// Lets <c>dotnet ef</c> build the model without starting the API host (which needs RabbitMQ and Keycloak).
/// Adding a migration never connects, so the connection string is a placeholder.
/// </summary>
public sealed class ReviewDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReviewDbContext>
{
    public ReviewDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ReviewDbContext>().UseNpgsql("Host=localhost;Database=reviewdb").Options);
}
