import type { StationDetail } from "@/types/masters";
import type { TimetablePoint, TimetablePointInput } from "@/types/train";

export type TrainStopDraft = Omit<TimetablePointInput, "sequenceIndex" | "arrivalSeconds" | "departureSeconds"> & {
  arrivalTime: string;
  departureTime: string;
};

export function formatTrainTime(seconds: number | null) {
  if (seconds === null) return "";
  return [Math.floor(seconds / 3600), Math.floor(seconds / 60) % 60, seconds % 60].map((value) => String(value).padStart(2, "0")).join(":");
}

export function parseTrainTime(time: string): number | null {
  if (!time.trim()) return null;
  const match = /^(\d{1,3}):([0-5]\d)(?::([0-5]\d))?$/.exec(time.trim());
  return match ? Number(match[1]) * 3600 + Number(match[2]) * 60 + Number(match[3] ?? 0) : NaN;
}

export function createTrainStop(stationId: string, point?: TimetablePoint | TimetablePointInput): TrainStopDraft {
  return {
    stationId, trackCircuitId: point?.trackCircuitId ?? "", handling: point?.handling ?? "stop",
    arrivalTime: formatTrainTime(point?.arrivalSeconds ?? null), departureTime: formatTrainTime(point?.departureSeconds ?? null),
    routes: (point?.routes ?? []).map(({ sequenceIndex, controlStationId, routeId }) => ({ sequenceIndex, controlStationId, routeId })),
  };
}

export function getTrainStations(stations: StationDetail[], origin: string, destination: string) {
  const from = stations.findIndex((station) => station.stationId === origin);
  const to = stations.findIndex((station) => station.stationId === destination);
  if (from < 0 || to < 0) return [];
  const range = stations.slice(Math.min(from, to), Math.max(from, to) + 1);
  return from <= to ? range : range.reverse();
}

export function trainStopToPoint(stop: TrainStopDraft, sequenceIndex: number): TimetablePointInput {
  const { arrivalTime, departureTime, ...point } = stop;
  return { ...point, sequenceIndex, arrivalSeconds: parseTrainTime(arrivalTime), departureSeconds: parseTrainTime(departureTime) };
}
