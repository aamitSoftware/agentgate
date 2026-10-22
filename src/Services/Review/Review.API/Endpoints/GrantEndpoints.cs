namespace Review.API.Endpoints;

public static class GetGrantsEndpoint
{
    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapGet("/grants", async (GrantStatus? status, Guid? agentId, Guid? serverId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetGrantsQuery(status, agentId, serverId), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetGrants")
            .WithTags("Grants");

        app.MapGet("/grants/{grantId:guid}", async (Guid grantId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetGrantQuery(grantId), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetGrant")
            .WithTags("Grants");
    }
}

public static class RequestGrantEndpoint
{
    public static void Map(this IEndpointRouteBuilder app) =>
        app.MapPost("/grants", async (RequestGrantCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var response = await sender.Send(command, cancellationToken);
                return TypedResults.Created($"/grants/{response.Id}", response);
            })
            .RequireAuthorization(Policies.RegistryAdmin)
            .WithName("RequestGrant")
            .WithTags("Grants");
}

/// <summary>The reviewer actions on a grant share one shape: <c>POST /grants/{id}:{verb}</c> with an optional comment.</summary>
public static class GrantDecisionEndpoints
{
    public static void Map(this IEndpointRouteBuilder app)
    {
        MapDecision<ApproveGrantCommand>(app, "approve", "ApproveGrant");
        MapDecision<RejectGrantCommand>(app, "reject", "RejectGrant");
        MapDecision<SuspendGrantCommand>(app, "suspend", "SuspendGrant");
        MapDecision<ResumeGrantCommand>(app, "resume", "ResumeGrant");
        MapDecision<RevokeGrantCommand>(app, "revoke", "RevokeGrant");
    }

    private static void MapDecision<TCommand>(IEndpointRouteBuilder app, string verb, string name)
        where TCommand : Review.Application.Features.Grants._Shared.GrantCommand, ICommand, new() =>
        app.MapPost($"/grants/{{grantId:guid}}:{verb}",
                async (Guid grantId, TCommand? command, ISender sender, CancellationToken cancellationToken) =>
                {
                    await sender.Send((command ?? new TCommand()) with { GrantId = grantId }, cancellationToken);
                    return TypedResults.NoContent();
                })
            .RequireAuthorization(Policies.SecurityReviewer)
            .WithName(name)
            .WithTags("Grants");
}

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        GetReviewsEndpoint.Map(app);
        ApproveReviewEndpoint.Map(app);
        RejectReviewEndpoint.Map(app);
        GetGrantsEndpoint.Map(app);
        RequestGrantEndpoint.Map(app);
        GrantDecisionEndpoints.Map(app);
        return app;
    }
}
