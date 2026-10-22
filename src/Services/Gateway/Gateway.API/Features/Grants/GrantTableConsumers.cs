using AgentGate.IntegrationEvents.Review;
using Gateway.API.Features.Anomalies;
using MassTransit;

namespace Gateway.API.Features.Grants;

public sealed class GrantActivatedConsumer(GrantTable grants, DenialSpikeDetector anomalies, ILogger<GrantActivatedConsumer> logger)
    : IConsumer<GrantActivated>
{
    public Task Consume(ConsumeContext<GrantActivated> context)
    {
        var grant = context.Message;
        grants.Activate(grant);
        anomalies.Lift(grant.AgentClientId, grant.OccurredOn); // a resume after an anomaly: a reviewer has looked
        logger.LogInformation("Grant {GrantId} in force: {AgentClientId} may call {Tools} on {ServerName} until {ExpiresAt}",
            grant.GrantId, grant.AgentClientId, string.Join(", ", grant.Tools), grant.ServerName, grant.ExpiresAt?.ToString("O") ?? "revoked");
        return Task.CompletedTask;
    }
}

public sealed class GrantSuspendedConsumer(GrantTable grants, ILogger<GrantSuspendedConsumer> logger) : IConsumer<GrantSuspended>
{
    public Task Consume(ConsumeContext<GrantSuspended> context)
    {
        grants.Suspend(context.Message);
        logger.LogWarning("Grant {GrantId} suspended: {Reason}", context.Message.GrantId, context.Message.Reason);
        return Task.CompletedTask;
    }
}

public sealed class GrantRevokedConsumer(GrantTable grants, ILogger<GrantRevokedConsumer> logger) : IConsumer<GrantRevoked>
{
    public Task Consume(ConsumeContext<GrantRevoked> context)
    {
        grants.Revoke(context.Message);
        logger.LogWarning("Grant {GrantId} revoked: {Reason}", context.Message.GrantId, context.Message.Reason);
        return Task.CompletedTask;
    }
}
