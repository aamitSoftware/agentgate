# 0004: The snapshot gRPC call is authenticated as a service and served on its own HTTP/2 port

**Status:** accepted

## Context
- **The call.** Review calls Registry's `GetServerSnapshot` at two transitions: the final approval of a manifest review and the activating approval of a grant. It is the only synchronous call between services.
- **Authentication gap.** Every HTTP endpoint validates a Keycloak token. The gRPC endpoint needs a rule too, otherwise anything on the network can read server state.
- **Whose token?** Forwarding the reviewer's token would make Registry trust a token issued for Review (the same confused-deputy problem the gateway avoids upstream). It would also make the call's permissions depend on a human's roles.
- **Transport.** gRPC needs HTTP/2. A cleartext Kestrel endpoint speaks HTTP/1.1 or HTTP/2, not both: `Http1AndHttp2` only negotiates HTTP/2 through TLS ALPN (checked: prior-knowledge h2c gets no response). Aspire records an endpoint's `http2` transport in its manifest but only passes the port to the project, so it doesn't configure the protocol.

## Decision
- **Service identity.** Review has its own confidential Keycloak client, `agentgate-review`:
  - client credentials;
  - an audience mapper for `agentgate-registry`;
  - one realm role, `snapshot-reader`.
- **Authorization at Registry.** The endpoint `MapGrpcService<RegistryGrpcService>()` requires the `snapshot-reader` policy. No human role grants it.
- **Token handling in Review.** `ServiceTokenProvider` fetches and caches the token until 30 s before expiry. `ServiceTokenHandler` attaches it to the code-first gRPC client. The client never forwards a user's token.
- **Two ports.** Under the AppHost, Registry gets explicit `http` (REST, HTTP/1.1) and `grpc` endpoints, passed as `HTTP_PORT` and `GRPC_PORT`. Registry binds `GRPC_PORT` as HTTP/2-only. Review resolves it as `http://_grpc.registry` through service discovery.
- **Failure is "cannot verify".** Any `RpcException` (unreachable, unauthorized, unknown server) becomes a 503 and the transition does not happen.

## Consequences
- **Registry ignores launch profiles.** Its endpoints are defined in `AppHost.cs` (`ExcludeLaunchProfile`), so a local `launchSettings.json` doesn't change them.
- **One more dev secret.** The realm JSON and the AppHost now hold Review's client secret, like the demo clients'.
- **No Duende dependency.** `Duende.AccessTokenManagement` would replace the token provider, but one call site doesn't justify another licence to track.
- **Still no TLS between services.** The tokens are bearer credentials on the internal network. A deployment adds TLS or a mesh; the authorization rule doesn't change.
