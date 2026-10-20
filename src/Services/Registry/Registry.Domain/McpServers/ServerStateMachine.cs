namespace Registry.Domain.McpServers;

/// <summary>One legal move: a row of the seeded transition table.</summary>
public sealed class ServerStatusTransition
{
    public ServerStatus CurrentStatus { get; init; }
    public ServerActionType ActionType { get; init; }
    public ServerStatus DestinationStatus { get; init; }
}

public interface IServerStateMachine
{
    bool CanFire(ServerActionType actionType);
}

// insight - the domain sees only this delegate; Stateless and the seeded table stay in outer layers
public delegate IServerStateMachine ServerStateMachineFactory(ServerStatus status);

public static class ServerStateMachineFactoryExtensions
{
    public static void ValidateTransition(this ServerStateMachineFactory factory, ServerStatus status, ServerActionType actionType)
    {
        if (!factory(status).CanFire(actionType))
            throw new DomainException($"Action {actionType} is not allowed while the server is {status}.");
    }
}
