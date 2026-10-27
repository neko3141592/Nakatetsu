"use server";

import { randomUUID } from "node:crypto";
import type { RunningPattern, RunningPatternSummary } from "@/types/runningPattern";

type Result<T> = { data: T; error?: never } | { data?: never; error: string };
const endpoint = "http://127.0.0.1:8080/api/running-patterns";

export async function loadRunningPatterns(trainTypeId: string): Promise<Result<RunningPatternSummary[]>> {
  try {
    const response = await fetch(`${endpoint}?trainTypeId=${encodeURIComponent(trainTypeId)}`, { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "走行パターン一覧の取得に失敗しました" };
    return { data: await response.json() };
  } catch { return { error: "走行パターン一覧の取得に失敗しました" }; }
}

export async function loadRunningPattern(id: string): Promise<Result<RunningPattern>> {
  try {
    const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { cache: "no-store" });
    if (!response.ok) return { error: (await response.text()).trim() || "走行パターンの取得に失敗しました" };
    return { data: await response.json() };
  } catch { return { error: "走行パターンの取得に失敗しました" }; }
}

export async function saveRunningPattern(pattern: RunningPattern): Promise<Result<RunningPattern>> {
  const data = { ...pattern, id: pattern.id || randomUUID(), displayName: pattern.displayName.trim() };
  if (!data.displayName || !data.trainTypeId) return { error: "名称と保存済みの列車種別を指定してください" };
  try {
    const response = await fetch(pattern.id ? `${endpoint}/${encodeURIComponent(pattern.id)}` : endpoint, {
      method: pattern.id ? "PATCH" : "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(pattern.id ? { displayName: data.displayName, points: data.points } : data),
    });
    if (!response.ok) return { error: (await response.text()).trim() || "走行パターンの保存に失敗しました" };
    return { data };
  } catch { return { error: "走行パターンの保存に失敗しました" }; }
}

export async function deleteRunningPattern(id: string): Promise<Result<null>> {
  try {
    const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { method: "DELETE" });
    if (!response.ok) return { error: (await response.text()).trim() || "走行パターンの削除に失敗しました" };
    return { data: null };
  } catch { return { error: "走行パターンの削除に失敗しました" }; }
}
