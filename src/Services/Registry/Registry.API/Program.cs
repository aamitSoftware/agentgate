using Blocks.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Registry.API;
using Registry.API.Endpoints;
using Registry.Application;
using Registry.Persistence;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();
