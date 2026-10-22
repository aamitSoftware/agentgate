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
- **Console (M4).** Inventory, approvals with a manifest diff, live activity, and server and agent detail. Sign-in with Keycloak PKCE.
- **Platform.**
  - .NET 10 and Aspire.
  - Transactional outbox and inbox.
  - Architecture tests, Testcontainers integration tests, Playwright, and CI for both stacks.
- **Decisions:** six ADRs in `docs/adr`.

Known gaps are listed under "Not covered in v1" in the README's threat model.
