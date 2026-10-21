namespace Review.Persistence.EntityConfigurations;

public sealed class GrantConfiguration : AuditedEntityConfiguration<Grant>
{
    public override void Configure(EntityTypeBuilder<Grant> builder)
    {
        base.Configure(builder);

        builder.Property(grant => grant.AgentClientId).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(grant => grant.ServerName).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(grant => grant.Tools).IsRequired(); // Postgres text[]
        builder.Property(grant => grant.Justification).HasMaxLength(MaxLength.C512).IsRequired();
        builder.Property(grant => grant.Status).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(grant => grant.FirstApproverId).HasMaxLength(MaxLength.C128);

        builder.HasIndex(grant => new { grant.AgentId, grant.Status });

        builder.HasMany(grant => grant.AuditEntries).WithOne()
            .HasForeignKey("GrantId").IsRequired().OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class GrantAuditEntryConfiguration : EntityConfiguration<GrantAuditEntry>
{
    public override void Configure(EntityTypeBuilder<GrantAuditEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("GrantAuditEntries");

        builder.Property(entry => entry.Action).HasMaxLength(MaxLength.C32).IsRequired();
        builder.Property(entry => entry.ActorId).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(entry => entry.Comment).HasMaxLength(MaxLength.C512);
        builder.Property(entry => entry.FromStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(entry => entry.ToStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
    }
}
