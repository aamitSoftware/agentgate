namespace Registry.API.Endpoints;

public static class RegisterAgentEndpoint
{
    public static void Map(this IEndpointRouteBuilder app) =>
        app.MapPost("/agents", async (RegisterAgentCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var response = await sender.Send(command, cancellationToken);
                return TypedResults.Created($"/agents/{response.Id}", response);
            })
            .RequireAuthorization(Policies.RegistryAdmin)
            .WithName("RegisterAgent")
            .WithTags("Agents");
}
