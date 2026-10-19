using Blocks.Core;
using Blocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Blocks.EntityFrameworkCore;

public abstract class EntityConfiguration<T> : IEntityTypeConfiguration<T>
    where T : class, IEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever(); // ids are generated in code (Guid v7)
    }
}

/// <summary>Audit columns are mandatory for every aggregate.</summary>
public abstract class AuditedEntityConfiguration<T> : EntityConfiguration<T>
    where T : class, IAggregateRoot
{
    public override void Configure(EntityTypeBuilder<T> builder)
    {
        base.Configure(builder);

        builder.Property(aggregate => aggregate.CreatedBy).IsRequired().HasMaxLength(MaxLength.C128);
        builder.Property(aggregate => aggregate.CreatedOn).IsRequired();
        builder.Property(aggregate => aggregate.LastModifiedBy).HasMaxLength(MaxLength.C128);
        builder.Property(aggregate => aggregate.LastModifiedOn);

        builder.Ignore(aggregate => aggregate.DomainEvents);
    }
}
