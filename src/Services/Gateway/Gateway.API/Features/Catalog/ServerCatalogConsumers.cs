using AgentGate.IntegrationEvents.Registry;
using MassTransit;

namespace Gateway.API.Features.Catalog;

public sealed class ServerApprovedConsumer(ServerCatalog catalog, ILogger<ServerApprovedConsumer> logger) : IConsumer<ServerApproved>
{
    public Task Consume(ConsumeContext<ServerApproved> context)
    {
        catalog.Pin(context.Message);
        logger.LogInformation("Pinned {ServerName} at manifest {ManifestHash}", context.Message.Name, context.Message.ManifestHash);
        return Task.CompletedTask;
    }
}

public sealed class ServerQuarantinedConsumer(ServerCatalog catalog, ILogger<ServerQuarantinedConsumer> logger) : IConsumer<ServerQuarantined>
{
    public Task Consume(ConsumeContext<ServerQuarantined> context)
    {
        catalog.Quarantine(context.Message);
        logger.LogWarning("Quarantined {ServerName}: {Reason}", context.Message.Name, context.Message.Reason);
        return Task.CompletedTask;
    }
}
