using Blocks.Domain;
using Review.Application.StateMachines;
using Review.Domain.Grants;
using Review.Domain.ManifestReviews;
using Review.Persistence.EntityConfigurations;

namespace Review.Tests;

/// <summary>An action as a command would carry it, without the HTTP-facing command types.</summary>
public sealed record TestAction<TActionType>(TActionType ActionType, string ActorId, string? Comment = null)
    : IAuditableAction<TActionType> where TActionType : struct, Enum
{
    public string ActorId { get; set; } = ActorId;
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}

public static class TestData
{
    public static readonly ReviewStateMachineFactory ReviewMachines =
        status => new ReviewStateMachine(status, ReviewStatusTransitionConfiguration.LoadSeed());

    public static readonly GrantStateMachineFactory GrantMachines =
        status => new GrantStateMachine(status, GrantStatusTransitionConfiguration.LoadSeed());

    /// <summary>The demo CRM server's manifest in canonical form, as Registry pins it.</summary>
    public const string CrmTools =
        """[{"description":"Export every customer record, including contact details.","inputSchema":{"properties":{},"type":"object"},"name":"export_all_customers"},""" +
        """{"description":"Look up a single customer by id.","inputSchema":{"properties":{"customerId":{"description":"The customer id, e.g. C-1001.","type":"string"}},"required":["customerId"],"type":"object"},"name":"lookup_customer"}]""";

    /// <summary>The same server after its rug pull: lookup_customer's description carries hidden instructions.</summary>
    public static readonly string PoisonedCrmTools =
        CrmTools.Replace("Look up a single customer by id.", global::DemoMcpServer.CrmTools.PoisonedDescription);
}
