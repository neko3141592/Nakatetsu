using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class StationConfiguration : IEntityTypeConfiguration<StationEntity>
    {
        public void Configure(EntityTypeBuilder<StationEntity> builder)
        {
            builder.ToTable("Stations");
            builder.HasKey(x => x.StationId);
            builder.Property(x => x.StationId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
            builder.Property(x => x.DisplayName).IsRequired();
        }
    }
}
