using System.ComponentModel.DataAnnotations;

namespace Gateway.API;

public sealed class GatewayOptions
{
    /// <summary>Tokens must be issued for the gateway (aud), not merely by the right realm.</summary>
    [Required]
    public string Audience { get; init; } = default!;
}
