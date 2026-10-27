"use server";

import { randomUUID } from "node:crypto";
import type { TrainTypeDetail } from "@/types/masters";

type Result<T> = { data: T; error?: never } | { data?: never; error: string };

export async function loadTrainType(id: string): Promise<Result<TrainTypeDetail>> {
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/train-types/${encodeURIComponent(id)}`, { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "列車種別の取得に失敗しました" };
    return { data: await response.json() };
  } catch {
    return { error: "列車種別の取得に失敗しました" };
  }
}

export async function saveTrainType(trainType: TrainTypeDetail, isNew: boolean): Promise<Result<TrainTypeDetail>> {
  const displayName = trainType.displayName.trim();
  if (!displayName) return { error: "名称を入力してください" };
  if (!/^#[0-9a-f]{6}$/i.test(trainType.color)) return { error: "色は#RRGGBB形式で指定してください" };
  if (!isNew && !trainType.trainTypeId.trim()) return { error: "列車種別を選択してください" };
  const data = { trainTypeId: isNew ? randomUUID() : trainType.trainTypeId, displayName, color: trainType.color, stationHandlings: trainType.stationHandlings };

  try {
    const response = await fetch(`http://127.0.0.1:8080/api/train-types${isNew ? "" : `/${encodeURIComponent(data.trainTypeId)}`}`, {
      method: isNew ? "POST" : "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(isNew ? data : { displayName: data.displayName, color: data.color, stationHandlings: data.stationHandlings }),
    });
    if (!response.ok) return { error: (await response.text()).trim() || "列車種別の保存に失敗しました" };
    return { data };
  } catch {
    return { error: "列車種別の保存に失敗しました" };
  }
}
