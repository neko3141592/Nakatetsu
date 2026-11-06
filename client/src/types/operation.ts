import type { TimetableTrainSummary } from "@/types/train";
export type TimetableOperationTrain = {
  operationId: string;
  operationPlanId: string;
  timetableId: string;
  trainId: string;
  sequenceIndex: number;
  train: TimetableTrainSummary;
};
export type TimetableOperation = {
  id: string;
  operationPlanId: string;
  timetableId: string;
  operationNumber: string;
  trains: TimetableOperationTrain[];
};
export type TimetableOperationSummary = Pick<
  TimetableOperation,
  "id" | "operationPlanId" | "timetableId" | "operationNumber"
>;
export type TimetableOperationDetail = TimetableOperation;
export type OperationPlan = {
  id: string;
  timetableId: string;
  displayName: string;
  operations: TimetableOperation[];
};
export type OperationPlanSummary = Omit<OperationPlan, "operations">;
