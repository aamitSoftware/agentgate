namespace Review.Application.Features.Grants._Shared;

/// <summary>Every command on a grant is the auditable action that carries it.</summary>
public abstract record GrantCommand : IAuditableAction<GrantActionType>
{
    [JsonIgnore] public Guid GrantId { get; init; }         // bound from the route
    [JsonIgnore] public string ActorId { get; set; } = "";  // stamped by the pipeline, or a system actor
    [JsonIgnore] public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public string? Comment { get; init; }
    [JsonIgnore] public abstract GrantActionType ActionType { get; }
}

public abstract class GrantCommandValidator<TCommand> : AbstractValidator<TCommand> where TCommand : GrantCommand
{
    protected GrantCommandValidator()
    {
        RuleFor(command => command.GrantId).NotEmpty();
        RuleFor(command => command.Comment).MaximumLength(MaxLength.C512);
    }
}
