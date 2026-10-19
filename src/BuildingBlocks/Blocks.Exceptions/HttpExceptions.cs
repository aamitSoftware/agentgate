using System.Net;

namespace Blocks.Exceptions;

// insight - the exception carries its own status; the global handler reads StatusCode instead of keeping a lookup table,
// so a new subclass needs no handler change.
public class HttpException(HttpStatusCode statusCode, string? message = null, Exception? innerException = null)
    : Exception(string.IsNullOrEmpty(message) ? statusCode.ToString() : message, innerException)
{
    public HttpStatusCode HttpStatusCode { get; } = statusCode;
    public int StatusCode => (int)HttpStatusCode;
}

public class BadRequestException(string message, Exception? innerException = null)
    : HttpException(HttpStatusCode.BadRequest, message, innerException);

public class NotFoundException(string message, Exception? innerException = null)
    : HttpException(HttpStatusCode.NotFound, message, innerException);

public class UnauthorizedException(string? message = null)
    : HttpException(HttpStatusCode.Unauthorized, message);

public class ConflictException(string message, Exception? innerException = null)
    : HttpException(HttpStatusCode.Conflict, message, innerException);
