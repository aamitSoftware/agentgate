using Blocks.Mediator;
using Blocks.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hub.Application;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddPipelineBehaviors();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ActivityFeed>();

        // consumers only: the Hub publishes nothing; the inbox makes every projection apply once
        builder.AddMessaging<HubDbContext>("hub", typeof(DependencyInjection).Assembly);
        return builder;
    }
}
