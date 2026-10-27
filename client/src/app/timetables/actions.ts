"use server";

import { randomUUID } from "node:crypto";
import type { Timetable, TimetableSummary } from "@/types/timetable";

type Result<T> = { data: T; error?: never } | { data?: never; error: string };

export async function deleteTimetable(id: string): Promise<Result<null>> {
  if (!id.trim()) return { error: "保存済みのダイヤを選択してください" };
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/timetable/${encodeURIComponent(id)}`, { method: "DELETE" });
    if (!response.ok) return { error: (await response.text()).trim() || "ダイヤの削除に失敗しました" };
    return { data: null };
  } catch {
    return { error: "ダイヤの削除に失敗しました" };
  }
}

export async function loadTimetable(id: string): Promise<Result<Timetable>> {
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/timetable/${encodeURIComponent(id)}`, { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "ダイヤの取得に失敗しました" };
    return { data: await response.json() };
  } catch {
    return { error: "ダイヤの取得に失敗しました" };
  }
}

export async function saveTimetable(timetable: TimetableSummary & { operationNumbers: string[] }): Promise<Result<Timetable>> {
  const displayName = timetable.displayName.trim();
  if (!displayName) return { error: "ダイヤ名を入力してください" };

  const isNew = timetable.id === "";
  const data = { id: isNew ? randomUUID() : timetable.id, displayName, revision: timetable.revision };
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/timetable/${isNew ? "" : encodeURIComponent(data.id)}`, {
      method: isNew ? "POST" : "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(isNew ? { ...data, operationNumbers: timetable.operationNumbers } : { displayName, operationNumbers: timetable.operationNumbers }),
    });
    if (!response.ok) return { error: (await response.text()).trim() || "ダイヤの保存に失敗しました" };
    return isNew ? { data: await response.json() } : loadTimetable(data.id);
  } catch {
    return { error: "ダイヤの保存に失敗しました" };
  }
}
