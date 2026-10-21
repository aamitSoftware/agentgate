---
name: authorization-security
description: Authentication and two-layer authorization in AgentGate - Keycloak OIDC with RS256 and audience validation (MapInboundClaims = false, flat roles claim), the closed role vocabulary (registry-admin, security-reviewer, auditor), endpoint role policies, resource checks answered from local data (the Gateway grant check), agents on client credentials, and actor stamping from the sub claim. Use when gating an endpoint, configuring JWT bearer auth in a service, adding a role, or deciding who acted on a change.
---

# Authorization & Security

Two gates at the boundary plus one identity contract. Business code (handlers, domain) never inspects
roles.

## Binding rules

1. **No role checks in handlers or the domain.** Gates are declarative on the endpoint.
2. **Every endpoint states its gate**: a role policy, or `AllowAnonymous()` with a reason (health only).
3. **Resource checks read local data only** - never gRPC, never a call to the owning service.
4. **Provenance is stamped, never trusted.** `ActorId` comes from the token's `sub` (`AssignActorBehavior`)
   or is an explicit system actor; it is `[JsonIgnore]` on every command.

## Identity: Keycloak

- Realm `agentgate` (AppHost, `Realms/agentgate-realm.json`). Tokens are **RS256**; keys come from the
  realm's JWKS - there is no shared secret anywhere.
- Humans: React console, OIDC authorization code + PKCE (client `agentgate-console`).
- Agents: **client credentials**; each agent's client is pre-created in the realm JSON.
- Every API validates issuer, signature, lifetime **and audience** (audience mappers in the realm JSON
  stand in for RFC 8707 resource indicators).

Target shape (identical in every API; lift into `Blocks.AspNetCore` next to `HttpContextClaimsProvider`,
which depends on these exact settings):

```csharp
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(serviceName: "keycloak", realm: "agentgate", options =>
    {
        options.Audience = "agentgate-registry";                  // this service's audience
        options.MapInboundClaims = false;                         // keep 'sub' and 'roles' as issued
        options.TokenValidationParameters.RoleClaimType = "roles"; // flat claim from a realm mapper
        if (builder.Environment.IsDevelopment())
            options.RequireHttpsMetadata = false;                 // Keycloak runs on http://localhost:8080
    });
```

`MapInboundClaims = false` is load-bearing: `HttpContextClaimsProvider.TryGetActorId()` reads `"sub"`
and `GetRoles()` reads the identity's `RoleClaimType`. Roles must arrive as a **flat `roles` claim**
(realm protocol mapper), not Keycloak's default nested `realm_access.roles`.

## Layer 1 - role vocabulary and policies

Closed vocabulary, defined once (target shape: `Roles` and `Policies` constants in
`AgentGate.Abstractions`), matching the realm roles:

```csharp
public static class Roles
{
    public const string RegistryAdmin = "registry-admin";       // registers servers and agents
    public const string SecurityReviewer = "security-reviewer"; // reviews manifests, approves grants (four-eyes)
    public const string Auditor = "auditor";                    // read-only hub and audit trail
    public const string SnapshotReader = "snapshot-reader";     // service-to-service: Review -> Registry gRPC only
}

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.RegistryAdmin, policy => policy.RequireRole(Roles.RegistryAdmin))
    .AddPolicy(Policies.SecurityReviewer, policy => policy.RequireRole(Roles.SecurityReviewer))
    .AddPolicy(Policies.Reader, policy => policy.RequireRole(Roles.Auditor, Roles.RegistryAdmin, Roles.SecurityReviewer));
```

Endpoints name a policy: `.RequireAuthorization(Policies.SecurityReviewer)`. Writes take the role that
owns the action; Hub reads take `Policies.Reader`.

**Add a role:** realm JSON role -> `Roles` constant -> policy -> use it at endpoints. Never scatter the
string literal.

## Layer 2 - resource checks

A resource check answers "may *this* caller touch *this* resource", from data the service already holds.

- **Gateway (the main one):** an agent may call `tools/call` for tool T on server S only if its local
  copy has an `Active`, unexpired grant for (agent, S, T) **and** S is approved and not quarantined.
  Deny by default; `tools/list` is answered from the pinned manifest filtered to the agent's grants.
  The local copy is built from `GrantActivated/Suspended/Revoked` and `ServerApproved/Quarantined`;
  the hot path never calls gRPC.
- **Console endpoints:** v1 roles own whole resource types, so the role policy is enough. Add a resource
  check only when a resource gets an owner narrower than a role - then as an
  `AuthorizationHandler<TRequirement>` reading the service's own tables.
- **Four-eyes is not authorization** - it is a domain invariant on `Grant`/`ManifestReview`
  (`DomainException` when the same `ActorId` approves twice).

An agent is identified at the Gateway by the token's `azp` (its Keycloak client id), which is
`Agent.ClientId` in Registry and `GrantActivated.AgentClientId` in the gateway's local copy.

**Service-to-service:** the one gRPC call authenticates as the calling service (Review's `agentgate-review`
client, role `snapshot-reader`, `Policies.SnapshotReader` on Registry), never with a forwarded user token
(ADR 0004).

## Actor stamping

`AssignActorBehavior` (Blocks.Mediator, first in the pipeline) fills `IAuditableAction.ActorId` from
`IClaimsProvider.TryGetActorId()` **only when empty**, so system actors survive:

```csharp
new QuarantineServerCommand { ServerId = serverId, ActorId = SystemActors.DriftDetector }   // "system:drift-detector"
```

System actors are explicit ids (`system:drift-detector`, `system:anomaly-detector`; target:
`AgentGate.Abstractions.SystemActors`), never null.

## Outbound credentials (Gateway)

When forwarding to an MCP server, strip the agent's `Authorization` header and attach the gateway's own
upstream credential - an MCP server must not accept a token issued for another client.

## Not this skill

Issuing tokens (Keycloak does), the realm JSON contents, the grant data model (`create-aggregate`).
