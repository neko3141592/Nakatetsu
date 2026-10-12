using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class StationRunningTimeConfiguration : IEntityTypeConfiguration<StationRunningTimeEntity>
    {
        public void Configure(EntityTypeBuilder<StationRunningTimeEntity> builder)
        {
            builder.ToTable("StationRunningTimes", table =>
            {
                table.HasCheckConstraint("CK_StationRunningTimes_RunningSeconds", "\"RunningSeconds\" > 0");
            });

            builder.HasKey(x => new { x.TrainTypeId, x.FromStationId, x.ToStationId });
            builder.Property(x => x.TrainTypeId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
            builder.Property(x => x.FromStationId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
            builder.Property(x => x.ToStationId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
            builder.Property(x => x.RunningSeconds).IsRequired();

            builder.HasOne<TrainTypeEntity>()
                .WithMany()
                .HasForeignKey(x => x.TrainTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<StationEntity>()
                .WithMany()
                .HasForeignKey(x => x.FromStationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<StationEntity>()
                .WithMany()
                .HasForeignKey(x => x.ToStationId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
