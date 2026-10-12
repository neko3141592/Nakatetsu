namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed record TimetableData(
        long TimetableId,
        string DisplayName,
        long Revision,
        IReadOnlyList<TimetableOperationData> Operations);

    public sealed record TimetableTrainData(
        int SequenceIndex,
        string TrainNumber,
        string TrainTypeId,
        IReadOnlyList<TimetablePointData> Points);

    public sealed record TimetableOperationData(
        string OperationNumber,
        IReadOnlyList<TimetableTrainData> Trains);

    public sealed record TimetablePointData(
        long TimetablePointId,
        int SequenceIndex,
        string? StationId,
        string TrackCircuitId,
        TimetablePointHandling Handling,
        int? ArrivalSeconds,
        int? DepartureSeconds,
        IReadOnlyList<TimetablePointRouteData> Routes);

    public sealed record TimetablePointRouteData(
        int SequenceIndex,
        string ControlStationId,
        string RouteId);
}
