using Blocks.EntityFrameworkCore;
using Blocks.Mediator;
using Blocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Registry.Application.Features.McpServers.DetectDrift;
using Registry.Application.StateMachines;

namespace Registry.Application;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        builder.Services
            .AddPipelineBehaviors()
            .AddValidatorsFromAssembly(assembly)
            .AddDomainEventDispatch(assembly);

        builder.Services.AddSingleton<ServerStateMachineFactory>(serviceProvider =>
        {
            using var scope = serviceProvider.CreateScope();
            // loaded once per process on first resolve; a reseeded table needs a restart
            var transitions = scope.ServiceProvider.GetRequiredService<RegistryDbContext>()
                .ServerStatusTransitions.AsNoTracking().ToList();
            return status => new ServerStateMachine(status, transitions);
        });

        builder.Services.AddHttpClient<McpToolsClient>();
        builder.Services.AddAndValidateOptions<DriftDetectionOptions>(builder.Configuration);
        builder.Services.AddHostedService<DriftDetectionJob>();

        builder.AddMessaging<RegistryDbContext>("registry", assembly);
        return builder;
    }
}
