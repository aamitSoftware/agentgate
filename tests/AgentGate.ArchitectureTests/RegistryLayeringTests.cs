using Registry.Domain.McpServers;
using Registry.Persistence;
using static AgentGate.ArchitectureTests.BuildingBlocksTests;

namespace AgentGate.ArchitectureTests;

/// <summary>Clean Architecture layering inside the Registry service: dependencies point inward only.</summary>
public class RegistryLayeringTests
{
    [Fact]
    public void Domain_is_framework_free() =>
        ShouldNotDependOn(typeof(McpServer).Assembly,
        [
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MassTransit", "Mediator", "FluentValidation", "Stateless",
            "ModelContextProtocol", "Blocks.Core", "Blocks.Exceptions", "Registry.Application", "Registry.Persistence", "Registry.API"
        ]);

    [Fact]
    public void Persistence_does_not_depend_on_application_or_api() =>
        ShouldNotDependOn(typeof(RegistryDbContext).Assembly, ["Registry.Application", "Registry.API"]);

    [Fact]
    public void Application_does_not_depend_on_api_or_aspnetcore() =>
        ShouldNotDependOn(typeof(Registry.Application.DependencyInjection).Assembly, ["Registry.API", "Microsoft.AspNetCore"]);
}
