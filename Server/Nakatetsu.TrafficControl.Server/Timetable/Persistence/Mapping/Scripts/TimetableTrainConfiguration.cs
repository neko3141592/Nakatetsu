using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TimetableTrainConfiguration : IEntityTypeConfiguration<TimetableTrainEntity>
    {
        public void Configure(EntityTypeBuilder<TimetableTrainEntity> builder)
        {
            builder.ToTable("TimetableTrains", table =>
            {
                table.HasCheckConstraint("CK_TimetableTrains_SequenceIndex", "\"SequenceIndex\" >= 0");
            });

            builder.HasKey(x => new { x.TimetableId, x.TrainNumber });
            builder.Property(x => x.TimetableId).IsRequired().ValueGeneratedNever();
            builder.Property(x => x.TrainNumber).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
            builder.Property(x => x.OperationNumber).IsRequired().UseCollation("BINARY");
            builder.Property(x => x.SequenceIndex).IsRequired().ValueGeneratedNever();
            builder.Property(x => x.TrainTypeId).IsRequired().UseCollation("BINARY");

            // 同じ運用の同じ順序に複数の列車を置かない。
            builder.HasIndex(x => new { x.TimetableId, x.OperationNumber, x.SequenceIndex }).IsUnique();

            builder.HasOne<TimetableEntity>()
                .WithMany(timetable => timetable.Trains)
                .HasForeignKey(x => x.TimetableId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<TimetableOperationEntity>()
                .WithMany(operation => operation.Trains)
                .HasForeignKey(x => new { x.TimetableId, x.OperationNumber })
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<TrainTypeEntity>()
                .WithMany()
                .HasForeignKey(x => x.TrainTypeId)
                .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
