using System.Text.Json.Serialization;
using AgentGate.Abstractions;
using Blocks.AspNetCore;
using ProtoBuf.Grpc.Server;

namespace Registry.API;

public static class Policies
{
    // policy names equal the realm role they require (Realms/agentgate-realm.json)
    public const string RegistryAdmin = Roles.RegistryAdmin;
    public const string SnapshotReader = Roles.SnapshotReader;
    public const string GatewaySync = Roles.GatewaySync;
}

public static class DependencyInjection
{
    private const string Audience = "agentgate-registry";

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
            .AddPolicy(Policies.RegistryAdmin, policy => policy.RequireRole(Roles.RegistryAdmin))
            .AddPolicy(Policies.SnapshotReader, policy => policy.RequireRole(Roles.SnapshotReader))
            .AddPolicy(Policies.GatewaySync, policy => policy.RequireRole(Roles.GatewaySync));

        builder.Services.AddCodeFirstGrpc();

        return builder;
    }
}
