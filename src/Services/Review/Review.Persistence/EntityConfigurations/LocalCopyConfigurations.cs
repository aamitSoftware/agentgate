namespace Review.Persistence.EntityConfigurations;

public sealed class KnownAgentConfiguration : EntityConfiguration<KnownAgent>
{
    public override void Configure(EntityTypeBuilder<KnownAgent> builder)
    {
        base.Configure(builder);

        builder.Property(agent => agent.Name).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(agent => agent.ClientId).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(agent => agent.Owner).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(agent => agent.Purpose).HasMaxLength(MaxLength.C512).IsRequired();
    }
}

public sealed class KnownServerConfiguration : EntityConfiguration<KnownServer>
{
    public override void Configure(EntityTypeBuilder<KnownServer> builder)
    {
        base.Configure(builder);

        builder.Property(server => server.Name).HasMaxLength(MaxLength.C64).IsRequired();
    }
}
