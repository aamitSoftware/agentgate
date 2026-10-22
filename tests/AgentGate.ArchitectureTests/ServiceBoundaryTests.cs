using AgentGate.Abstractions;
using AgentGate.Grpc.Contracts.Registry;
using AgentGate.IntegrationEvents.Registry;
using static AgentGate.ArchitectureTests.BuildingBlocksTests;

namespace AgentGate.ArchitectureTests;

/// <summary>What may cross a service boundary, and how.</summary>
public class ServiceBoundaryTests
{
    // insight - the gateway's hot path must not depend on Registry being up: it answers from event-built local copies
    [Fact]
    public void Gateway_never_calls_gRPC() =>
        ShouldNotDependOn(typeof(Gateway.API.GatewayOptions).Assembly, ["AgentGate.Grpc.Contracts", "Grpc", "ProtoBuf"]);

    [Fact]
    public void Services_never_reference_each_other() =>
        Assert.All(
            [
                (typeof(Registry.Application.DependencyInjection).Assembly, new[] { "Review.", "Gateway.", "Hub." }),
                (typeof(Review.Application.DependencyInjection).Assembly, new[] { "Registry.", "Gateway.", "Hub." }),
                (typeof(Gateway.API.GatewayOptions).Assembly, new[] { "Registry.", "Review.", "Hub." }),
                (typeof(Hub.Application.DependencyInjection).Assembly, new[] { "Registry.", "Review.", "Gateway." })
            ],
            rule => ShouldNotDependOn(rule.Item1, rule.Item2));

    // insight - the Hub only reads: it neither calls another service nor publishes, so it can never decide anything
    [Fact]
    public void Hub_neither_calls_gRPC_nor_holds_business_rules() =>
        ShouldNotDependOn(typeof(Hub.Application.DependencyInjection).Assembly,
            ["AgentGate.Grpc.Contracts", "Grpc", "ProtoBuf", "Blocks.Domain", "Stateless"]);

    [Fact]
    public void Hub_rows_reference_nothing() =>
        ShouldNotDependOn(typeof(Hub.Domain.ServerView).Assembly,
            ["Microsoft.EntityFrameworkCore", "MassTransit", "Blocks.", "AgentGate.", "Hub.Persistence", "Hub.Application", "Hub.API"]);

    [Fact]
    public void Integration_events_are_plain_contracts() =>
        ShouldNotDependOn(typeof(ServerApproved).Assembly,
            ["Blocks.", "Registry.", "Review.", "Gateway.", "Hub.", "MassTransit", "Microsoft.EntityFrameworkCore"]);

    [Fact]
    public void The_gRPC_contract_references_no_service() =>
        ShouldNotDependOn(typeof(IRegistryService).Assembly, ["Blocks.", "Registry.", "Review.", "Gateway."]);

    [Fact]
    public void Shared_vocabulary_is_a_leaf() =>
        ShouldNotDependOn(typeof(Roles).Assembly, ["Blocks.", "Registry.", "Review.", "Gateway.", "Microsoft.", "MassTransit"]);
}
