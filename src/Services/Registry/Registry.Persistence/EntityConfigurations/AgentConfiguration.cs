namespace Registry.Persistence.EntityConfigurations;

public sealed class AgentConfiguration : AuditedEntityConfiguration<Agent>
{
    public override void Configure(EntityTypeBuilder<Agent> builder)
    {
        base.Configure(builder);

        builder.Property(agent => agent.Name).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(agent => agent.ClientId).HasMaxLength(MaxLength.C64).IsRequired();
        builder.HasIndex(agent => agent.ClientId).IsUnique();
        builder.Property(agent => agent.Owner).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(agent => agent.Purpose).HasMaxLength(MaxLength.C512).IsRequired();
    }
}
