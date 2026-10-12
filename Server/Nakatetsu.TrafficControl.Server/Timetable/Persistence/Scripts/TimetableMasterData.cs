namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence
{
    public sealed record TrainTypeData(string TrainTypeId, string DisplayName);

    public sealed record StationData(string StationId, string DisplayName);

    public sealed record TrackCircuitData(string TrackCircuitId);

    public sealed record RouteData(string ControlStationId, string RouteId);
}
