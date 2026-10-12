using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TimetablePointRouteConfiguration : IEntityTypeConfiguration<TimetablePointRouteEntity>
    {
        public void Configure(EntityTypeBuilder<TimetablePointRouteEntity> builder)
        {
            builder.ToTable("TimetablePointRoutes", table =>
            {
                table.HasCheckConstraint("CK_TimetablePointRoutes_SequenceIndex", "\"SequenceIndex\" >= 0");
            });

            builder.HasKey(x => new { x.TimetablePointId, x.SequenceIndex });
            builder.Property(x => x.TimetablePointId).ValueGeneratedNever();
            builder.Property(x => x.SequenceIndex).ValueGeneratedNever();
            builder.Property(x => x.ControlStationId).IsRequired().UseCollation("BINARY");
            builder.Property(x => x.RouteId).IsRequired().UseCollation("BINARY");

            builder.HasOne<TimetablePointEntity>()
                .WithMany(point => point.Routes)
                .HasForeignKey(x => x.TimetablePointId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<RouteEntity>()
                .WithMany()
                .HasForeignKey(x => new { x.ControlStationId, x.RouteId })
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
