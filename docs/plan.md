# AgentGate: design and delivery plan

## Context

AgentGate applies the patterns of a DDD / vertical-slice / CQRS microservices reference architecture (MassTransit, gRPC, YARP) to a new, real problem. It also fixes the reference's known gaps: no outbox, no tests, no observability, no CI.

**The problem:**
- **Akamai's State of the Internet report** (22 Sept 2026, "Speed, Scale, and Nonhuman Identity") says *MCP exposure ranks last among CISO priorities*, even though MCP lets agents act autonomously across systems. It calls for "behavioral governance" of nonhuman identities and for matching autonomy to verifiability.
- **Current agentic security frameworks** focus on agents reaching *public websites*. Few cover what *enterprise* agents may do with internal tools.

**AgentGate** fills that gap. It is a governance control plane for AI agents' tool access over MCP. Each server goes through: discover → risk-scan → approve (two different approvers) → pin manifest → enforce per-agent grants at a gateway → detect drift or anomalies → quarantine or suspend.

**Demo, two scenarios:**
1. **Rug pull.** An approved MCP server quietly rewrites a tool description to include hidden instructions. AgentGate quarantines it automatically, the agent never sees the poisoned text, and a re-review is triggered. The whole flow can be traced in the Aspire dashboard.
2. **Prompt-injected agent.** An agent starts hammering tools it has no grant for. Its grant is suspended automatically, and you can watch it happen in the UI.

**Scope:** 4 services built to production standard, all .NET, plus a React console.

---

## Architecture

### Services

Four services, plus Keycloak and the demo kit.

| Service | Course analogue | Owns |
|---|---|---|
| **Registry** | Submission + Journals | `Agent`: nonhuman identity (owner, purpose, Keycloak clientId).<br>`McpServer`: url, status, pinned `Manifest` (tools + SHA-256 hash).<br>Background **drift job**: re-fetches `tools/list` and quarantines the server on a hash mismatch.<br>gRPC server for `GetServerSnapshot`. |
| **Review** | Review | `ManifestReview`: risk scan + decision.<br>`Grant` aggregate: agent × server × tools, with expiry.<br>Four-eyes approval.<br>**Risk scanner**: one interface with `HeuristicRiskScanner` (default) and `ClaudeRiskScanner` (last milestone). |
| **Gateway** | Production + YARP | MCP enforcement proxy.<br>Keeps its own copy of approved servers and active grants, built from events. The hot path **never** calls gRPC.<br>Anomaly detection; emits `ToolCallObserved`. |
| **Hub** | ArticleHub + ArticleTimeline | Read model built only from events: inventory, who-can-call-what, audit timeline, live SSE activity feed. |

**Identity: Keycloak.**
- Hosted through Aspire's Keycloak integration (preview), configured with `WithRealmImport`.
- Humans sign in to React with OIDC + PKCE.
- Agents use client credentials. Their clients are pre-created in the realm JSON (no Keycloak admin-API automation in v1).
- Tokens are asymmetric RS256. This fixes the course's symmetric key.
- Keycloak doesn't support RFC 8707 resource indicators yet, so audience mappers in the realm JSON stand in for them.

**Dropped on purpose: a separate YARP API gateway for the UI.** Every service validates JWTs anyway. If a single origin is needed, Aspire's `AddYarp().WithConfiguration()` is configuration only.

**Design note:** at this scale Registry and Review could be one modular monolith. They are split to exercise gRPC and event choreography across a real boundary.

### State machines (course pattern, data-driven)

| Aggregate | Lifecycle |
|---|---|
| `McpServer` | Discovered → UnderReview → Approved → Quarantined → (UnderReview) → Retired |
| `Grant` | Requested → PendingApproval → Active ⇄ Suspended → Revoked, plus Rejected |

- **No Expired state for grants.** The gateway checks `ExpiresAt` itself, so no scheduler is needed.
- **Same mechanism as the course:**
  - A seeded `*StageTransition` JSON table is loaded into a cached Stateless machine.
  - The domain only sees a factory delegate.
  - `ValidateTransition` runs before any change.
- **Every change takes an `IAuditableAction<TActionType>`** (`Blocks.Domain`: actor, when, action type, comment), the course's `IArticleAction` pattern. The action validates the transition, writes the audit entry and raises the domain event.
- **System actors are explicit actor types, not null users.** Examples are the drift job and the anomaly detector.

