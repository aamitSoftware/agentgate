namespace Registry.Application.Features.McpServers.RegisterServer;

public sealed record RegisterServerCommand : ServerCommand, ICommand<RegisterServerResponse>
{
    public required string Name { get; init; }
    public required string Url { get; init; }
    public override ServerActionType ActionType => ServerActionType.Register;
}

public sealed record RegisterServerResponse(Guid Id, string ManifestHash);

public sealed class RegisterServerValidator : AbstractValidator<RegisterServerCommand>
{
    public RegisterServerValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(MaxLength.C64);
        RuleFor(command => command.Url).NotEmpty().MaximumLength(MaxLength.C2048);
        RuleFor(command => command.Comment).MaximumLength(MaxLength.C512);
    }
}

public sealed class RegisterServerHandler(McpServerRepository servers, McpToolsClient toolsClient)
    : ICommandHandler<RegisterServerCommand, RegisterServerResponse>
{
    public async ValueTask<RegisterServerResponse> Handle(RegisterServerCommand command, CancellationToken cancellationToken)
    {
        if (await servers.NameExistsAsync(command.Name, cancellationToken))
            throw new ConflictException($"A server named '{command.Name}' is already registered.");

        var url = ServerUrl.Create(command.Url);
        var manifest = await toolsClient.FetchManifestAsync(url, cancellationToken);
        var server = McpServer.Register(command.Name, url, manifest, command);

        await servers.AddAsync(server, cancellationToken);
        await servers.SaveChangesAsync(cancellationToken);
        return new RegisterServerResponse(server.Id, manifest.Hash);
    }
}
