namespace Blocks.Domain;

// insight - the domain doesn't know HTTP exists; mapping a broken rule to a status code happens once, at the edge
public class DomainException(string message, Exception? innerException = null)
    : Exception(message, innerException);