### Communication

**Integration events** are flat DTO records in `AgentGate.IntegrationEvents`:

| Publisher | Events |
|---|---|
| Registry | `AgentRegistered`, `ManifestSubmitted(serverId, hash, tools)`, `ServerApproved(serverId, url, hash, tools)`, `ServerQuarantined(serverId, reason, observedHash)` |
| Review | `ManifestReviewCompleted(serverId, hash, decision, risk)`, `GrantActivated(grantId, agentId, serverId, tools, expiresAt)`, `GrantSuspended`, `GrantRevoked` |
| Gateway | `ToolCallObserved`, published directly rather than through the outbox. It is an observation stream, so losing one is acceptable (recorded in an ADR).<br>`AnomalyDetected` |

**Rug-pull loop:**
1. The Registry drift job finds a hash mismatch and calls `server.Quarantine(systemAction)`.
2. Registry publishes `ServerQuarantined` and `ManifestSubmitted` (with the new hash).
3. The gateway blocks the server, and Review opens a re-review.
4. Review publishes `ManifestReviewCompleted`.
5. Registry re-approves and publishes `ServerApproved`.
6. The gateway re-pins the manifest.

**gRPC: one contract only.** `IRegistryService.GetServerSnapshot(serverId)` returns `{status, manifestHash, toolNames}`.
- Review calls it when **approving a manifest** and when **activating a grant**, to confirm the hash is still current at that moment.
- This follows the course rule that gRPC is only for checks that must be authoritative at a state transition.
- Code-first, using protobuf-net.Grpc.

### Gateway enforcement

Targets MCP spec version 2026-07-28; check at M1 whether it has changed.

**Routing.** A single `MapPost("/mcp/{serverId}")` calls YARP's `IHttpForwarder` directly, with the upstream address looked up in the gateway's local copy.

**Request inspection** (requests only, never responses):
- Only POST with `MCP-Protocol-Version: 2026-07-28`.
- Body capped at 256 KB: call `EnableBuffering`, parse, then reset `Body.Position`.
- Read `id`, `method` and `params.name`.
- The `Mcp-Method` and `Mcp-Name` headers must equal the body values, otherwise return 400 / -32020.

**Deny by default:**

| Method | Gateway behaviour |
|---|---|
| `server/discover` | Pass through |
| `tools/list` | **Answered by the gateway** from the pinned manifest, filtered to the agent's grants. The agent never sees descriptions that haven't been pinned, so the attack is prevented rather than only detected. |
| `tools/call` | Check the grant, then pass through |
| Anything else (resources, prompts, subscriptions) | 403 in v1 |

**Forwarding:**
- Strip the agent's `Authorization` header and attach the gateway's own upstream credential. The spec says a server must not accept tokens passed through from another client.
- Don't buffer or compress SSE responses on this route.
- Raise YARP's 100-second activity timeout.

**Anomaly detection (v1 has one detector).** A spike in denied calls from one agent:
1. the gateway blocks the agent locally straight away and publishes `AnomalyDetected`;
2. Review, which owns grants, performs the actual `Suspend`.

**Known gap (README threat model).** Changes in what a tool *does*, and instructions injected into tool *results*, are not covered. Catching them needs response inspection, which is v2.

---

## Course patterns: carried over vs fixed

**Carried over** (pattern registry: `docs/reference-model.md` in the course repo):
- Clean Architecture layers with feature slices.
- Aggregates split into a state file and a behaviour file.
- Value objects with a private constructor and a static `Create`.
- The repository acts as the unit of work.
- An audited entity-configuration base class.
- `PublishIntegrationEventOn{X}Handler`.
- Local copies of other services' data, built from events.
- Two-layer authorization: a role check plus a resource check.
- A single `GlobalExceptionHandler` (ProblemDetails); `DomainException` knows nothing about HTTP.
- Pipeline order: AssignActor → Validation → Logging.
- Fail-fast options, `MaxLength` constants, central package management, and `:verb` routes.

**Fixed:**

