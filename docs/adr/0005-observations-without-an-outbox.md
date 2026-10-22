# 0005: Tool-call observations are published without an outbox, off the request path

**Status:** accepted

## Context
- **What it's for.** The gateway decides on every agent request. Each decision should reach the Hub's live activity feed and, from M5, the anomaly detector.
- **No outbox available.** The gateway owns no database. Its state is a local copy rebuilt from events, so there is no transaction an outbox could join.
- **Publishing inline costs the agent.** A broker round trip would sit on every MCP request, and a broker outage would turn into failed tool calls.

## Decision
- **Direct publish.** `ToolCallObserved` goes out with `IBus.Publish`, with no outbox.
- **Off the request path.** The request path only writes to a bounded in-memory channel (10,000 entries). A background loop publishes from it.
- **When the channel is full, the oldest observation is dropped**, never the request. The drop count is logged when publishing fails.
- **Allows and denials are both observed.** That includes requests for servers the gateway doesn't know.

## Consequences
- **Losses are possible.** Observations can be lost on a crash, a full channel or a broker outage. The feed may then miss rows, but no governance decision is lost: grants, quarantines and reviews still go through outboxes.
- **Gaps don't matter to the M5 detector.** It counts denials over a window, so a gap delays detection slightly but cannot cause a false suspension.
- **Not an audit log.** An audit trail of tool calls would need a durable store at the gateway; that's v2.
