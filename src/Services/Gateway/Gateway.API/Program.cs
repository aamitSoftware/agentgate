using System.Diagnostics;
using System.Net;
using Blocks.AspNetCore;
using Blocks.Core;
using Blocks.Messaging;
using Gateway.API;
using Gateway.API.Features.Bootstrap;
using Gateway.API.Features.McpProxy;
using Gateway.API.Features.Catalog;
using Gateway.API.Features.Grants;
using Gateway.API.Features.Observation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddApiDefaults();
builder.Services.AddAndValidateOptions<GatewayOptions>(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddKeycloakJwtBearer(serviceName: "keycloak", realm: "agentgate", options =>
    {
        options.Audience = builder.Configuration[$"{nameof(GatewayOptions)}:{nameof(GatewayOptions.Audience)}"];
        options.MapInboundClaims = false; // keep 'sub' / 'azp' as issued
        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;
    });
builder.Services.AddAuthorization();

builder.Services.AddHttpForwarder();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GrantTable>();
builder.Services.AddSingleton<ServerCatalog>();
builder.Services.AddSingleton<ObservationQueue>();
builder.Services.AddHostedService<ObservationPublisher>();
if (builder.Configuration.GetValue($"{nameof(GatewayOptions)}:{nameof(GatewayOptions.Bootstrap)}", defaultValue: true))
    builder.AddGatewayBootstrap();
// no outbox: the gateway owns no transactional state (its local copy is rebuilt from events)
builder.AddMessaging("gateway", typeof(ServerCatalog).Assembly);
// one pooled client for all upstream MCP servers, configured as YARP recommends for proxying
builder.Services.AddSingleton(new HttpMessageInvoker(new SocketsHttpHandler
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false,
    EnableMultipleHttp2Connections = true,
    ConnectTimeout = TimeSpan.FromSeconds(15),
    ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current)
}));

var app = builder.Build();

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapMcpProxy();

app.Run();
