import type { TimetableTrain } from "@/types/train";

export type TimetableOperationTrain = {
  operationNumber: string;
  trainNumber: string;
  sequenceIndex: number;
  train: TimetableTrain;
};

export type TimetableOperation = {
  operationNumber: string;
  trains: TimetableOperationTrain[] | null;
};

export type TimetableOperationSummary = Pick<TimetableOperation, "operationNumber">;

export type TimetableOperationDetail = TimetableOperationSummary & {
  trains: Pick<TimetableOperationTrain, "trainNumber" | "sequenceIndex">[];
};
