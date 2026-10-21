using Review.Domain.ManifestReviews;
using Review.Persistence;
using static AgentGate.ArchitectureTests.BuildingBlocksTests;

namespace AgentGate.ArchitectureTests;

/// <summary>Clean Architecture layering inside the Review service: dependencies point inward only.</summary>
public class ReviewLayeringTests
{
    [Fact]
    public void Domain_is_framework_free() =>
        ShouldNotDependOn(typeof(ManifestReview).Assembly,
        [
            "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MassTransit", "Mediator", "FluentValidation", "Stateless",
            "Grpc", "ProtoBuf", "AgentGate.Grpc.Contracts", "AgentGate.IntegrationEvents", "Blocks.Core", "Blocks.Exceptions",
            "Review.Application", "Review.Persistence", "Review.API"
        ]);

    [Fact]
    public void Persistence_does_not_depend_on_application_or_api() =>
        ShouldNotDependOn(typeof(ReviewDbContext).Assembly, ["Review.Application", "Review.API"]);

    [Fact]
    public void Application_does_not_depend_on_api_or_aspnetcore() =>
        ShouldNotDependOn(typeof(Review.Application.DependencyInjection).Assembly, ["Review.API", "Microsoft.AspNetCore"]);
}
