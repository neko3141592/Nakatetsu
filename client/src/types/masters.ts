export type Station = {
  stationId: string;
  displayName: string;
};

export type StationTrack = {
  stationId: string;
  trackId: string;
  displayName: string;
  trackCircuitId: string;
};

export type StationDetail = Station & { tracks: StationTrack[] };

export type TrackCircuit = {
  trackCircuitId: string;
  displayName: string;
};

export type Route = {
  controlStationId: string;
  routeId: string;
};

export type TrainType = {
  trainTypeId: string;
  displayName: string;
};
