import type { Station } from "@/types/masters";

export type TimetableDiagramStationLayout = {
  stationId: string;
  divisionsToNext?: number;
};

export type TimetableDiagramDefinition = {
  stations: TimetableDiagramStationLayout[];
};

export type TimetableDiagramStation = TimetableDiagramStationLayout & Pick<Station, "displayName">;
