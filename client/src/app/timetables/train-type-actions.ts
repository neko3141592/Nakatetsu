"use server";

import { randomUUID } from "node:crypto";
import type { TrainType } from "@/types/masters";

type Result<T> = { data: T; error?: never } | { data?: never; error: string };

export async function saveTrainType(trainType: TrainType, isNew: boolean): Promise<Result<TrainType>> {
  const displayName = trainType.displayName.trim();
  if (!displayName) return { error: "名称を入力してください" };
  if (!isNew && !trainType.trainTypeId.trim()) return { error: "列車種別を選択してください" };
  const data = { trainTypeId: isNew ? randomUUID() : trainType.trainTypeId, displayName };

  try {
    const response = await fetch(`http://127.0.0.1:8080/api/train-types${isNew ? "" : `/${encodeURIComponent(data.trainTypeId)}`}`, {
      method: isNew ? "POST" : "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(isNew ? data : { displayName: data.displayName }),
    });
    if (!response.ok) return { error: (await response.text()).trim() || "列車種別の保存に失敗しました" };
    return { data };
  } catch {
    return { error: "列車種別の保存に失敗しました" };
  }
}
