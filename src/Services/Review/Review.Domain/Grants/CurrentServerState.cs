namespace Review.Domain.Grants;

/// <summary>What Registry says about the server right now (GetServerSnapshot), as the facts a grant activation needs.</summary>
public sealed record CurrentServerState(bool IsApproved, IReadOnlyCollection<string> PinnedToolNames);
