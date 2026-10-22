using System.Text.Json.Serialization;
using AgentGate.Abstractions;
using Blocks.AspNetCore;

namespace Hub.API;

public static class Policies
{
    /// <summary>Read access for anyone with a governance role: the Hub is read-only.</summary>
    public const string Reader = "reader";
}

public static class DependencyInjection
{
    private const string Audience = "agentgate-hub";

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
                    options.RequireHttpsMetadata = false;                  // Keycloak runs on plain HTTP in dev (AppHost)
            });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Reader, policy => policy.RequireRole(Roles.Auditor, Roles.RegistryAdmin, Roles.SecurityReviewer));

        return builder;
    }
}
