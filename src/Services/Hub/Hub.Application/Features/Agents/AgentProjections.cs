using AgentGate.IntegrationEvents.Registry;

namespace Hub.Application.Features.Agents;

public sealed class AgentRegisteredConsumer(HubDbContext db, ActivityFeed feed) : IConsumer<AgentRegistered>
{
    public async Task Consume(ConsumeContext<AgentRegistered> context)
    {
        var message = context.Message;
        db.Agents.Add(new AgentView
        {
            Id = message.AgentId,
            Name = message.Name,
            ClientId = message.ClientId,
            Owner = message.Owner,
            Purpose = message.Purpose,
            RegisteredOn = message.OccurredOn
        });

        var entry = db.AddTimeline(context, message.OccurredOn,
            $"Agent '{message.Name}' ({message.ClientId}) registered, owned by {message.Owner}.", Severities.Info, agentId: message.AgentId);
        await db.SaveChangesAsync(context.CancellationToken);
        feed.Publish(ActivityItem.From(entry));
    }
}
