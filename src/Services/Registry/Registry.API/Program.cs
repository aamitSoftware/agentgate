using Blocks.AspNetCore;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Registry.API;
using Registry.API.Endpoints;
using Registry.API.Grpc;
using Registry.Application;
using Registry.Persistence;

var builder = WebApplication.CreateBuilder(args);

// gRPC needs HTTP/2 and a cleartext endpoint speaks only one protocol, so under the AppHost REST and gRPC get one port each
if (builder.Configuration["GRPC_PORT"] is { } grpcPort && builder.Configuration["HTTP_PORT"] is { } httpPort)
    builder.WebHost.ConfigureKestrel(kestrel =>
    {
        kestrel.ListenAnyIP(int.Parse(httpPort));
        kestrel.ListenAnyIP(int.Parse(grpcPort), listen => listen.Protocols = HttpProtocols.Http2);
    });

builder.AddServiceDefaults();
builder.AddPersistenceServices();
builder.AddApplicationServices();
builder.AddApiServices();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // dev convenience only: a real deployment applies migrations as a pipeline step, never on app startup
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<RegistryDbContext>().Database.MigrateAsync();
}

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapAllEndpoints();
app.MapGrpcService<RegistryGrpcService>().RequireAuthorization(Policies.SnapshotReader);

app.Run();
