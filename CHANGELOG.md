# Changelog

## 1.0.0

The first complete release: the governance lifecycle from registration to enforcement, both demo scenarios, and the console.

- **Gateway (M1, M4, M5).** The MCP enforcement proxy.
  - Answers `tools/list` from the pinned manifest and checks per-agent grants on `tools/call`.
  - Denies by default and rejects header/body smuggling.
  - Blocks agents whose denied calls spike.
  - Rebuilds its state from Registry and Review after a restart.
- **Registry (M2, M3).**
  - MCP servers with SHA-256-pinned manifests.
  - Agents as nonhuman identities.
  - A drift detector that quarantines a server on a rug pull.
  - Approval only through a completed review.
- **Review (M3, M5).**
  - Heuristic risk scan, plus an optional Claude scan.
  - Four-eyes approval for manifests and grants.
  - The one authoritative gRPC check at final approval.
  - Grant lifecycle with automatic suspension on anomalies.
- **Hub (M4).** An event-built read model and a live SSE activity feed.
- **Console (M4, M6).** Sign-in with Keycloak PKCE.
  - **Screens:** Overview, Approvals (queue, grant requests, history), a review with findings and a manifest diff, Servers, Agents, Grants, live activity, and server and agent detail.
  - **Built on:** shadcn/ui on Radix with light and dark themes, a ⌘K palette and an auditor read-only mode.
  - **Safety:** confirmed destructive actions.
  - **Accessibility:** WCAG AA contrast, checked by axe in CI.
  - **Design:** a documented design system (`web/DESIGN.md`) and vendored design skills for future UI work (ADR 0007).
- **Platform.**
  - .NET 10 and Aspire; a fresh clone runs with `dotnet run --project src/AgentGate.AppHost`.
  - Transactional outbox and inbox.
  - Architecture tests, Testcontainers integration tests, Playwright, and CI for both stacks.
- **Decisions:** seven ADRs in `docs/adr`.

Known gaps are listed under "Not covered in v1" in the README's threat model.
