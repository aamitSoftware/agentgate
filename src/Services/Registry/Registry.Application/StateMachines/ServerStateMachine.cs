using Stateless;

namespace Registry.Application.StateMachines;

public sealed class ServerStateMachine : IServerStateMachine
{
    private readonly StateMachine<ServerStatus, ServerActionType> _machine;

    public ServerStateMachine(ServerStatus status, IReadOnlyList<ServerStatusTransition> transitions)
    {
        _machine = new StateMachine<ServerStatus, ServerActionType>(status);
        foreach (var transition in transitions)
            _machine.Configure(transition.CurrentStatus).Permit(transition.ActionType, transition.DestinationStatus);
    }

    public bool CanFire(ServerActionType actionType) => _machine.CanFire(actionType);
}
