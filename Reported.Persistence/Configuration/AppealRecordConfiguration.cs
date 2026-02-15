using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Reported.Persistence.Configuration;

public sealed class AppealRecordConfiguration : IEntityTypeConfiguration<AppealRecord>
{
    public void Configure(EntityTypeBuilder<AppealRecord> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(i => i.DiscordId).IsUnique();
    }
}
