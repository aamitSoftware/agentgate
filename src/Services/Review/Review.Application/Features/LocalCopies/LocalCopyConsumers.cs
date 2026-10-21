using AgentGate.IntegrationEvents.Registry;

namespace Review.Application.Features.LocalCopies;

/// <summary>Keeps Review's copy of Registry's agents, so grant requests can be checked without calling Registry.</summary>
public sealed class AgentRegisteredConsumer(ReviewDbContext db) : IConsumer<AgentRegistered>
{
    public async Task Consume(ConsumeContext<AgentRegistered> context)
    {
        var message = context.Message;
        if (await db.KnownAgents.AnyAsync(agent => agent.Id == message.AgentId, context.CancellationToken))
            return; // agents are immutable in Registry: a first-seen fact, nothing to update

        db.KnownAgents.Add(KnownAgent.Create(message.AgentId, message.Name, message.ClientId, message.Owner, message.Purpose));
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
