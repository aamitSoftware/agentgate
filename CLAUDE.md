# AgentGate

Governance control plane for AI agents' MCP tool access. Design and milestones: `docs/plan.md`. Decisions: `docs/adr/`. Patterns and templates: `.claude/skills/` — check them before adding a service, aggregate, slice, event, consumer or gRPC call. Console (web/) work follows `.claude/skills/console-ui` and `web/DESIGN.md`; the vendored design skills it names (impeccable, interface-design, shadcn, web-design-guidelines, react-best-practices, composition-patterns, accessibility) carry their upstream licenses in each folder.

## Layout
- `src/BuildingBlocks/Blocks.*` — shared primitives (Domain, Core, Exceptions, EntityFrameworkCore, Mediator, AspNetCore, Messaging)
- `src/BuildingBlocks/AgentGate.*` — cross-service contracts: `IntegrationEvents`, `Grpc.Contracts` (the one gRPC call), `Abstractions` (roles, system actors)
- `src/Services/{Registry,Review,Gateway,Hub}/{X}.API|Application|Domain|Persistence` (added per milestone)
- `src/AgentGate.AppHost`, `src/AgentGate.ServiceDefaults` — Aspire (every service's endpoints are declared there, not in launch profiles)
- `web/` — React console (Vite, TanStack Query/Router, Tailwind); talks to services through the dev server's `/api/{service}` proxy
- `tests/` — architecture tests, unit tests, Testcontainers integration tests

## Rules
- **Git: commits are authored by the repo owner only. Never add `Co-Authored-By` trailers or any AI attribution to commits or PRs.**
- Domain projects reference only `Blocks.Domain` (enforced by `tests/AgentGate.ArchitectureTests`). Add a rule there for every new boundary.
- Domain events are dispatched before commit (ADR 0001): handlers never call `SaveChanges` and never re-query the aggregate.
- Every state change takes an `IAuditableAction`; provenance (`ActorId`) is never bound from the request body.
- gRPC only for authoritative checks at a state transition; everything else is replicated by events.
- Minimal APIs + Mediator (scoped lifetime) everywhere; Postgres only; central package versions (no `Version=` in csproj); MassTransit stays on 8.5.x (ADR 0002).
- No service-layer classes, no single-implementation interfaces for repositories, no `Helpers/`/`Utils/` folders.
- Naming: `_camelCase` private fields, no abbreviations (`request`, not `req`).

## Commands
```bash
dotnet build
dotnet test
dotnet format --verify-no-changes
dotnet run --project src/AgentGate.AppHost   # needs Docker; console on http://localhost:5173
cd web && npm test && npm run build && npm run screenshots
```
