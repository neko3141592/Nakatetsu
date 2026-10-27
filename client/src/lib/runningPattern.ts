import type { RunningPattern, RunningPatternPoint } from "@/types/runningPattern";
import type { TimetableDiagramTrain } from "@/types/timetableDiagram";
import { buildTrainPoints, createTrainStop, type TrainStopDraft } from "@/lib/trainTimetable";

export function runningPatternToStops(points: readonly RunningPatternPoint[], startSeconds = 0): TrainStopDraft[] {
  return points.toSorted((a, b) => a.sequenceIndex - b.sequenceIndex).map((point) => createTrainStop(point.stationId, {
    sequenceIndex: point.sequenceIndex, section: "service", stationId: point.stationId, trackCircuitId: point.trackCircuitId,
    handling: point.handling,
    arrivalSeconds: point.arrivalOffsetSeconds === null ? null : startSeconds + point.arrivalOffsetSeconds,
    departureSeconds: point.departureOffsetSeconds === null ? null : startSeconds + point.departureOffsetSeconds, routes: [],
  }));
}

export function buildRunningPatternPoints(stops: TrainStopDraft[]): RunningPatternPoint[] {
  return buildTrainPoints(stops).map((point, index) => ({
    sequenceIndex: index, stationId: point.stationId ?? "", trackCircuitId: point.trackCircuitId,
    handling: point.handling === "pass" ? "pass" : "stop",
    arrivalOffsetSeconds: point.arrivalSeconds,
    departureOffsetSeconds: index === 0 ? 0 : point.departureSeconds,
  }));
}

export function runningPatternToDiagram(pattern: RunningPattern, color?: string): TimetableDiagramTrain {
  return {
    trainNumber: pattern.displayName,
    trainType: color ? { color } : undefined,
    points: pattern.points.map((point) => ({
      sequenceIndex: point.sequenceIndex, stationId: point.stationId, section: "service",
      arrivalSeconds: point.arrivalOffsetSeconds, departureSeconds: point.departureOffsetSeconds,
    })),
  };
}
