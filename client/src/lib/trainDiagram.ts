import type { TimetableDiagramPoint } from "@/types/timetableDiagram";

export function getTrainDiagramPoints(
  points: readonly TimetableDiagramPoint[],
  stationPositions: ReadonlyMap<string, number>,
  pixelsPerMinute: number,
  startSeconds: number,
  left = 0,
) {
  return points.toSorted((a, b) => a.sequenceIndex - b.sequenceIndex).flatMap((point) => {
    if ((point.section ?? "service") !== "service" || point.stationId === null) return [];
    const y = stationPositions.get(point.stationId);
    if (y === undefined) return [];
    return [point.arrivalSeconds, point.departureSeconds].flatMap((seconds) =>
      seconds !== null && Number.isFinite(seconds)
        ? [{ x: left + (seconds - startSeconds) / 60 * pixelsPerMinute, y }]
        : []);
  });
}
