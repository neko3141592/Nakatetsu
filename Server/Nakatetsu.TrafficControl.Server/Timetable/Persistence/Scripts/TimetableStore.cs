using Microsoft.EntityFrameworkCore;

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed class TimetableStore
    {
        private readonly IDbContextFactory<TimetableDbContext> contextFactory;

        public TimetableStore(
            IDbContextFactory<TimetableDbContext> contextFactory
        )
        {
            this.contextFactory = contextFactory;
        }

        public async Task<long> CreateEnptyAsync(string displayName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

            await using var db = await contextFactory.CreateDbContextAsync();

            var timetable = new TimetableEntity
            {
                DisplayName = displayName,
                Revision = 0
            };

            db.Timetables.Add(timetable);

            await db.SaveChangesAsync();

            return timetable.TimetableId;
        }


        /// <summary>運用・列車・地点・使用進路の階層で読み込む。</summary>
        public async Task<TimetableData?> LoadAsync(
            long timetableId,
            CancellationToken cancellationToken = default)
        {
            await using var db =
                await contextFactory.CreateDbContextAsync(cancellationToken);

            // 分割された問い合わせを、同じ時点のデータとして読み込む。
            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            var timetable = await db.Timetables
                .AsNoTracking()
                .AsSplitQuery()
                .Include(timetable => timetable.Operations)
                    .ThenInclude(operation => operation.Trains)
                        .ThenInclude(train => train.Points)
                            .ThenInclude(point => point.Routes)
                .SingleOrDefaultAsync(
                    timetable => timetable.TimetableId == timetableId,
                    cancellationToken);

            if (timetable is null)
            {
                return null;
            }

            await transaction.CommitAsync(cancellationToken);

            // Entityから値をコピーし、子データを使用順に並べて返す。
            var operations = timetable.Operations
                .OrderBy(operation => operation.OperationNumber, StringComparer.Ordinal)
                .Select(operation =>
                {
                    var trains = operation.Trains
                        .OrderBy(train => train.SequenceIndex)
                        .Select(train =>
                        {
                            var points = train.Points
                                .OrderBy(point => point.SequenceIndex)
                                .Select(point =>
                                {
                                    var routes = point.Routes
                                        .OrderBy(route => route.SequenceIndex)
                                        .Select(route => new TimetablePointRouteData(
                                            route.SequenceIndex,
                                            route.ControlStationId,
                                            route.RouteId))
                                        .ToArray();

                                    return new TimetablePointData(
                                        point.TimetablePointId,
                                        point.SequenceIndex,
                                        point.StationId,
                                        point.TrackCircuitId,
                                        point.Handling,
                                        point.ArrivalSeconds,
                                        point.DepartureSeconds,
                                        routes);
                                })
                                .ToArray();

                            return new TimetableTrainData(
                                train.SequenceIndex,
                                train.TrainNumber,
                                train.TrainTypeId,
                                points);
                        })
                        .ToArray();

                    return new TimetableOperationData(operation.OperationNumber, trains);
                })
                .ToArray();

            return new TimetableData(
                timetable.TimetableId,
                timetable.DisplayName,
                timetable.Revision,
                operations);
        }

        public async Task<long> CreateAsync(TimetableData timetableData)
        {
            ValidateTimetableData(timetableData);

            var timetableEntity = new TimetableEntity
            {
                DisplayName = timetableData.DisplayName,
                Revision = 0,
                Operations = CreateOperations(0, timetableData.Operations),
            };
            foreach (var operation in timetableEntity.Operations)
            {
                foreach (var train in operation.Trains)
                {
                    timetableEntity.Trains.Add(train);
                }
            }

            await using var db = await contextFactory.CreateDbContextAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();

            db.Timetables.Add(timetableEntity);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            return timetableEntity.TimetableId;
        }

        /// <summary>指定Revisionのダイヤ全体を置き換え、新しいRevisionを返す。地点IDは再発行する。</summary>
        public async Task<long> UpdateAsync(
            TimetableData timetableData,
            CancellationToken cancellationToken = default)
        {
            ValidateTimetableData(timetableData);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timetableData.TimetableId);
            ArgumentOutOfRangeException.ThrowIfNegative(timetableData.Revision);

            var newRevision = checked(timetableData.Revision + 1);
            var operations = CreateOperations(timetableData.TimetableId, timetableData.Operations);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            // 子データを変更する前に、編集開始時のRevisionとの一致をDBで確認する。
            var updatedCount = await db.Timetables
                .Where(timetable => timetable.TimetableId == timetableData.TimetableId &&
                    timetable.Revision == timetableData.Revision)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(timetable => timetable.DisplayName, timetableData.DisplayName)
                    .SetProperty(timetable => timetable.Revision, newRevision), cancellationToken);
            if (updatedCount != 1)
            {
                throw new DbUpdateConcurrencyException(
                    "ダイヤが削除されたか、別の編集で更新されています。再取得してから更新してください。");
            }

            // 順序の入れ替えが一意制約に衝突しないよう、旧階層を削除してから再登録する。
            await db.TimetableOperations
                .Where(operation => operation.TimetableId == timetableData.TimetableId)
                .ExecuteDeleteAsync(cancellationToken);
            db.TimetableOperations.AddRange(operations);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return newRevision;
        }

        /// <summary>指定Revisionのダイヤと、その運用・列車・地点・使用進路を削除する。</summary>
        public async Task DeleteAsync(
            long timetableId,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timetableId);
            ArgumentOutOfRangeException.ThrowIfNegative(expectedRevision);

            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var deletedCount = await db.Timetables
                .Where(timetable => timetable.TimetableId == timetableId &&
                    timetable.Revision == expectedRevision)
                .ExecuteDeleteAsync(cancellationToken);
            if (deletedCount != 1)
            {
                throw new DbUpdateConcurrencyException(
                    "ダイヤが削除されたか、別の編集で更新されています。再取得してから削除してください。");
            }

            await transaction.CommitAsync(cancellationToken);
        }

        private static void ValidateTimetableData(TimetableData timetableData)
        {
            ArgumentNullException.ThrowIfNull(timetableData);
            ArgumentException.ThrowIfNullOrWhiteSpace(timetableData.DisplayName);
            ArgumentNullException.ThrowIfNull(timetableData.Operations);

            // 列番と運用番号はダイヤ内で一意とする。
            var operationNumbers = new HashSet<string>(StringComparer.Ordinal);
            var assignedTrainNumbers = new HashSet<string>(StringComparer.Ordinal);
            foreach (var operationData in timetableData.Operations)
            {
                ArgumentNullException.ThrowIfNull(operationData);
                if (string.IsNullOrWhiteSpace(operationData.OperationNumber) ||
                    !operationNumbers.Add(operationData.OperationNumber))
                {
                    throw new ArgumentException(
                        "運用番号には、空白でないダイヤ内で重複しない値を指定してください。",
                        nameof(timetableData));
                }

                ArgumentNullException.ThrowIfNull(operationData.Trains);
                foreach (var trainData in operationData.Trains)
                {
                    ArgumentNullException.ThrowIfNull(trainData);
                    ArgumentNullException.ThrowIfNull(trainData.Points);
                    foreach (var pointData in trainData.Points)
                    {
                        ArgumentNullException.ThrowIfNull(pointData);
                        ArgumentNullException.ThrowIfNull(pointData.Routes);
                        foreach (var routeData in pointData.Routes)
                        {
                            ArgumentNullException.ThrowIfNull(routeData);
                        }
                    }
                }

                var orderedTrains = operationData.Trains.OrderBy(train => train.SequenceIndex).ToArray();
                for (var index = 0; index < orderedTrains.Length; index++)
                {
                    var trainData = orderedTrains[index];
                    if (trainData.SequenceIndex != index)
                    {
                        throw new ArgumentException(
                            "運用内の順序は0始まりの連番で指定してください。",
                            nameof(timetableData));
                    }

                    if (string.IsNullOrWhiteSpace(trainData.TrainNumber) ||
                        !assignedTrainNumbers.Add(trainData.TrainNumber))
                    {
                        throw new ArgumentException(
                            "列番には空白でない値を指定し、1本の列車を重複して割り当てないでください。",
                            nameof(timetableData));
                    }
                }
            }
        }

        private static List<TimetableOperationEntity> CreateOperations(
            long timetableId,
            IReadOnlyList<TimetableOperationData> operationDataList)
        {
            var operations = new List<TimetableOperationEntity>();
            foreach (var operationData in operationDataList)
            {
                var operationEntity = new TimetableOperationEntity
                {
                    TimetableId = timetableId,
                    OperationNumber = operationData.OperationNumber,
                };

                foreach (var trainData in operationData.Trains)
                {
                    var trainEntity = new TimetableTrainEntity
                    {
                        TimetableId = timetableId,
                        OperationNumber = operationData.OperationNumber,
                        SequenceIndex = trainData.SequenceIndex,
                        TrainNumber = trainData.TrainNumber,
                        TrainTypeId = trainData.TrainTypeId,
                    };

                    foreach (var pointData in trainData.Points)
                    {
                        var pointEntity = new TimetablePointEntity
                        {
                            TimetableId = timetableId,
                            TrainNumber = trainData.TrainNumber,
                            SequenceIndex = pointData.SequenceIndex,
                            StationId = pointData.StationId,
                            TrackCircuitId = pointData.TrackCircuitId,
                            Handling = pointData.Handling,
                            ArrivalSeconds = pointData.ArrivalSeconds,
                            DepartureSeconds = pointData.DepartureSeconds,
                        };

                        foreach (var routeData in pointData.Routes)
                        {
                            pointEntity.Routes.Add(new TimetablePointRouteEntity
                            {
                                SequenceIndex = routeData.SequenceIndex,
                                ControlStationId = routeData.ControlStationId,
                                RouteId = routeData.RouteId,
                            });
                        }

                        trainEntity.Points.Add(pointEntity);
                    }

                    operationEntity.Trains.Add(trainEntity);
                }

                operations.Add(operationEntity);
            }

            return operations;
        }
    }
}
