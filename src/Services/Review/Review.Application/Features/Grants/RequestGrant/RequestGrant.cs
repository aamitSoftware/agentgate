namespace Review.Application.Features.Grants.RequestGrant;

public sealed record RequestGrantCommand : GrantCommand, ICommand<RequestGrantResponse>
{
    public required Guid AgentId { get; init; }
    public required Guid ServerId { get; init; }
    public required IReadOnlyList<string> Tools { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public required string Justification { get; init; }
    public override GrantActionType ActionType => GrantActionType.Request;
}

public sealed record RequestGrantResponse(Guid Id);

public sealed class RequestGrantValidator : AbstractValidator<RequestGrantCommand>
{
    public RequestGrantValidator()
    {
        RuleFor(command => command.AgentId).NotEmpty();
        RuleFor(command => command.ServerId).NotEmpty();
        RuleFor(command => command.Tools).NotEmpty();
        RuleForEach(command => command.Tools).NotEmpty().MaximumLength(MaxLength.C128);
        RuleFor(command => command.Justification).NotEmpty().MaximumLength(MaxLength.C512);
        RuleFor(command => command.Comment).MaximumLength(MaxLength.C512);
    }
}

public sealed class RequestGrantHandler(ReviewDbContext db, GrantRepository grants) : ICommandHandler<RequestGrantCommand, RequestGrantResponse>
{
    public async ValueTask<RequestGrantResponse> Handle(RequestGrantCommand command, CancellationToken cancellationToken)
    {
        // local copies only: an agent or server Review hasn't heard of yet can't be granted (Registry's events are in flight)
        var agent = (await db.KnownAgents.FindAsync([command.AgentId], cancellationToken))
            .OrThrowNotFound($"Agent {command.AgentId} is not registered (or not yet known to Review).");
        var server = (await db.KnownServers.FindAsync([command.ServerId], cancellationToken))
            .OrThrowNotFound($"Server {command.ServerId} is not registered (or not yet known to Review).");

        var grant = Grant.Request(agent, server, command.Tools, command.ExpiresAt, command.Justification, command);
        await grants.AddAsync(grant, cancellationToken);
        await grants.SaveChangesAsync(cancellationToken);
        return new RequestGrantResponse(grant.Id);
    }
}
