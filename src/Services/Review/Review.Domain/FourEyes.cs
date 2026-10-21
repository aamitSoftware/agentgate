namespace Review.Domain;

/// <summary>The two-person rule shared by manifest reviews and grants. Not authorization: a domain invariant.</summary>
internal static class FourEyes
{
    public static void EnsureSecondApprover(string? firstApproverId, IAuditableAction action)
    {
        if (string.Equals(firstApproverId, action.ActorId, StringComparison.Ordinal))
            throw new DomainException($"Four-eyes: '{action.ActorId}' already gave the first approval; a different reviewer must give the second.");
    }
}
