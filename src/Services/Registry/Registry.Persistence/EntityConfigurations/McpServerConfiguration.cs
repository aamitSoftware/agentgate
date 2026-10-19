using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Registry.Persistence.EntityConfigurations;

public sealed class McpServerConfiguration : AuditedEntityConfiguration<McpServer>
{
    // insight - stored as the canonical JSON only (text, not jsonb: jsonb would reorder keys);
    // the hash is recomputed on load, so it can never disagree with the tools it describes
    private static readonly ValueConverter _manifestConverter =
        new ValueConverter<ToolManifest, string>(manifest => manifest.ToolsJson, json => ToolManifest.Create(json));

    public override void Configure(EntityTypeBuilder<McpServer> builder)
    {
        base.Configure(builder);

        builder.Property(server => server.Name).HasMaxLength(MaxLength.C64).IsRequired();
        builder.HasIndex(server => server.Name).IsUnique();
        builder.Property(server => server.Url).HasMaxLength(MaxLength.C2048).IsRequired()
            .HasConversion(url => url.Value, value => ServerUrl.Create(value));
        builder.Property(server => server.Status).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(server => server.PinnedManifest).HasConversion(_manifestConverter);
        builder.Property(server => server.PendingManifest).HasConversion(_manifestConverter);

        builder.HasMany(server => server.AuditEntries).WithOne()
            .HasForeignKey("McpServerId").IsRequired().OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ServerAuditEntryConfiguration : EntityConfiguration<ServerAuditEntry>
{
    public override void Configure(EntityTypeBuilder<ServerAuditEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("ServerAuditEntries");

        builder.Property(entry => entry.Action).HasMaxLength(MaxLength.C32).IsRequired();
        builder.Property(entry => entry.ActorId).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(entry => entry.Comment).HasMaxLength(MaxLength.C512);
        builder.Property(entry => entry.FromStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(entry => entry.ToStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
    }
}
