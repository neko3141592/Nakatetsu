"use server";

import type { TimetableOperationDetail } from "@/types/operation";
import type { TimetableTrainSummary } from "@/types/train";

type Result<T> = { data: T; error?: never } | { data?: never; error: string };

export async function deleteOperation(number: string): Promise<Result<null>> {
  if (!number.trim()) return { error: "保存済みの運用を選択してください" };
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/operations/${encodeURIComponent(number)}`, { method: "DELETE" });
    if (!response.ok) return { error: (await response.text()).trim() || "運用の削除に失敗しました" };
    return { data: null };
  } catch {
    return { error: "運用の削除に失敗しました" };
  }
}

export async function loadOperation(number: string): Promise<Result<TimetableOperationDetail>> {
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/operations/${encodeURIComponent(number)}`, { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "運用の取得に失敗しました" };
    return { data: await response.json() };
  } catch {
    return { error: "運用の取得に失敗しました" };
  }
}

export async function saveOperation(operation: TimetableOperationDetail, isNew = false): Promise<Result<TimetableOperationDetail>> {
  const operationNumber = isNew ? operation.operationNumber.trim() : operation.operationNumber;
  if (!operationNumber.trim()) return { error: "運用番号を入力してください" };
  const trains = operation.trains.map((train, sequenceIndex) => ({ trainNumber: train.trainNumber, sequenceIndex }));
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/operations${isNew ? "" : `/${encodeURIComponent(operationNumber)}`}`, {
      method: isNew ? "POST" : "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(isNew ? { operationNumber, trains } : { trains }),
    });
    if (!response.ok) return { error: (await response.text()).trim() || "運用の保存に失敗しました" };
    return { data: { operationNumber, trains } };
  } catch {
    return { error: "運用の保存に失敗しました" };
  }
}

export async function loadTrainList(): Promise<Result<TimetableTrainSummary[]>> {
  try {
    const response = await fetch("http://127.0.0.1:8080/api/trains", { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "列車一覧の取得に失敗しました" };
    return { data: await response.json() };
  } catch {
    return { error: "列車一覧の取得に失敗しました" };
  }
}
