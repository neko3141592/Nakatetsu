import type { Station, TrainType } from "@/types/masters";
import type { TimetablePoint } from "@/types/train";

export type TimetableDiagramStationLayout = {
  stationId: string;
  divisionsToNext?: number;
};

export type TimetableDiagramDefinition = {
  stations: TimetableDiagramStationLayout[];
};

export type TimetableDiagramStation = TimetableDiagramStationLayout & Pick<Station, "displayName">;

export type TimetableDiagramPoint = Pick<TimetablePoint, "sequenceIndex" | "stationId" | "section" | "arrivalSeconds" | "departureSeconds">;
export type TimetableDiagramTrain = { trainNumber: string; trainType?: Pick<TrainType, "color">; points: readonly TimetableDiagramPoint[] | null };
