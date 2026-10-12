using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TimetableConfiguration : IEntityTypeConfiguration<TimetableEntity>
    {
        public void Configure(EntityTypeBuilder<TimetableEntity> builder)
        {
            builder.ToTable("Timetables", table =>
            {
                table.HasCheckConstraint("CK_Timetables_Revision", "\"Revision\" >= 0");
            });

            builder.HasKey(x => x.TimetableId);
            builder.Property(x => x.TimetableId).ValueGeneratedOnAdd();
            builder.Property(x => x.DisplayName).IsRequired();
            builder.Property(x => x.Revision).IsRequired().IsConcurrencyToken().ValueGeneratedNever();
        }
    }
}
