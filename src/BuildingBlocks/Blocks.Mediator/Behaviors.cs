using System.Diagnostics;
using Blocks.Core;
using Blocks.Domain;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Blocks.Mediator;

/// <summary>Stamps the caller ('sub') onto auditable commands. Provenance is never trusted from the request body.</summary>
public sealed class AssignActorBehavior<TMessage, TResponse>(IClaimsProvider claimsProvider)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        // system actors (consumers, background jobs) set ActorId explicitly; only fill it when empty
        if (message is IAuditableAction { ActorId: null or "" } action && claimsProvider.TryGetActorId() is { } actorId)
            action.ActorId = actorId;

        return next(message, cancellationToken);
    }
}

public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TMessage>(message);
        var results = await Task.WhenAll(validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results.SelectMany(result => result.Errors).ToList();
        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(message, cancellationToken);
    }
}

/// <summary>Traces carry timings already; this only flags slow handlers in the logs.</summary>
public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    private static readonly TimeSpan _slowThreshold = TimeSpan.FromSeconds(1);

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var response = await next(message, cancellationToken);

        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        if (elapsed > _slowThreshold)
            logger.LogWarning("Slow handler: {Message} took {ElapsedMs} ms", typeof(TMessage).Name, (int)elapsed.TotalMilliseconds);

        return response;
    }
}
