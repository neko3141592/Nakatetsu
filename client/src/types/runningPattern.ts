import type { PointHandling } from "@/types/train";

export type RunningPatternPoint = {
  sequenceIndex: number;
  stationId: string;
  trackCircuitId: string;
  handling: Extract<PointHandling, "stop" | "pass">;
  arrivalOffsetSeconds: number | null;
  departureOffsetSeconds: number | null;
};

export type RunningPatternSummary = { id: string; trainTypeId: string; displayName: string };
export type RunningPattern = RunningPatternSummary & { points: RunningPatternPoint[] };
