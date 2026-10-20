using System.Reflection;
using Blocks.Core;
using Blocks.Domain;
using Blocks.Exceptions;
using NetArchTest.Rules;

namespace AgentGate.ArchitectureTests;

/// <summary>The dependency rules from the course's reference model, enforced instead of documented.</summary>
public class BuildingBlocksTests
{
    private static readonly string[] _frameworks =
    [
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "MassTransit", "Mediator", "FluentValidation"
    ];

    [Fact]
    public void Domain_kernel_is_framework_free() =>
        ShouldNotDependOn(typeof(Entity).Assembly, [.. _frameworks, "Blocks.Core", "Blocks.Exceptions"]);

    [Fact]
    public void Exceptions_are_a_leaf() =>
        ShouldNotDependOn(typeof(HttpException).Assembly, [.. _frameworks, "Blocks.Domain", "Blocks.Core"]);

    [Fact]
    public void Core_stays_free_of_infrastructure_frameworks() =>
        ShouldNotDependOn(typeof(Guard).Assembly, _frameworks);

    internal static void ShouldNotDependOn(Assembly assembly, string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbidden)}]; offending types: " +
            string.Join(", ", result.FailingTypeNames ?? []));
    }
}
