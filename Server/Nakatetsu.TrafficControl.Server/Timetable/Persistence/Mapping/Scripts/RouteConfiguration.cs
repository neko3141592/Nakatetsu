using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class RouteConfiguration : IEntityTypeConfiguration<RouteEntity>
    {
        public void Configure(EntityTypeBuilder<RouteEntity> builder)
        {
            builder.ToTable("Routes");
            builder.HasKey(x => new { x.ControlStationId, x.RouteId });
            builder.Property(x => x.ControlStationId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
            builder.Property(x => x.RouteId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
        }
    }
}
