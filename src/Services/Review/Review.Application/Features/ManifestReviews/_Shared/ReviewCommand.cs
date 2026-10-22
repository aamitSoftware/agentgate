namespace Review.Application.Features.ManifestReviews._Shared;

/// <summary>Every command on a review is the auditable action that carries it.</summary>
public abstract record ReviewCommand : IAuditableAction<ReviewActionType>
{
    [JsonIgnore] public Guid ReviewId { get; init; }        // bound from the route
    [JsonIgnore] public string ActorId { get; set; } = "";  // stamped by the pipeline, or a system actor
    [JsonIgnore] public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
    public string? Comment { get; init; }
    [JsonIgnore] public abstract ReviewActionType ActionType { get; }
}

public abstract class ReviewCommandValidator<TCommand> : AbstractValidator<TCommand> where TCommand : ReviewCommand
{
    protected ReviewCommandValidator()
    {
        RuleFor(command => command.ReviewId).NotEmpty();
        RuleFor(command => command.Comment).MaximumLength(MaxLength.C512);
    }
}
