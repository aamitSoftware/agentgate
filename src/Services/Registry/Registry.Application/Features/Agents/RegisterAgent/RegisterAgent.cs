using Contracts = AgentGate.IntegrationEvents.Registry;

namespace Registry.Application.Features.Agents.RegisterAgent;

public sealed record RegisterAgentCommand : ICommand<RegisterAgentResponse>, IAuditableAction<AgentActionType>
{
    public required string Name { get; init; }
    public required string ClientId { get; init; }
    public required string Owner { get; init; }
    public required string Purpose { get; init; }
    public string? Comment { get; init; }

    [JsonIgnore] public string ActorId { get; set; } = "";
    [JsonIgnore] public DateTime OccurredOn { get; } = DateTime.UtcNow;
    [JsonIgnore] public AgentActionType ActionType => AgentActionType.Register;
}

public sealed record RegisterAgentResponse(Guid Id);

public sealed class RegisterAgentValidator : AbstractValidator<RegisterAgentCommand>
{
    public RegisterAgentValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(MaxLength.C128);
        RuleFor(command => command.ClientId).NotEmpty().MaximumLength(MaxLength.C64);
        RuleFor(command => command.Owner).NotEmpty().MaximumLength(MaxLength.C128);
        RuleFor(command => command.Purpose).NotEmpty().MaximumLength(MaxLength.C512);
        RuleFor(command => command.Comment).MaximumLength(MaxLength.C512);
    }
}

public sealed class RegisterAgentHandler(AgentRepository agents) : ICommandHandler<RegisterAgentCommand, RegisterAgentResponse>
{
    public async ValueTask<RegisterAgentResponse> Handle(RegisterAgentCommand command, CancellationToken cancellationToken)
    {
        if (await agents.ClientIdExistsAsync(command.ClientId, cancellationToken))
            throw new ConflictException($"An agent with client id '{command.ClientId}' is already registered.");

        var agent = Agent.Register(command.Name, command.ClientId, command.Owner, command.Purpose, command);
        await agents.AddAsync(agent, cancellationToken);
        await agents.SaveChangesAsync(cancellationToken);
        return new RegisterAgentResponse(agent.Id);
    }
}

public sealed class PublishIntegrationEventOnAgentRegisteredHandler(IPublishEndpoint publishEndpoint)
    : IDomainEventHandler<AgentRegistered>
{
    public Task HandleAsync(AgentRegistered domainEvent, CancellationToken ct)
    {
        var agent = domainEvent.Agent;
        return publishEndpoint.Publish(
            new Contracts.AgentRegistered(agent.Id, agent.Name, agent.ClientId, agent.Owner, agent.Purpose, domainEvent.Action.OccurredOn),
            ct);
    }
}
