namespace Hub.Persistence.EntityConfigurations;

// ids come from the events (the owning service generated them), never from the database

public sealed class ServerViewConfiguration : IEntityTypeConfiguration<ServerView>
{
    public void Configure(EntityTypeBuilder<ServerView> builder)
    {
        builder.HasKey(server => server.Id);
        builder.Property(server => server.Id).ValueGeneratedNever();
        builder.Property(server => server.Name).HasMaxLength(MaxLength.C64).IsRequired();
        builder.HasIndex(server => server.Name).IsUnique();
        builder.Property(server => server.Url).HasMaxLength(MaxLength.C2048);
        builder.Property(server => server.Status).HasMaxLength(MaxLength.C32).IsRequired();
        builder.Property(server => server.PinnedHash).HasMaxLength(MaxLength.C64);
        builder.Property(server => server.PendingHash).HasMaxLength(MaxLength.C64);
        builder.Property(server => server.OpenReviewRisk).HasMaxLength(MaxLength.C16);
        builder.Property(server => server.LastReviewRisk).HasMaxLength(MaxLength.C16);
    }
}

public sealed class AgentViewConfiguration : IEntityTypeConfiguration<AgentView>
{
    public void Configure(EntityTypeBuilder<AgentView> builder)
    {
        builder.HasKey(agent => agent.Id);
        builder.Property(agent => agent.Id).ValueGeneratedNever();
        builder.Property(agent => agent.Name).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(agent => agent.ClientId).HasMaxLength(MaxLength.C64).IsRequired();
        builder.HasIndex(agent => agent.ClientId).IsUnique();
        builder.Property(agent => agent.Owner).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(agent => agent.Purpose).HasMaxLength(MaxLength.C512).IsRequired();
    }
}

public sealed class GrantViewConfiguration : IEntityTypeConfiguration<GrantView>
{
    public void Configure(EntityTypeBuilder<GrantView> builder)
    {
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.Id).ValueGeneratedNever();
        builder.Property(grant => grant.AgentClientId).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(grant => grant.ServerName).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(grant => grant.Status).HasMaxLength(MaxLength.C32).IsRequired();
        builder.HasIndex(grant => grant.AgentId);
        builder.HasIndex(grant => grant.ServerId);
    }
}

public sealed class TimelineEntryConfiguration : IEntityTypeConfiguration<TimelineEntry>
{
    public void Configure(EntityTypeBuilder<TimelineEntry> builder)
    {
        builder.ToTable("Timeline");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.Kind).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(entry => entry.Summary).HasMaxLength(MaxLength.C1024).IsRequired();
        builder.Property(entry => entry.Severity).HasMaxLength(MaxLength.C16).IsRequired();
        builder.Property(entry => entry.ActorId).HasMaxLength(MaxLength.C128);
        builder.HasIndex(entry => entry.OccurredOn);
        builder.HasIndex(entry => entry.ServerId);
        builder.HasIndex(entry => entry.AgentId);
    }
}

public sealed class ToolCallViewConfiguration : IEntityTypeConfiguration<ToolCallView>
{
    public void Configure(EntityTypeBuilder<ToolCallView> builder)
    {
        builder.ToTable("ToolCalls");
        builder.HasKey(call => call.Id);
        builder.Property(call => call.Id).ValueGeneratedNever();
        builder.Property(call => call.AgentClientId).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(call => call.ServerName).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(call => call.Method).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(call => call.ToolName).HasMaxLength(MaxLength.C128);
        builder.Property(call => call.Decision).HasMaxLength(MaxLength.C16).IsRequired();
        builder.Property(call => call.Reason).HasMaxLength(MaxLength.C512);
        builder.Property(call => call.TraceId).HasMaxLength(MaxLength.C32);
        builder.HasIndex(call => call.OccurredOn);
        builder.HasIndex(call => new { call.AgentClientId, call.OccurredOn });
        builder.HasIndex(call => new { call.ServerName, call.OccurredOn });
    }
}
