---
name: error-handling
description: Exception hierarchy, guards and error flow in AgentGate - DomainException in the domain, HttpException subclasses (BadRequest/NotFound/Unauthorized) that carry their own status, FluentValidation ValidationException from the Mediator pipeline, and the single GlobalExceptionHandler (IExceptionHandler) that writes RFC 9457 ProblemDetails. Use when throwing an error, adding an exception type, writing a not-found check, or considering a try/catch.
---

# Error Handling

Three rules:

1. Exceptions **self-describe their status**: an `HttpException` carries its `HttpStatusCode`.
2. **One place translates** exceptions to HTTP: `GlobalExceptionHandler` (Blocks.AspNetCore), registered
   by `AddApiDefaults()` and activated by `app.UseApiDefaults()` (first in the pipeline).
3. Domain, handler and endpoint code **just throws**. No `Result<T>`, `OneOf`, `ErrorOr`, no
   per-endpoint try/catch.

## Which exception where

| You are in | Throw | Status |
|---|---|---|
| Domain (aggregate, value object, state machine) - rule broken | `DomainException` (Blocks.Domain) | 400 |
| Handler/endpoint - missing resource | `NotFoundException`, or a guard (below) | 404 |
| Handler/endpoint - bad request not expressible as a validator | `BadRequestException` | 400 |
| Validation | nothing - the validator fails, `ValidationBehavior` throws `ValidationException` | 400 + `errors` |
| Argument contract | `Guard.AgainstNull` / `Guard.ThrowIfFalse` (`ArgumentException`) | 400 |
| No `sub` claim | `IClaimsProvider.GetActorId()` throws `UnauthorizedAccessException` | 401 |
| Anything else | let it bubble | 500, generic title |

A broken invariant is the caller's fault (illegal transition, same reviewer twice), hence 400. The domain
never references `Blocks.Exceptions`.

## The handler (reference - don't add per-endpoint mapping)

```csharp
var statusCode = exception switch
{
    HttpException httpException => httpException.StatusCode,
    ValidationException or DomainException or ArgumentException => StatusCodes.Status400BadRequest,
    UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
    _ => StatusCodes.Status500InternalServerError
};
```

- 5xx: logged with the exception; the client gets `"An unexpected error occurred."` - never the message
  (it can leak SQL, paths). The trace has the details.
- `ValidationException`: `errors` extension grouped by property.
- Client aborted (`OperationCanceledException` + `RequestAborted`): 499, not logged.

Response (`application/problem+json`, ASP.NET adds `traceId`):

```json
{ "status": 400, "title": "Action Approve is not allowed while the grant is Revoked.", "traceId": "00-..." }
{ "status": 400, "title": "Validation failed: ...", "errors": { "Comment": ["..."] } }
```

## Guards instead of inline null checks

```csharp
var grant = await grants.GetByIdOrThrowAsync(command.GrantId, ct);   // repository: 404
var dto = (await query.SingleOrDefaultAsync(ct)).OrThrowNotFound();  // fluent: 404
var server = Guard.NotFound(await repository.GetByIdAsync(id, ct));  // static: 404
Guard.ThrowIfFalse(hash.Length == 64, "Manifest hash must be a SHA-256 hex string.");   // 400
```

Pick the one that reads best; don't hand-write `if (x is null) throw ...`. Guards live in `Blocks.Core`,
so they are for Application/API code - the domain throws `DomainException` itself.

## Adding an exception type

One edit - the handler matches the `HttpException` base, so no switch arm is needed:

```csharp
// Blocks.Exceptions/HttpExceptions.cs
public class ConflictException(string message, Exception? innerException = null)
    : HttpException(HttpStatusCode.Conflict, message, innerException);
```

A domain-rule variant subclasses `DomainException` (still 400, lives in the service's Domain):
`public sealed class FourEyesViolationException(string message) : DomainException(message);`

## try/catch only at I/O boundaries

Allowed where something outside the domain can fail and the caller must keep going:
- the Registry drift job's per-server `tools/list` fetch (one unreachable server must not stop the loop -
  catch, log, continue);
- the `ClaudeRiskScanner` HTTP call (fall back or fail the scan explicitly);
- Gateway forwarding (`IHttpForwarder` returns a `ForwarderError` - map it, don't catch around it).

Everywhere else - handlers, domain, consumers, endpoints - throw and let the handler (HTTP) or
MassTransit retry/error queue (messaging) deal with it. Consumers: `DomainException` is not retried.

## Not this skill

How validation is wired (`service-infra-conventions`), validators per feature (`create-feature-slice`).
