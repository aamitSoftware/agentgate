using System.Text.Json;
using System.Text.Json.Serialization;

namespace Review.Persistence.EntityConfigurations;

/// <summary>Reads a seeded transition table (Data/{name}.json), copied next to the assembly.</summary>
public static class TransitionSeed
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new JsonStringEnumConverter() } };

    public static List<T> Load<T>(string name) =>
        JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", $"{name}.json")), _json)!;
}

public sealed class ReviewStatusTransitionConfiguration : IEntityTypeConfiguration<ReviewStatusTransition>
{
    public void Configure(EntityTypeBuilder<ReviewStatusTransition> builder)
    {
        builder.HasKey(transition => new { transition.CurrentStatus, transition.ActionType });
        builder.Property(transition => transition.CurrentStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.ActionType).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.DestinationStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);

        builder.HasData(LoadSeed());
    }

    public static List<ReviewStatusTransition> LoadSeed() => TransitionSeed.Load<ReviewStatusTransition>(nameof(ReviewStatusTransition));
}

public sealed class GrantStatusTransitionConfiguration : IEntityTypeConfiguration<GrantStatusTransition>
{
    public void Configure(EntityTypeBuilder<GrantStatusTransition> builder)
    {
        builder.HasKey(transition => new { transition.CurrentStatus, transition.ActionType });
        builder.Property(transition => transition.CurrentStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.ActionType).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.DestinationStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);

        builder.HasData(LoadSeed());
    }

    public static List<GrantStatusTransition> LoadSeed() => TransitionSeed.Load<GrantStatusTransition>(nameof(GrantStatusTransition));
}
