namespace Registry.Application.Features.McpServers._Shared;

/// <summary>Every command on a server is the auditable action that carries it.</summary>
public abstract record ServerCommand : IAuditableAction<ServerActionType>
{
    [JsonIgnore] public Guid ServerId { get; init; }        // bound from the route
    [JsonIgnore] public string ActorId { get; set; } = "";  // stamped by the pipeline, or a system actor
    [JsonIgnore] public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string? Comment { get; init; }
    public abstract ServerActionType ActionType { get; }
}

public abstract class ServerCommandValidator<TCommand> : AbstractValidator<TCommand> where TCommand : ServerCommand
{
    protected ServerCommandValidator()
    {
        RuleFor(command => command.ServerId).NotEmpty();
        RuleFor(command => command.Comment).MaximumLength(MaxLength.C512);
    }
}
