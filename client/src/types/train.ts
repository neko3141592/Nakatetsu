import type { Route, Station, TrackCircuit, TrainType } from "@/types/masters";

export type PointHandling = "pass" | "stop" | "unspecified";
export type PointSection = "service" | "depot_exit" | "depot_entry";

export type TimetablePointRoute = {
  timetablePointId: number;
  sequenceIndex: number;
  controlStationId: string;
  routeId: string;
  route: Route;
};

export type TimetablePoint = {
  timetablePointId: number;
  trainId: string;
  sequenceIndex: number;
  stationId: string | null;
  section: PointSection;
  trackCircuitId: string;
  handling: PointHandling;
  arrivalSeconds: number | null;
  departureSeconds: number | null;
  station: Station | null;
  trackCircuit: TrackCircuit;
  routes: TimetablePointRoute[] | null;
};

export type TimetableTrain = {
  id: string;
  timetableId: string;
  trainNumber: string;
  trainTypeId: string;
  originStationId: string;
  destinationStationId: string;
  trainType: TrainType;
  points: TimetablePoint[] | null;
};

export type TimetableTrainSummary = Pick<
  TimetableTrain,
  | "id"
  | "timetableId"
  | "trainNumber"
  | "trainTypeId"
  | "originStationId"
  | "destinationStationId"
> & { departureSeconds?: number | null };
export type TimetableTrainBasics = Pick<
  TimetableTrain,
  "trainNumber" | "trainTypeId" | "originStationId" | "destinationStationId"
>;

export type TimetablePointInput = Pick<
  TimetablePoint,
  | "sequenceIndex"
  | "section"
  | "stationId"
  | "trackCircuitId"
  | "handling"
  | "arrivalSeconds"
  | "departureSeconds"
> & {
  routes: Pick<
    TimetablePointRoute,
    "sequenceIndex" | "controlStationId" | "routeId"
  >[];
};

export type TimetableTrainInput = TimetableTrainBasics & {
  timetableId: string;
  points: TimetablePointInput[];
};
