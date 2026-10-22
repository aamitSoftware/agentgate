using Blocks.AspNetCore;
using Hub.API;
using Hub.API.Endpoints;
using Hub.Application;
using Hub.Persistence;
using Microsoft.EntityFrameworkCore;

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
    await scope.ServiceProvider.GetRequiredService<HubDbContext>().Database.MigrateAsync();
}

app.UseApiDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapAllEndpoints();

app.Run();

public partial class Program;
