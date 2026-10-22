using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hub.Persistence;

public static class DependencyInjection
{
    public const string ConnectionName = "hubdb";

    public static IHostApplicationBuilder AddPersistenceServices(this IHostApplicationBuilder builder)
    {
        // no domain-event interceptor: the Hub raises no domain events, it only projects other services' events
        builder.Services.AddDbContext<HubDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString(ConnectionName)));
        builder.EnrichNpgsqlDbContext<HubDbContext>(); // Aspire: tracing, health check, retries
        return builder;
    }
}

/// <summary>Lets <c>dotnet ef</c> build the model without starting the API host.</summary>
public sealed class HubDesignTimeDbContextFactory : IDesignTimeDbContextFactory<HubDbContext>
{
    public HubDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<HubDbContext>().UseNpgsql("Host=localhost;Database=hubdb").Options);
}
