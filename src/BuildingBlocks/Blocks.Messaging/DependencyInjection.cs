using System.Reflection;
using Blocks.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Blocks.Messaging;

public static class DependencyInjection
{
    private const string ConnectionName = "rabbitmq";

    /// <summary>
    /// Bus with the transactional outbox and inbox on <typeparamref name="TDbContext"/>, for every service that owns state.
    /// Outbox: IPublishEndpoint writes rows to the DbContext; they commit with the caller's SaveChanges, then get delivered.
    /// Inbox: each consumed MessageId is recorded in the same transaction as the consumer's changes, so redelivery is a no-op.
    /// The DbContext must map the outbox tables: modelBuilder.AddInboxStateEntity/AddOutboxMessageEntity/AddOutboxStateEntity.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging<TDbContext>(this IHostApplicationBuilder builder, string serviceName, Assembly consumersAssembly)
        where TDbContext : DbContext
        => builder.AddMessaging(serviceName, consumersAssembly, bus =>
        {
            bus.AddEntityFrameworkOutbox<TDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            bus.AddConfigureEndpointsCallback((context, _, endpoint) => endpoint.UseEntityFrameworkOutbox<TDbContext>(context));
        });

    /// <summary>Bus without an outbox, for a service with no transactional state of its own (e.g. the Gateway).</summary>
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder, string serviceName, Assembly consumersAssembly,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{ConnectionName}' is missing.");

        builder.Services.AddMassTransit(bus =>
        {
            // one queue per (service, consumer): an event fans out to every interested service
            bus.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(serviceName, includeNamespace: false));
            bus.AddConsumers(consumersAssembly);
            configure?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(connectionString), _ => { });
                // ponytail: immediate retries only; delayed redelivery needs the RabbitMQ delayed-exchange plugin
                rabbit.UseMessageRetry(retry =>
                {
                    retry.Intervals(100, 500, 1000, 5000);
                    retry.Ignore<DomainException>(); // a broken business rule won't fix itself on retry
                });
                rabbit.ConfigureEndpoints(context);
            });
        });

        return builder;
    }
}