| Course gap | AgentGate |
|---|---|
| Events published **after** commit (`DispatchDomainEventsInterceptor.SavedChangesAsync`), so the database write and the publish aren't atomic | Dispatch in **`SavingChangesAsync`**, so the MassTransit EF **bus outbox** rows are committed in the same save.<br>IDs generated in code with `Guid.CreateVersion7()`.<br>Handlers read the aggregate carried by the event and never re-query it (rows are not inserted yet).<br>One Testcontainers test proves the change and its event commit together. |
| Each consumer handles duplicate messages differently | MassTransit EF **inbox** everywhere, plus a retry and redelivery policy |
| No tests at all | Domain unit tests, Testcontainers integration tests, the MassTransit test harness, and **architecture tests** (NetArchTest) that encode the layering rules |
| No observability | Aspire ServiceDefaults: OpenTelemetry traces, metrics and logs, plus health checks. One trace runs agent → gateway → MCP server, across RabbitMQ too. |
| No CI | GitHub Actions: build, test (Testcontainers on Ubuntu), format check |
| Symmetric JWT, audience not checked, gateway without auth | Keycloak RS256 tokens with audience validation |
| A different web framework in each service | **Minimal APIs everywhere** |
| Four different storage engines | **Postgres only**: one server, one database per service. Easier to justify. |

**Stack (licences checked October 2026):**
- **.NET 10 LTS + Aspire 13.x.** Postgres and RabbitMQ integrations are stable; Keycloak is in preview.
- **MassTransit pinned to 8.5.x.** v9 is commercial; v8 is Apache-licensed. Recorded in an ADR.
- **`Mediator` 3.x** (martinothamar; MIT, source-generated) instead of MediatR, because MediatR v13+ is commercial. The pipeline maps almost one-to-one, and the choice is a good licensing-awareness talking point.
- **Other libraries:** FluentValidation, Stateless, Mapster, protobuf-net.Grpc.
- **`ModelContextProtocol` C# SDK 2.x** for the demo server and agent. Set `ProtocolVersion` explicitly, otherwise the client falls back to `initialize`, which the gateway rejects.

---

## Repo layout

```
agentgate/
├── AgentGate.AppHost / AgentGate.ServiceDefaults   (Aspire)
├── src/BuildingBlocks/   Blocks.Domain, Blocks.Core, Blocks.Exceptions,
│                         Blocks.EntityFrameworkCore (fixed interceptor + outbox),
│                         Blocks.Messaging (MassTransit 8 + outbox/inbox),
│                         Blocks.AspNetCore, Blocks.Mediator,
│                         AgentGate.Abstractions (roles, system actor ids),
│                         AgentGate.IntegrationEvents, AgentGate.Grpc.Contracts
├── src/Services/{Registry,Review,Gateway,Hub}/{X}.API|Application|Domain|Persistence
├── demo/ DemoMcpServer (rug-pull toggle), DemoAgent (scripted scenarios)
├── web/  React console
├── tests/ {X}.Domain.Tests, {X}.IntegrationTests, ArchitectureTests
├── docs/adr/  (outbox, MassTransit 8 pin, Mediator, Postgres-only,
│               ToolCallObserved without outbox, Registry/Review split)
└── .claude/skills/  adapted copies of the course repo's 13 skills
```

**Port BuildingBlocks selectively; don't copy them wholesale.**
- Drop:
  - Redis, Hasura and multi-tenancy (unused).
  - `ThreadSafeMemoryCache`, which holds a sync lock across an `await`.
  - The dual MediatR/FastEndpoints `IDomainEvent`; keep the domain free of framework types.
- Fix `HttpContextProvider.GetClaimValues`, which ignores its argument.

**Port `.claude/skills/` from the course repo:**
- The skills: create-service, create-aggregate, create-feature-slice, domain-event-wiring, add-integration-event, consumer-patterns, persistence-patterns, error-handling, authorization-security, grpc-communication, service-infra-conventions, and article-state-machine (renamed `governance-state-machine`).
- Rename Articles to AgentGate and apply the fixes above, so future sessions build in the house style.

---

## Frontend: React console

**Stack:**
- Vite, React, TypeScript, TanStack Query + Router, Tailwind, shadcn/ui.
- Keycloak OIDC with PKCE.
- A fetch-based SSE client for the live feed. `EventSource` can't send an `Authorization` header.

