import type { TimetableTrain } from "@/types/train";
export type Timetable = {
  id: string;
  displayName: string;
  revision: number;
  trains: TimetableTrain[];
};
export type TimetableSummary = Pick<
  Timetable,
  "id" | "displayName" | "revision"
>;
