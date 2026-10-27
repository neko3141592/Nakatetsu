"use server";

import type { Station, StationDetail, TrainType } from "@/types/masters";
import type { TimetableTrain, TimetableTrainInput } from "@/types/train";

type Result<T> = { data: T; error?: never } | { data?: never; error: string };

export async function loadTrain(number: string): Promise<Result<TimetableTrain>> {
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/trains/${encodeURIComponent(number)}`, { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "列車の取得に失敗しました" };
    return { data: await response.json() };
  } catch {
    return { error: "列車の取得に失敗しました" };
  }
}

export async function loadTrainTypes(): Promise<Result<TrainType[]>> {
  try {
    const response = await fetch("http://127.0.0.1:8080/api/train-types", { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "列車種別の取得に失敗しました" };
    return { data: await response.json() };
  } catch {
    return { error: "列車種別の取得に失敗しました" };
  }
}

export async function loadStations(): Promise<Result<StationDetail[]>> {
  try {
    const response = await fetch("http://127.0.0.1:8080/api/stations", { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "駅一覧の取得に失敗しました" };
    const stations: Station[] = await response.json();
    const details = await Promise.all(stations.map(async (station) => {
      const detail = await fetch(`http://127.0.0.1:8080/api/stations/${encodeURIComponent(station.stationId)}`, { cache: "no-store" });
      if (!detail.ok) throw new Error("駅情報の取得に失敗しました");
      return detail.json() as Promise<StationDetail>;
    }));
    return { data: details };
  } catch {
    return { error: "駅一覧の取得に失敗しました" };
  }
}

export async function saveTrain(train: TimetableTrainInput, isNew: boolean): Promise<Result<TimetableTrainInput>> {
  const data = { ...train, trainNumber: isNew ? train.trainNumber.trim() : train.trainNumber };
  if (![data.trainNumber, data.trainTypeId, data.originStationId, data.destinationStationId].every((value) => typeof value === "string" && value.trim())) {
    return { error: "列番・列車種別・発駅・終着駅を入力してください" };
  }
  const { trainNumber, ...patch } = data;
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/trains${isNew ? "" : `/${encodeURIComponent(trainNumber)}`}`, {
      method: isNew ? "POST" : "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(isNew ? data : patch),
    });
    if (!response.ok) return { error: (await response.text()).trim() || "列車の保存に失敗しました" };
    return { data };
  } catch {
    return { error: "列車の保存に失敗しました" };
  }
}
