using System.Text.Json;
using System.Text.Json.Serialization;

namespace Registry.Persistence.EntityConfigurations;

public sealed class ServerStatusTransitionConfiguration : IEntityTypeConfiguration<ServerStatusTransition>
{
    private static readonly JsonSerializerOptions _json = new() { Converters = { new JsonStringEnumConverter() } };

    public void Configure(EntityTypeBuilder<ServerStatusTransition> builder)
    {
        builder.HasKey(transition => new { transition.CurrentStatus, transition.ActionType });
        builder.Property(transition => transition.CurrentStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.ActionType).HasConversion<string>().HasMaxLength(MaxLength.C32);
        builder.Property(transition => transition.DestinationStatus).HasConversion<string>().HasMaxLength(MaxLength.C32);

        builder.HasData(LoadSeed());
    }

    public static List<ServerStatusTransition> LoadSeed() =>
        JsonSerializer.Deserialize<List<ServerStatusTransition>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "ServerStatusTransition.json")), _json)!;
}
