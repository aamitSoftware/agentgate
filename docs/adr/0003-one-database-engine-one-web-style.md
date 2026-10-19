# 0003: One database engine and one endpoint style across services

**Status:** accepted

## Context
The reference course used four storage engines (SQL Server, Postgres, Redis, Mongo) and three endpoint frameworks (Minimal APIs, Carter, FastEndpoints), one per service. That was a teaching device. In a product it multiplies operational and cognitive cost and buys nothing.

## Decision
- **Postgres for every service.** Each service gets its own database on one server, and a service owns its database exclusively.
- **Minimal APIs everywhere.** Endpoints stay thin and send commands through Mediator.
- **Single correlation mechanism.** OpenTelemetry's W3C trace context replaces the custom `X-Correlation-ID` middleware. It already flows over HTTP, gRPC and MassTransit.

## Consequences
Every service looks the same, so the skills in `.claude/skills/` describe one shape. The architectural boundaries (database per service, events vs gRPC) are unchanged.
