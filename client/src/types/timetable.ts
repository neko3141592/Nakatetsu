import type { TimetableOperation } from "@/types/operation";
import type { TimetableTrain } from "@/types/train";

export type Timetable = {
  id: string;
  displayName: string;
  revision: number;
  trains: TimetableTrain[];
  temporaryTrainNumbers: string[];
  operations: TimetableOperation[];
};

export type TimetableSummary = Pick<Timetable, "id" | "displayName" | "revision">;
