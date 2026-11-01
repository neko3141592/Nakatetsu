import type { StationDetail, TrainTypeStationHandling } from "@/types/masters";
import type { PointSection, TimetablePoint, TimetablePointInput } from "@/types/train";

export type TrainStopDraft = Omit<TimetablePointInput, "sequenceIndex" | "arrivalSeconds" | "departureSeconds"> & {
  rowId: string;
  arrivalTime: string;
  departureTime: string;
};

export function getStopDraftValues(stops: readonly TrainStopDraft[]) {
  return stops.map(({ section, stationId, trackCircuitId, handling, arrivalTime, departureTime }) => ({ section, stationId, trackCircuitId, handling, arrivalTime, departureTime }));
}

export function formatTrainTime(seconds: number | null) {
  if (seconds === null) return "";
  return [Math.floor(seconds / 3600), Math.floor(seconds / 60) % 60, seconds % 60].map((value) => String(value).padStart(2, "0")).join(":");
}

export function parseTrainTime(time: string): number | null {
  if (!time.trim()) return null;
  const match = /^(\d{1,3}):([0-5]\d)(?::([0-5]\d))?$/.exec(time.trim());
  return match ? Number(match[1]) * 3600 + Number(match[2]) * 60 + Number(match[3] ?? 0) : NaN;
}

export function createTrainStop(stationId: string | null = null, point?: TimetablePoint | TimetablePointInput, section: PointSection = point?.section ?? "service"): TrainStopDraft {
  return {
    rowId: crypto.randomUUID(),
    section, stationId, trackCircuitId: point?.trackCircuitId ?? "", handling: section === "service" ? point?.handling ?? "stop" : "unspecified",
    arrivalTime: formatTrainTime(point?.arrivalSeconds ?? null), departureTime: formatTrainTime(point?.departureSeconds ?? null),
    routes: (point?.routes ?? []).map(({ sequenceIndex, controlStationId, routeId }) => ({ sequenceIndex, controlStationId, routeId })),
  };
}

export function buildServiceStops(stations: StationDetail[], origin: string, destination: string, previous: TrainStopDraft[], defaults: readonly TrainTypeStationHandling[] = []) {
  const from = stations.findIndex((station) => station.stationId === origin);
  const to = stations.findIndex((station) => station.stationId === destination);
  if (from < 0 || to < 0) return [];
  const range = stations.slice(Math.min(from, to), Math.max(from, to) + 1);
  if (from > to) range.reverse();
  return range.map((station, index) => {
    const stop = previous.find((stop) => stop.stationId === station.stationId) ?? {
      ...createTrainStop(station.stationId), handling: defaults.find((item) => item.stationId === station.stationId)?.handling ?? "stop",
    };
    return index === 0 || index === range.length - 1 ? { ...stop, handling: "stop" as const } : stop;
  });
}

export function applyTrainTypeHandlings(stops: TrainStopDraft[], defaults: readonly TrainTypeStationHandling[]): TrainStopDraft[] {
  return stops.map((stop, index) => ({
    ...stop, handling: index === 0 || index === stops.length - 1 ? "stop" : defaults.find((item) => item.stationId === stop.stationId)?.handling ?? "stop",
  }));
}

export function buildTrainPoints(stops: TrainStopDraft[]): TimetablePointInput[] {
  return stops.map((stop, index) => {
    const point = trainStopToPoint(stop, index);
    if (index === 0) point.arrivalSeconds = null;
    if (index === stops.length - 1) point.departureSeconds = null;
    return point;
  });
}

export function trainStopToPoint(stop: TrainStopDraft, sequenceIndex: number): TimetablePointInput {
  return {
    sequenceIndex, section: stop.section, stationId: stop.stationId, trackCircuitId: stop.trackCircuitId, handling: stop.handling,
    arrivalSeconds: stop.handling === "pass" ? null : parseTrainTime(stop.arrivalTime), departureSeconds: parseTrainTime(stop.departureTime), routes: stop.routes,
  };
}
