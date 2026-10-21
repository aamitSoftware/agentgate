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
                (typeof(Registry.Application.DependencyInjection).Assembly, new[] { "Review.", "Gateway." }),
                (typeof(Review.Application.DependencyInjection).Assembly, new[] { "Registry.", "Gateway." }),
                (typeof(Gateway.API.GatewayOptions).Assembly, new[] { "Registry.", "Review." })
            ],
            rule => ShouldNotDependOn(rule.Item1, rule.Item2));

    [Fact]
    public void Integration_events_are_plain_contracts() =>
        ShouldNotDependOn(typeof(ServerApproved).Assembly,
            ["Blocks.", "Registry.", "Review.", "Gateway.", "MassTransit", "Microsoft.EntityFrameworkCore"]);

    [Fact]
    public void The_gRPC_contract_references_no_service() =>
        ShouldNotDependOn(typeof(IRegistryService).Assembly, ["Blocks.", "Registry.", "Review.", "Gateway."]);

    [Fact]
    public void Shared_vocabulary_is_a_leaf() =>
        ShouldNotDependOn(typeof(Roles).Assembly, ["Blocks.", "Registry.", "Review.", "Gateway.", "Microsoft.", "MassTransit"]);
}
