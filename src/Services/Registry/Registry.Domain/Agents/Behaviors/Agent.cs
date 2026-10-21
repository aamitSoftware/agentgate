using System.Text.RegularExpressions;

namespace Registry.Domain.Agents;

public partial class Agent
{
    public static Agent Register(string name, string clientId, string owner, string purpose, IAuditableAction<AgentActionType> action)
    {
        if (!ClientIdPattern().IsMatch(clientId))
            throw new DomainException($"'{clientId}' is not a valid client id: use 2-64 letters, digits, '-', '_' or '.'.");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(purpose))
            throw new DomainException("An agent needs a name, an owner and a purpose.");

        var agent = new Agent
        {
            Name = name,
            ClientId = clientId,
            Owner = owner,
            Purpose = purpose,
            CreatedBy = action.ActorId,
            CreatedOn = action.OccurredOn
        };
        agent.AddDomainEvent(new AgentRegistered(agent, action));
        return agent;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{1,63}$")]
    private static partial Regex ClientIdPattern();
}