**Avoiding the AI-template look:**
- **Tooling:** install Anthropic's official **`frontend-design`** plugin (from an interactive `claude` terminal, run `/plugin` and search `frontend-design`), and use it for every UI task.
- **One committed direction: a security operations console.**
  - Dark-first and dense.
  - No purple gradients and no marketing hero sections.
  - Monospace for IDs and hashes.
  - Colour reserved for risk and status signals.
  - Manifest diffs rendered like a code review.

**Screens (v1):**

| Screen | Contents |
|---|---|
| Inventory | Servers and tools, with status and risk |
| Approvals queue | Manifest diff, scanner findings, two-person approval |
| Live activity | Allowed and denied calls streaming in, anomalies highlighted |
| Server / agent detail | Audit timeline |

---

## Milestones

Each milestone is demoable. Tests and CI start at M0. Estimate: about 8 weeks part-time.

| # | Scope | Demo / exit criteria |
|---|---|---|
| **M0** (wk 1) | Repo setup: `global.json`; `Directory.Build.props` (nullable on, warnings as errors); central package management.<br>AppHost with Postgres, RabbitMQ and the Keycloak realm. ServiceDefaults.<br>Ported BuildingBlocks with the fixed interceptor.<br>ArchitectureTests, CI workflow, ported skills. | Aspire dashboard all green; CI green |
| **M1** (wk 2), *riskiest first* | DemoMcpServer.<br>DemoAgent using Keycloak client credentials.<br>Gateway: JWT auth, request parsing and checks, a hard-coded grant table, forwarding through `IHttpForwarder`. | Allowed call passes; tool without a grant → 403; header/body mismatch → 400; one trace across agent → gateway → server |
| **M2** (wk 3–4) | **Registry:** Agent and McpServer aggregates; state machine; audit trail; outbox; drift job; a temporary admin approve endpoint.<br>**Manifest hashing:** sort tools by name; SHA-256 over name, title, description, inputSchema, outputSchema and annotations, with JSON keys sorted.<br>**Gateway:** consumes `ServerApproved` and `ServerQuarantined`, and answers `tools/list` from the pinned manifest. | Flip the rug-pull switch → quarantined within 30 s → calls blocked; the agent only ever saw pinned descriptions |
| **M3** (wk 4–5) | **Review:** heuristic scanner (hidden-instruction patterns, overly broad schemas, suspicious names); ManifestReview and Grant aggregates; four-eyes approval; gRPC `GetServerSnapshot` check; inbox.<br>**Registry:** approval moves from the temporary endpoint to a consumer that calls the same `server.Approve(action)`. | A poisoned server is flagged high risk; one person can't give both approvals; approving against an outdated manifest hash is rejected |
| **M4** (wk 6–7) | Hub projections, SSE feed, timeline. React console with the 4 screens. | The scripted scenario plays live in the UI |
| **M5** (wk 8) | Denied-call spike → `AnomalyDetected` → Review suspends the grant.<br>`ClaudeRiskScanner` (use the `claude-api` skill).<br>README: architecture diagram, threat model including the v2 gaps, ADRs, a 3-minute demo script and a GIF. | Full demo end to end |

**M2 as built** (differences from the scope above):
- `McpServer` lifecycle is trimmed to what M2 needs: `Register` creates it in UnderReview; UnderReview → Approve → Approved → Quarantine → Quarantined → Approve → Approved. Discovered, SubmitForReview and Retired come with Review in M3.
- The `Agent` aggregate is deferred to M3, where grants need it; agents are still the pre-created Keycloak clients.
- The gateway's server catalog is in memory and built only from events, so a restarted gateway fails closed (unknown server → 404) until M3 adds a bootstrap read from Registry.
- Registry applies EF migrations at startup in Development only.

