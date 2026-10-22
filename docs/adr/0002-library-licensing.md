# 0002: Stay on open-source licences: MassTransit 8.5.x, Mediator instead of MediatR

**Status:** accepted

## Context
- **MassTransit:** v9 moved to a commercial licence. v8 stays Apache-2.0 and is still patched; 8.5.11 is current.
- **MediatR:** v13 and later are commercial. A free community tier exists, but it needs a registered key.

## Decision
- **MassTransit:** pin 8.5.x in `Directory.Packages.props`.
- **Mediator:** use the `Mediator` library (martinothamar, MIT, source-generated). Its `IRequest`/`IPipelineBehavior`/`INotification` model maps almost one-to-one onto MediatR, so the pipeline from the reference course (assign actor → validate → log) carries over unchanged.
- **gRPC:** `protobuf-net.Grpc` 1.3.x (Apache-2.0) for the one code-first contract, `IRegistryService`. It uses its own `[Service]`/`[Operation]` attributes, so the contracts project doesn't need `System.ServiceModel`.
- **Claude API:** the official `Anthropic` NuGet package (Apache-2.0), for the model-based risk scan (ADR 0006).
- **Domain events:** they don't go through Mediator at all (see ADR 0001), so the domain kernel references no third-party package.

## Consequences
- **Upgrade path:** moving to MassTransit 9 later is a licence decision, not a code rewrite.
- **Mediator lifetime:** its source generator defaults to singleton handlers. Each service sets `ServiceLifetime.Scoped`, because handlers depend on the scoped DbContext.
