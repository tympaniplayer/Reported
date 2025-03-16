using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Reported.Persistence.Configuration;

public sealed class UserReportConfiguration: IEntityTypeConfiguration<UserReport>
{
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(i => i.DiscordId);
    }
}