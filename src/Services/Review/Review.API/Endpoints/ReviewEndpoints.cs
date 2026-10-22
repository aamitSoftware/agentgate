namespace Review.API.Endpoints;

public static class GetReviewsEndpoint
{
    public static void Map(this IEndpointRouteBuilder app)
    {
        app.MapGet("/reviews", async (ReviewStatus? status, Guid? serverId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetReviewsQuery(status, serverId), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetReviews")
            .WithTags("Reviews");

        app.MapGet("/reviews/{reviewId:guid}", async (Guid reviewId, ISender sender, CancellationToken cancellationToken) =>
                TypedResults.Ok(await sender.Send(new GetReviewQuery(reviewId), cancellationToken)))
            .RequireAuthorization(Policies.Reader)
            .WithName("GetReview")
            .WithTags("Reviews");
    }
}

public static class ApproveReviewEndpoint
{
    public static void Map(this IEndpointRouteBuilder app) =>
        app.MapPost("/reviews/{reviewId:guid}:approve",
                async (Guid reviewId, ApproveReviewCommand? command, ISender sender, CancellationToken cancellationToken) =>
                {
                    await sender.Send((command ?? new ApproveReviewCommand()) with { ReviewId = reviewId }, cancellationToken);
                    return TypedResults.NoContent();
                })
            .RequireAuthorization(Policies.SecurityReviewer)
            .WithName("ApproveReview")
            .WithTags("Reviews");
}

public static class RejectReviewEndpoint
{
    public static void Map(this IEndpointRouteBuilder app) =>
        app.MapPost("/reviews/{reviewId:guid}:reject",
                async (Guid reviewId, RejectReviewCommand command, ISender sender, CancellationToken cancellationToken) =>
                {
                    await sender.Send(command with { ReviewId = reviewId }, cancellationToken);
                    return TypedResults.NoContent();
                })
            .RequireAuthorization(Policies.SecurityReviewer)
            .WithName("RejectReview")
            .WithTags("Reviews");
}
