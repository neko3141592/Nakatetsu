using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TimetablePointConfiguration : IEntityTypeConfiguration<TimetablePointEntity>
    {
        public void Configure(EntityTypeBuilder<TimetablePointEntity> builder)
        {
            builder.ToTable("TimetablePoints", table =>
            {
                table.HasCheckConstraint("CK_TimetablePoints_SequenceIndex", "\"SequenceIndex\" >= 0");
                table.HasCheckConstraint("CK_TimetablePoints_Handling", "\"Handling\" IN (1, 2)");
                table.HasCheckConstraint("CK_TimetablePoints_ArrivalSeconds",
                    "\"ArrivalSeconds\" IS NULL OR \"ArrivalSeconds\" >= 0");
                table.HasCheckConstraint("CK_TimetablePoints_DepartureSeconds",
                    "\"DepartureSeconds\" IS NULL OR \"DepartureSeconds\" >= 0");
                table.HasCheckConstraint("CK_TimetablePoints_ArrivalDepartureOrder",
                    "\"ArrivalSeconds\" IS NULL OR \"DepartureSeconds\" IS NULL OR " +
                    "\"ArrivalSeconds\" <= \"DepartureSeconds\"");
            });

            builder.HasKey(x => x.TimetablePointId);
            builder.Property(x => x.TimetablePointId).ValueGeneratedOnAdd();
            builder.Property(x => x.TimetableId).IsRequired();
            builder.Property(x => x.TrainNumber).IsRequired().UseCollation("BINARY");
            builder.Property(x => x.SequenceIndex).IsRequired();
            builder.Property(x => x.StationId).IsRequired(false).UseCollation("BINARY");
            builder.Property(x => x.TrackCircuitId).IsRequired().UseCollation("BINARY");
            builder.Property(x => x.Handling).HasConversion<int>().IsRequired();
            builder.Property(x => x.ArrivalSeconds).IsRequired(false);
            builder.Property(x => x.DepartureSeconds).IsRequired(false);
            builder.HasIndex(x => new { x.TimetableId, x.TrainNumber, x.SequenceIndex }).IsUnique();

            builder.HasOne<TimetableTrainEntity>()
                .WithMany(train => train.Points)
                .HasForeignKey(x => new { x.TimetableId, x.TrainNumber })
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<StationEntity>()
                .WithMany()
                .HasForeignKey(x => x.StationId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // 回路は複数列車・複数地点から参照できる。
            builder.HasOne<TrackCircuitEntity>()
                .WithMany()
                .HasForeignKey(x => x.TrackCircuitId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
