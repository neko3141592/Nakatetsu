using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TimetableOperationConfiguration : IEntityTypeConfiguration<TimetableOperationEntity>
    {
        public void Configure(EntityTypeBuilder<TimetableOperationEntity> builder)
        {
            builder.ToTable("TimetableOperations");
            builder.HasKey(x => new { x.TimetableId, x.OperationNumber });
            builder.Property(x => x.TimetableId).IsRequired().ValueGeneratedNever();
            builder.Property(x => x.OperationNumber).IsRequired().ValueGeneratedNever().UseCollation("BINARY");

            builder.HasOne<TimetableEntity>()
                .WithMany(timetable => timetable.Operations)
                .HasForeignKey(x => x.TimetableId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
