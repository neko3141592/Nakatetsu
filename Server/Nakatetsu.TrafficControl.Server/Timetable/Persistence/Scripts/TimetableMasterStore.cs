using Microsoft.EntityFrameworkCore;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    /// <summary>ダイヤから参照するマスターの新規登録・一覧取得・削除を行う。</summary>
    public sealed class TimetableMasterStore
    {
        private readonly IDbContextFactory<TimetableDbContext> contextFactory;

        public TimetableMasterStore(IDbContextFactory<TimetableDbContext> contextFactory)
        {
            ArgumentNullException.ThrowIfNull(contextFactory);
            this.contextFactory = contextFactory;
        }

        /// <summary>列車種別を新規登録する。同じIDが存在する場合はDbUpdateExceptionを投げる。</summary>
        public async Task RegisterTrainTypeAsync(
            TrainTypeData trainTypeData,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(trainTypeData);
            ArgumentException.ThrowIfNullOrWhiteSpace(trainTypeData.TrainTypeId);
            ArgumentException.ThrowIfNullOrWhiteSpace(trainTypeData.DisplayName);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.TrainTypes.Add(new TrainTypeEntity
            {
                TrainTypeId = trainTypeData.TrainTypeId,
                DisplayName = trainTypeData.DisplayName,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>列車種別をID順で取得する。</summary>
        public async Task<IReadOnlyList<TrainTypeData>> ListTrainTypesAsync(
            CancellationToken cancellationToken = default)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.TrainTypes
                .AsNoTracking()
                .OrderBy(trainType => trainType.TrainTypeId)
                .Select(trainType => new TrainTypeData(trainType.TrainTypeId, trainType.DisplayName))
                .ToArrayAsync(cancellationToken);
        }

        /// <summary>列車種別を削除する。対象がなければfalse、参照中ならDbUpdateExceptionを通知する。</summary>
        public async Task<bool> DeleteTrainTypeAsync(
            string trainTypeId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(trainTypeId);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.TrainTypes.Remove(new TrainTypeEntity { TrainTypeId = trainTypeId });
            return await SaveDeletionAsync(db, cancellationToken);
        }

        /// <summary>駅を新規登録する。同じIDが存在する場合はDbUpdateExceptionを投げる。</summary>
        public async Task RegisterStationAsync(
            StationData stationData,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(stationData);
            ArgumentException.ThrowIfNullOrWhiteSpace(stationData.StationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(stationData.DisplayName);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.Stations.Add(new StationEntity
            {
                StationId = stationData.StationId,
                DisplayName = stationData.DisplayName,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>駅をID順で取得する。</summary>
        public async Task<IReadOnlyList<StationData>> ListStationsAsync(
            CancellationToken cancellationToken = default)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.Stations
                .AsNoTracking()
                .OrderBy(station => station.StationId)
                .Select(station => new StationData(station.StationId, station.DisplayName))
                .ToArrayAsync(cancellationToken);
        }

        /// <summary>駅を削除する。対象がなければfalse、参照中ならDbUpdateExceptionを通知する。</summary>
        public async Task<bool> DeleteStationAsync(
            string stationId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stationId);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.Stations.Remove(new StationEntity { StationId = stationId });
            return await SaveDeletionAsync(db, cancellationToken);
        }

        /// <summary>軌道回路を新規登録する。同じIDが存在する場合はDbUpdateExceptionを投げる。</summary>
        public async Task RegisterTrackCircuitAsync(
            TrackCircuitData trackCircuitData,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(trackCircuitData);
            ArgumentException.ThrowIfNullOrWhiteSpace(trackCircuitData.TrackCircuitId);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.TrackCircuits.Add(new TrackCircuitEntity
            {
                TrackCircuitId = trackCircuitData.TrackCircuitId,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>軌道回路をID順で取得する。</summary>
        public async Task<IReadOnlyList<TrackCircuitData>> ListTrackCircuitsAsync(
            CancellationToken cancellationToken = default)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.TrackCircuits
                .AsNoTracking()
                .OrderBy(trackCircuit => trackCircuit.TrackCircuitId)
                .Select(trackCircuit => new TrackCircuitData(trackCircuit.TrackCircuitId))
                .ToArrayAsync(cancellationToken);
        }

        /// <summary>軌道回路を削除する。対象がなければfalse、参照中ならDbUpdateExceptionを通知する。</summary>
        public async Task<bool> DeleteTrackCircuitAsync(
            string trackCircuitId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(trackCircuitId);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.TrackCircuits.Remove(new TrackCircuitEntity { TrackCircuitId = trackCircuitId });
            return await SaveDeletionAsync(db, cancellationToken);
        }

        /// <summary>進路を新規登録する。同じ制御駅ID・進路IDの組が存在する場合はDbUpdateExceptionを投げる。</summary>
        public async Task RegisterRouteAsync(
            RouteData routeData,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(routeData);
            ArgumentException.ThrowIfNullOrWhiteSpace(routeData.ControlStationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(routeData.RouteId);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.Routes.Add(new RouteEntity
            {
                ControlStationId = routeData.ControlStationId,
                RouteId = routeData.RouteId,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>進路を制御駅ID・進路ID順で取得する。</summary>
        public async Task<IReadOnlyList<RouteData>> ListRoutesAsync(
            CancellationToken cancellationToken = default)
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.Routes
                .AsNoTracking()
                .OrderBy(route => route.ControlStationId)
                .ThenBy(route => route.RouteId)
                .Select(route => new RouteData(route.ControlStationId, route.RouteId))
                .ToArrayAsync(cancellationToken);
        }

        /// <summary>進路を削除する。対象がなければfalse、参照中ならDbUpdateExceptionを通知する。</summary>
        public async Task<bool> DeleteRouteAsync(
            string controlStationId,
            string routeId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(controlStationId);
            ArgumentException.ThrowIfNullOrWhiteSpace(routeId);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            db.Routes.Remove(new RouteEntity
            {
                ControlStationId = controlStationId,
                RouteId = routeId,
            });
            return await SaveDeletionAsync(db, cancellationToken);
        }

        private static async Task<bool> SaveDeletionAsync(
            TimetableDbContext db,
            CancellationToken cancellationToken)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // キーだけで削除するため、削除件数0は対象が存在しないことを表す。
                return false;
            }
        }
    }
}
