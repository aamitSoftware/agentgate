# 0001: Dispatch domain events before the commit, publish through the outbox

**Status:** accepted

## Context
The reference course dispatched domain events in `SavedChangesAsync`, after the commit. Integration events were then published to RabbitMQ in a separate step. A crash or a broker outage between the two lost the event while the state change stayed committed. This is the dual-write problem.

## Decision
- `DispatchDomainEventsInterceptor` dispatches in **`SavingChangesAsync`**, before the commit.
- `PublishIntegrationEventOn{X}` handlers call `IPublishEndpoint.Publish`. Behind it sits the MassTransit EF **bus outbox**, which adds outbox rows to the same DbContext, so the state change and its outgoing events commit in one transaction. A MassTransit background service delivers the rows afterwards.
- Consumers use the EF **inbox**: each consumed MessageId is recorded in the consumer's transaction, so a redelivered message is a no-op.

## Consequences
- **IDs:** they are generated in code (`Guid.CreateVersion7()`), because an event raised before the insert must already carry the aggregate's id.
- **Handlers:** they must not call `SaveChanges` or re-query the aggregate. Rows aren't inserted yet, so they read from the aggregate the event carries.
- **Sync saves:** `SaveChanges` with pending events throws instead of silently dropping them.
- **Proof:** `DispatchDomainEventsInterceptorTests` shows a handler's writes committing and rolling back with the triggering change.
- **One interceptor:** the course's two variants (post-save vs transactional) collapse into one.
- **No publisher abstraction:** handlers are plain `IDomainEventHandler<T>` in a framework-free domain kernel, with no `IDomainEventPublisher`.
