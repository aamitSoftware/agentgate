using System.ComponentModel.DataAnnotations;

namespace Review.API;

public sealed class RegistryClientOptions
{
    /// <summary>Registry's HTTP/2 endpoint; <c>_grpc.registry</c> resolves the named endpoint through service discovery.</summary>
    [Required] public string Address { get; init; } = default!;
}
