using System.ComponentModel.DataAnnotations;

namespace Gateway.API;

public sealed class GatewayOptions
{
    /// <summary>Tokens must be issued for the gateway (aud), not merely by the right realm.</summary>
    [Required]
    public string Audience { get; init; } = default!;

    /// <summary>Rebuild servers and grants from Registry and Review at startup (off only in tests that feed events directly).</summary>
    public bool Bootstrap { get; init; } = true;

    [Required]
    public string RegistryAddress { get; init; } = default!;

    [Required]
    public string ReviewAddress { get; init; } = default!;
}
