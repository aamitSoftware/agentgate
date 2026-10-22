using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Review.Persistence.EntityConfigurations;

public sealed class ManifestReviewConfiguration : AuditedEntityConfiguration<ManifestReview>
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new JsonStringEnumConverter() } };

    public override void Configure(EntityTypeBuilder<ManifestReview> builder)
    {
        base.Configure(builder);

        builder.Property(review => review.ServerName).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(review => review.ManifestHash).HasMaxLength(MaxLength.C64).IsRequired();
        builder.Property(review => review.PreviousHash).HasMaxLength(MaxLength.C64);
        // canonical JSON text, never jsonb: jsonb would reorder keys and break the hash relationship
        builder.Property(review => review.ToolsJson).IsRequired();
        builder.Property(review => review.PreviousToolsJson);
        builder.Property(review => review.SubmissionReason).HasMaxLength(MaxLength.C512).IsRequired();
        builder.Property(review => review.RiskLevel).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(review => review.Status).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(review => review.FirstApproverId).HasMaxLength(MaxLength.C128);

        // findings are written once by the scan and only ever read whole: one jsonb column, no table
        builder.Property(review => review.Findings).HasColumnType("jsonb")
            .HasConversion(
                findings => JsonSerializer.Serialize(findings, _json),
                json => JsonSerializer.Deserialize<List<RiskFinding>>(json, _json)!,
                new ValueComparer<IReadOnlyList<RiskFinding>>(
                    (left, right) => left!.SequenceEqual(right!),
                    findings => findings.Aggregate(0, (hash, finding) => HashCode.Combine(hash, finding)),
                    findings => findings.ToList()));

        builder.HasIndex(review => new { review.ServerId, review.Status });

        builder.HasMany(review => review.AuditEntries).WithOne()
            .HasForeignKey("ManifestReviewId").IsRequired().OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReviewAuditEntryConfiguration : EntityConfiguration<ReviewAuditEntry>
{
    public override void Configure(EntityTypeBuilder<ReviewAuditEntry> builder)
    {
        base.Configure(builder);
        builder.ToTable("ReviewAuditEntries");

        builder.Property(entry => entry.Action).HasMaxLength(MaxLength.C32).IsRequired();
        builder.Property(entry => entry.ActorId).HasMaxLength(MaxLength.C128).IsRequired();
        builder.Property(entry => entry.Comment).HasMaxLength(MaxLength.C512);
        builder.Property(entry => entry.FromStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(entry => entry.ToStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
    }
}
