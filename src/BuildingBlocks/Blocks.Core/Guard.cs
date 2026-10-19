using Blocks.Exceptions;

namespace Blocks.Core;

public static class Guard
{
    public static T AgainstNull<T>(T? value, string parameterName) =>
        value ?? throw new ArgumentNullException(parameterName);

    public static void ThrowIfFalse(bool condition, string message)
    {
        if (!condition)
            throw new ArgumentException(message);
    }

    public static T NotFound<T>(T? value) where T : class =>
        value ?? throw new NotFoundException($"{typeof(T).Name} not found");
}

public static class GuardExtensions
{
    public static T OrThrowNotFound<T>(this T? value, string? message = null) where T : class =>
        value ?? throw new NotFoundException(message ?? $"{typeof(T).Name} not found");
}
