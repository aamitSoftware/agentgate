namespace Registry.API.Endpoints;

public static class RegisterServerEndpoint
{
    public static void Map(this IEndpointRouteBuilder app) =>
        app.MapPost("/servers", async (RegisterServerCommand command, ISender sender, CancellationToken cancellationToken) =>
            {
                var response = await sender.Send(command, cancellationToken);
                return TypedResults.Created($"/servers/{response.Id}", response);
            })
            .RequireAuthorization(Policies.RegistryAdmin)
            .WithName("RegisterServer")
            .WithTags("Servers");
}

public static class EndpointRegistration
{
    public static IEndpointRouteBuilder MapAllEndpoints(this IEndpointRouteBuilder app)
    {
        RegisterServerEndpoint.Map(app);
        RegisterAgentEndpoint.Map(app);
        return app;
    }
}