**M3 as built** (differences from the scope above):
- **Server lifecycle.** `McpServer` gains `Rejected` (UnderReview / Quarantined → Reject → Rejected). `Register` and the drift quarantine both raise `ManifestSubmitted`, so Review opens a review in the same commit as the change. Discovered and Retired are still deferred.
- **Agent aggregate.** It exists now, with `POST /agents` and `AgentRegistered`. Its `ClientId` is the Keycloak client the gateway sees as `azp`.
- **Approval is a consumer.** `ManifestReviewCompletedConsumer` replaced the temporary approve endpoint. `Approve`/`Reject` take the reviewed hash and refuse a decision about a manifest that is no longer pending.
- **Four-eyes on manifest reviews too**, not only on grants: Open → PartiallyApproved → Approved needs two different reviewers. One reviewer is enough to reject. A high-risk manifest needs a written reason from each approver.
- **When gRPC is called.** Only the completing approval of a review or grant calls `GetServerSnapshot`; earlier approvals change nothing agents see. The snapshot carries both the pinned and the pending hash.
- **Service identity for gRPC (ADR 0004).** Review calls Registry with its own client-credentials token (role `snapshot-reader`). Registry serves gRPC on a separate HTTP/2-only port.
- **Gateway grants.** They are an in-memory local copy fed by `GrantActivated/Suspended/Revoked`, with `ExpiresAt` checked at use. `GatewayOptions.Grants` is gone.
- **Demo reviewers.** The scripted four-eyes demo uses two reviewer service accounts (`demo-reviewer-1`, `demo-reviewer-2`). Human reviewers sign in through the console in M4.
- **Still deferred:** the gateway's bootstrap read after a restart. It still fails closed: no servers and no grants until events re-announce them. This belongs with M4's read models.

**M4 as built** (differences from the scope above):
- **Hub** is a read model only. It holds plain rows (servers, agents, grants, timeline, tool calls) built by consumers with the EF inbox. Each governance event is filed on the timeline of every subject it concerns, with a severity.
- **Ordering.** A status from an older event never overwrites a newer one; this matters because queues don't preserve order across event types.
- **SSE.** `GET /activity/stream` fans out from memory, with one bounded buffer per subscriber, so a slow browser loses rows rather than slowing the consumers. The stream opens with a heartbeat, because neither browsers nor test clients see a response until something is written, and sends another every 15 s while idle. One Hub replica only.
- **Two new events feed the Hub.**
  - `ToolCallObserved`: the gateway publishes it from a bounded background queue, without an outbox (ADR 0005).
  - `ManifestReviewOpened`: Review publishes it, so the inventory shows a pending review's risk before anyone decides.
- **Console.**
  - `web/` holds Vite, React and TypeScript, TanStack Query and Router, and Tailwind. The shadcn-style primitives are written in the repo, because the shadcn CLI needs its registry.
  - The `frontend-design` plugin wasn't available where M4 was built, so the design direction above was followed by hand.
  - Sign-in is Keycloak authorization code + PKCE with `oidc-client-ts`. Roles and `sub` come from the access token, the same claims the APIs authorize on.
  - The Vite dev server proxies `/api/{registry,review,hub}`, so the browser sees one origin and no service needs CORS.
  - Approvals and decisions go to Review; everything else is read from the Hub.
- **Gateway bootstrap**, deferred from M2/M3, is done.
  - A restarted gateway reads `/internal/gateway/servers` from Registry and `/internal/gateway/grants` from Review, as its own service identity (role `gateway-sync`).
  - Snapshot rows carry the time of their last change and go through the same overwrite-if-newer rules as events.
- **AppHost.** Every service's endpoints are declared in `AppHost.cs` instead of machine-local launch profiles. Without them, service references resolved to nothing on a fresh clone.
- **Not done:** a production build of the console behind a single origin (YARP or static hosting). The dev server's proxy is the only front door for now.

**Out of scope for v1** (listed as v2 in the README):
- Response inspection: instructions injected into results, and changes in tool behaviour.
- MCP resources, prompts and subscriptions.
- Multiple gateway replicas with shared rate state in Redis.
- Automating the Keycloak admin API.
- A Go data plane.

---

## Verification

**Every milestone:**
- `dotnet test` passes locally and in CI (unit, Testcontainers integration and architecture tests).
- `aspire run` on the AppHost shows every resource healthy.

**Key automated tests:**
- Outbox atomicity: the change and its event commit or roll back together.
- Inbox de-duplication: the same message delivered twice changes state once.
- State machine: an illegal transition throws `DomainException`, which becomes a 400.
- Gateway: the header/body mismatch combinations, deny-by-default methods, and a tool without a grant.
- Drift: quarantine, then calls blocked.
- Four-eyes: one person can't give both approvals.
- gRPC: approval against an outdated hash is rejected.

**End-to-end:**
- `DemoAgent --scenario rugpull|anomaly`, checked by hand in the React console.
- One distributed trace in the Aspire dashboard covering HTTP, RabbitMQ and gRPC.

