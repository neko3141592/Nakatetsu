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

export async function loadTrains(numbers: string[]): Promise<Result<TimetableTrain[]>> {
  const results = await Promise.all(numbers.map(loadTrain));
  const failed = results.find((result) => result.error !== undefined);
  if (failed) return { error: failed.error! };
  return { data: results.flatMap((result) => result.data ? [result.data] : []) };
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

export async function saveTrain(train: TimetableTrainInput, id?: string): Promise<Result<TimetableTrain>> {
  const isNew = !id;
  const data = { ...train, trainNumber: train.trainNumber.trim() };
  if (![data.trainNumber, data.trainTypeId, data.originStationId, data.destinationStationId].every((value) => typeof value === "string" && value.trim())) {
    return { error: "列番・列車種別・発駅・終着駅を入力してください" };
  }
  const { timetableId, ...patch } = data;
  if (!timetableId) return { error: "先にダイヤを保存してください" };
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/trains${isNew ? "" : `/${encodeURIComponent(id!)}`}`, {
      method: isNew ? "POST" : "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(isNew ? data : patch),
    });
    if (!response.ok) return { error: (await response.text()).trim() || "列車の保存に失敗しました" };
    return isNew ? { data: await response.json() } : loadTrain(id!);
  } catch {
    return { error: "列車の保存に失敗しました" };
  }
}

export async function deleteTrain(id: string): Promise<Result<null>> {
 try { const response = await fetch(`http://127.0.0.1:8080/api/trains/${encodeURIComponent(id)}`, { method: "DELETE" });
 if (!response.ok) return { error: (await response.text()).trim() }; return { data: null };
 } catch { return { error: "列車の削除に失敗しました" }; }
}

export async function loadTrainList(timetableId?: string): Promise<Result<import("@/types/train").TimetableTrainSummary[]>> {
 try { const response = await fetch(`http://127.0.0.1:8080/api/trains${timetableId ? `?timetableId=${encodeURIComponent(timetableId)}` : ""}`, { cache: "no-store" });
 if (!response.ok) return { error: (await response.text()).trim() }; return { data: await response.json() };
 } catch { return { error: "列車一覧の取得に失敗しました" }; }
}

export async function copyTrain(id: string, timetableId: string): Promise<Result<TimetableTrain>> {
 const source = await loadTrain(id); if (source.error !== undefined) return source;
 const train = source.data;
 return saveTrain({ timetableId, trainNumber: train.trainNumber, trainTypeId: train.trainTypeId, originStationId: train.originStationId, destinationStationId: train.destinationStationId,
 points: (train.points ?? []).map(({ sequenceIndex, section, stationId, trackCircuitId, handling, arrivalSeconds, departureSeconds }) => ({ sequenceIndex, section, stationId, trackCircuitId, handling, arrivalSeconds, departureSeconds, routes: [] })) });
}
