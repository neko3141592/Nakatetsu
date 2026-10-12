using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TrainTypeConfiguration : IEntityTypeConfiguration<TrainTypeEntity>
    {
        public void Configure(EntityTypeBuilder<TrainTypeEntity> builder)
        {
            builder.ToTable("TrainTypes");
            builder.HasKey(x => x.TrainTypeId);
            builder.Property(x => x.TrainTypeId).IsRequired().ValueGeneratedNever().UseCollation("BINARY");
            builder.Property(x => x.DisplayName).IsRequired();
        }
    }
}
