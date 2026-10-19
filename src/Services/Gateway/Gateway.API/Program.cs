using System.Diagnostics;
using System.Net;
using Blocks.AspNetCore;
using Blocks.Core;
using Gateway.API;
using Gateway.API.Features.McpProxy;
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
builder.Services.AddSingleton<GrantTable>();
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
