# Handoff: M2 (Registry) in progress

Branch `m2-registry-wip`. M0 and M1 are done on `main` (CI green). This branch holds M2 work that is
**not finished and not fully built or tested yet**. Merge to `main` only when everything below passes.

## Before you start (new machine)

```bash
git config user.name "Amit Asher"
git config user.email "264781261+aamitSoftware@users.noreply.github.com"   # repo-local, never the global config
dotnet tool restore                                                           # dotnet-ef 10.0.13
```

Read `CLAUDE.md` (repo rules, including: no AI attribution in commits/PRs), `docs/plan.md`, and the
skills in `.claude/skills/` (house patterns; follow them).

## Done on this branch

- **Registry service** `src/Services/Registry/{Domain,Persistence,Application,API}`:
  - `McpServer` aggregate (state/behavior split): `Register` → UnderReview, `Approve` (pins the pending
    manifest), `Quarantine` (keeps the observed manifest as pending). Audit entries per action.
  - `ToolManifest` value object: canonical JSON (tools sorted by name, keys sorted, only
    name/title/description/inputSchema/outputSchema/annotations) + SHA-256 hash. Stored as `text`
    (not `jsonb`, which reorders keys); hash recomputed on load.
  - Transition table seeded from `Registry.Persistence/Data/ServerStatusTransition.json`
    (UnderReview→Approve→Approved, Approved→Quarantine→Quarantined, Quarantined→Approve→Approved).
  - Features: `POST /servers` (fetches tools/list via `McpToolsClient`), `POST /servers/{id}:approve`
    (temporary, single admin; M3 moves approval to Review), `QuarantineServerCommand` (no endpoint).
  - `DriftDetectionJob` (BackgroundService, `DriftDetectionOptions.IntervalSeconds` = 10): re-fetches
    approved servers, quarantines on hash mismatch as `system:drift-detector`; unreachable = log only.
  - Publishers `PublishIntegrationEventOnServerApproved/QuarantinedHandler` → EF bus outbox.
  - Initial migration generated. Dev-only `MigrateAsync` on startup in `Program.cs`.
  - Auth: Keycloak JWT, audience `agentgate-registry`, flat `roles` claim, policy `registry-admin`.
- **`AgentGate.IntegrationEvents`**: `ServerApproved(ServerId, Name, Url, ManifestHash, ToolsJson, OccurredOn)`,
  `ServerQuarantined(ServerId, Name, PinnedHash, ObservedHash, Reason, OccurredOn)`.
- **Gateway**: static `Servers` option removed. `Features/Catalog/ServerCatalog` is an in-memory local
  copy fed by `ServerApprovedConsumer`/`ServerQuarantinedConsumer` (last-writer-wins by `OccurredOn`).
  `tools/list` is answered from the pinned manifest filtered to the agent's grants; quarantined server
  → 403; unknown server → 404 (fails closed after restart until M3 bootstrap); `tools/call` also
  requires the tool to be in the pinned manifest.
- **Demo**: `DemoMcpServer` has dev-only `POST /admin/rug-pull` (poisons lookup_customer's description).
  `DemoAgent` runs setup (register + approve as `agentgate-admin`) → calls → rug pull, by
  `DemoAgent:Scenario` = all|setup|call|rugpull.
- **Realm**: confidential client `agentgate-admin` (service account with registry-admin, registry
  audience mapper, flat roles mapper).
- **AppHost**: `registrydb`, `registry` project, gateway now references rabbitmq; demo-agent references
  registry + demo-mcp-server.
- **Tests (written, not yet run)**: `tests/Registry.Tests` with `ToolManifestTests`, `McpServerTests`
  (uses the real seeded JSON + Stateless machine), `McpToolsClientTests` (in-memory DemoMcpServer).

## Still to do for M2

1. **Build + fix**: `dotnet build` (AppHost reference to Registry.API was added last; not compiled yet).
2. **Add `tests/Registry.Tests` to `AgentGate.slnx`** (`dotnet sln AgentGate.slnx add tests/Registry.Tests/Registry.Tests.csproj --solution-folder tests`).
3. **Outbox atomicity test** (`tests/Registry.Tests/OutboxAtomicityTests.cs`, Testcontainers Postgres):
   - DI: `AddDomainEventDispatch(Registry.Application assembly)`, DbContext with the interceptor,
     `McpServerRepository`, a `ServerStateMachineFactory` from `ServerStatusTransitionConfiguration.LoadSeed()`,
     `AddMassTransitTestHarness(bus => bus.AddEntityFrameworkOutbox<RegistryDbContext>(o => { o.UsePostgres(); o.UseBusOutbox(b => b.DisableDeliveryService()); }))`, then `MigrateAsync`.
   - Test 1: register+save, approve+save → status Approved and exactly 1 `OutboxMessage` whose MessageType contains `ServerApproved`.
   - Test 2: approve + add a second server with the same name in the same save → `DbUpdateException`,
     status still UnderReview, 0 outbox rows.
   - Without Docker: catch `DockerUnavailableException` when env `CI` is unset and `Assert.Skip`; CI (ubuntu) must run it.
4. **Gateway tests** (`tests/Gateway.Tests/GatewayEndToEndTests.cs`) no longer pass as written: the
   gateway learns servers from events now. Update:
   - `UseSetting("ConnectionStrings:rabbitmq", "amqp://localhost")` and `services.AddMassTransitTestHarness()` in ConfigureTestServices;
   - in `InitializeAsync` publish `ServerApproved` for `crm` (Url `http://demo-mcp-server/mcp`, a hand-written pinned ToolsJson) and wait for `harness.Consumed.Any<ServerApproved>()`;
   - new tests: tools/list returns only granted tools with the pinned description (also after `POST /admin/rug-pull`);
     quarantined → 403; unknown server → 404; plus `ServerCatalog` unit tests (stale approval can't lift a quarantine).
5. **Architecture tests**: Registry.Domain framework-free (no EF, MassTransit, Mediator, Stateless, ModelContextProtocol, Blocks.Core/Exceptions);
   Persistence must not depend on Application/API; Application must not depend on API/AspNetCore.
6. **Docs**: README status (M2 done), `docs/plan.md` notes (trimmed M2 lifecycle; gateway fails closed on restart until M3 bootstrap;
   dev-only startup migrations), update the lifecycle table in `.claude/skills/governance-state-machine/SKILL.md` to match the seed.
7. `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes` must pass. Then merge to `main`, push, check `gh run list`.
8. Delete this file when M2 is merged.

## Environment gotchas

- MassTransit stays on 8.5.x (v9 is commercial); Mediator (martinothamar), not MediatR.
- Tests use xunit.v3 on Microsoft.Testing.Platform (`global.json`), plain `Assert` (no Shouldly).
- Full demo needs Docker Desktop (Postgres, RabbitMQ, Keycloak via Aspire).

## After M2

M3 Review (risk scanner, four-eyes, grants, gRPC snapshot + gateway bootstrap), M4 Hub + React console,
M5 anomaly auto-suspend, Claude risk scanner, demo, then make the repo public. See `docs/plan.md`.
