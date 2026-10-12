using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TrackCircuitConfiguration : IEntityTypeConfiguration<TrackCircuitEntity>
    {
        public void Configure(EntityTypeBuilder<TrackCircuitEntity> builder)
        {
            builder.ToTable("TrackCircuits");
            builder.HasKey(x => x.TrackCircuitId);
            builder.Property(x => x.TrackCircuitId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
        }
    }
}
