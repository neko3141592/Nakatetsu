using Microsoft.EntityFrameworkCore;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>ダイヤ・運用のテーブルとFluent設定をまとめるDB接続の窓口。</summary>
    public sealed class TimetableDbContext : DbContext
    {
        public TimetableDbContext(DbContextOptions<TimetableDbContext> options)
            : base(options)
        {
        }

        public DbSet<TimetableEntity> Timetables => Set<TimetableEntity>();
        public DbSet<TimetableTrainEntity> TimetableTrains => Set<TimetableTrainEntity>();
        public DbSet<TimetableOperationEntity> TimetableOperations => Set<TimetableOperationEntity>();
        public DbSet<TrainTypeEntity> TrainTypes => Set<TrainTypeEntity>();
        public DbSet<TimetablePointEntity> TimetablePoints => Set<TimetablePointEntity>();
        public DbSet<TimetablePointRouteEntity> TimetablePointRoutes => Set<TimetablePointRouteEntity>();
        public DbSet<TrackCircuitEntity> TrackCircuits => Set<TrackCircuitEntity>();
        public DbSet<StationRunningTimeEntity> StationRunningTimes => Set<StationRunningTimeEntity>();
        public DbSet<StationEntity> Stations => Set<StationEntity>();
        public DbSet<RouteEntity> Routes => Set<RouteEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(TimetableDbContext).Assembly);
        }
    }
}
