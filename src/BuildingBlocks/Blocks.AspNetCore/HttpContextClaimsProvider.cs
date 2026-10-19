using System.Security.Claims;
using Blocks.Core;
using Microsoft.AspNetCore.Http;

namespace Blocks.AspNetCore;

// insight - expects JwtBearer with MapInboundClaims = false, so 'sub' stays 'sub' instead of becoming ClaimTypes.NameIdentifier
public sealed class HttpContextClaimsProvider(IHttpContextAccessor httpContextAccessor) : IClaimsProvider
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? TryGetActorId() => User?.FindFirstValue("sub");

    public IReadOnlySet<string> GetRoles() =>
        User?.Identities
            .SelectMany(identity => identity.FindAll(identity.RoleClaimType))
            .Select(claim => claim.Value)
            .ToHashSet()
        ?? new HashSet<string>();
}
