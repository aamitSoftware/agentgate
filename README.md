# AgentGate

**Governance control plane for AI agents' tool access over MCP.**

AI agents increasingly act through MCP servers: CRM lookups, ticketing, databases. An MCP server that is trusted today can quietly change its tool descriptions tomorrow (a "rug pull"). A prompt-injected agent can start calling tools it was never meant to use. AgentGate puts a lifecycle around that access:

```
discover → risk-scan → approve (four-eyes) → pin manifest → enforce per-agent grants at a gateway
        → detect drift / anomalies → quarantine server / suspend grant → re-review
```

## Architecture

| Service | Owns |
|---|---|
| **Registry** | MCP servers, pinned tool manifests (SHA-256), agents as nonhuman identities, drift detection |
| **Review** | Risk scans, four-eyes approval, grants (agent × server × tools) |
| **Gateway** | MCP enforcement proxy (YARP): answers `tools/list` from the pinned manifest, checks grants on `tools/call`, detects anomalies |
| **Hub** | Read model built only from events: inventory, who can call what, audit timeline, live activity feed |

**How the services talk:**
- **Events:** RabbitMQ via MassTransit, with a transactional outbox and inbox.
- **gRPC:** one code-first call, used only where a check must be authoritative at the moment of a state transition.
- **Identity:** Keycloak (OIDC); agents authenticate with client credentials.

**Stack:** .NET 10, Aspire, Postgres, OpenTelemetry.

**Further reading:**
- Design: [docs/plan.md](docs/plan.md)
- Decisions: [docs/adr](docs/adr)

## Status

| Milestone | Scope | State |
|---|---|---|
| M0 | Repo, BuildingBlocks, Aspire AppHost, architecture tests, CI | done |
| M1 | Gateway enforcement + demo MCP server and agent | next |
| M2 | Registry: manifest pinning, drift → quarantine | |
| M3 | Review: risk scan, four-eyes, grants, gRPC check | |
| M4 | Hub + React console | |
| M5 | Anomaly auto-suspend, Claude risk scanner, demo | |

## Run

Requirements: .NET 10 SDK, plus Docker Desktop for the AppHost's containers (Postgres, RabbitMQ, Keycloak).

```bash
dotnet test
dotnet run --project src/AgentGate.AppHost
```
