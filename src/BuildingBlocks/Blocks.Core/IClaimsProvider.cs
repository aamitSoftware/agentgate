namespace Blocks.Core;

/// <summary>The authenticated caller, independent of where the claims come from (HTTP today).</summary>
public interface IClaimsProvider
{
    /// <summary>The subject ('sub') of the token: a user or an agent client.</summary>
    string? TryGetActorId();
    string GetActorId() => TryGetActorId() ?? throw new UnauthorizedAccessException("Missing subject claim.");
    IReadOnlySet<string> GetRoles();
}
