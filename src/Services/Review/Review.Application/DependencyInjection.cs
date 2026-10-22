using Anthropic;
using Blocks.EntityFrameworkCore;
using Blocks.Mediator;
using Blocks.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Review.Application.Features.ManifestReviews.RiskScanning;
using Review.Application.StateMachines;

namespace Review.Application;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddApplicationServices(this IHostApplicationBuilder builder)
    {
        var assembly = typeof(DependencyInjection).Assembly;
        builder.Services
            .AddPipelineBehaviors()
            .AddValidatorsFromAssembly(assembly)
            .AddDomainEventDispatch(assembly);

        // loaded once per process on first resolve; a reseeded table needs a restart
        builder.Services.AddSingleton<ReviewStateMachineFactory>(serviceProvider =>
        {
            var transitions = LoadTransitions(serviceProvider, db => db.ReviewStatusTransitions);
            return status => new ReviewStateMachine(status, transitions);
        });
        builder.Services.AddSingleton<GrantStateMachineFactory>(serviceProvider =>
        {
            var transitions = LoadTransitions(serviceProvider, db => db.GrantStatusTransitions);
            return status => new GrantStateMachine(status, transitions);
        });

        builder.Services.AddAndValidateOptions<ClaudeRiskScannerOptions>(builder.Configuration);
        var claude = builder.Configuration.GetSection(nameof(ClaudeRiskScannerOptions)).Get<ClaudeRiskScannerOptions>();
        if (claude?.Enabled == true)
        {
            // its own HttpClient: the default resilience handler's 30 s total timeout would cut a long model call short;
            // the SDK retries 408/409/429/5xx itself, and the scanner bounds the whole call with TimeoutSeconds
            builder.Services.AddSingleton(new AnthropicClient { ApiKey = claude.ApiKey!, Timeout = TimeSpan.FromSeconds(claude.TimeoutSeconds) });
            builder.Services.AddSingleton<IRiskScanner, ClaudeRiskScanner>();
        }
        else
        {
            builder.Services.AddSingleton<IRiskScanner, HeuristicRiskScanner>();
        }

        builder.AddMessaging<ReviewDbContext>("review", assembly);
        return builder;
    }

    private static List<T> LoadTransitions<T>(IServiceProvider serviceProvider, Func<ReviewDbContext, IQueryable<T>> table) where T : class
    {
        using var scope = serviceProvider.CreateScope();
        return [.. table(scope.ServiceProvider.GetRequiredService<ReviewDbContext>()).AsNoTracking()];
    }
}
