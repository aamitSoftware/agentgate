using System.Text.Json.Serialization;
using AgentGate.Abstractions;
using AgentGate.Grpc.Contracts.Registry;
using Blocks.AspNetCore;
using Blocks.Core;
using Microsoft.Extensions.Options;
using ProtoBuf.Grpc.ClientFactory;

namespace Review.API;

public static class Policies
{
    public const string SecurityReviewer = Roles.SecurityReviewer;
    public const string RegistryAdmin = Roles.RegistryAdmin;

    /// <summary>Read access for anyone with a governance role.</summary>
    public const string Reader = "reader";

    public const string GatewaySync = Roles.GatewaySync;
}

public static class DependencyInjection
{
    private const string Audience = "agentgate-review";

    public static IHostApplicationBuilder AddApiServices(this IHostApplicationBuilder builder)
    {
        // the generator lives in this project; handlers in the referenced Application project are discovered
        builder.Services.AddMediator((MediatorOptions options) => options.ServiceLifetime = ServiceLifetime.Scoped);
        builder.Services.AddApiDefaults();
        builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddAuthentication()
            .AddKeycloakJwtBearer(serviceName: "keycloak", realm: "agentgate", options =>
            {
                options.Audience = Audience;
                options.MapInboundClaims = false;                          // keep 'sub' and 'roles' as issued
                options.TokenValidationParameters.RoleClaimType = "roles"; // flat claim from a realm mapper
                if (builder.Environment.IsDevelopment())
                    options.RequireHttpsMetadata = false;                  // Keycloak runs on http://localhost:8080
            });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.SecurityReviewer, policy => policy.RequireRole(Roles.SecurityReviewer))
            .AddPolicy(Policies.RegistryAdmin, policy => policy.RequireRole(Roles.RegistryAdmin))
            .AddPolicy(Policies.Reader, policy => policy.RequireRole(Roles.Auditor, Roles.RegistryAdmin, Roles.SecurityReviewer))
            .AddPolicy(Policies.GatewaySync, policy => policy.RequireRole(Roles.GatewaySync));

        builder.AddRegistryClient();
        return builder;
    }

    /// <summary>The one gRPC client (grpc-communication skill): authenticated as Review itself, resolved by service discovery.</summary>
    private static void AddRegistryClient(this IHostApplicationBuilder builder)
    {
        builder.AddServiceIdentity();
        builder.Services.AddAndValidateOptions<RegistryClientOptions>(builder.Configuration);

        builder.Services.AddCodeFirstGrpcClient<IRegistryService>((serviceProvider, options) =>
                options.Address = new Uri(serviceProvider.GetRequiredService<IOptions<RegistryClientOptions>>().Value.Address))
            .AddHttpMessageHandler<ServiceTokenHandler>();
    }
}
