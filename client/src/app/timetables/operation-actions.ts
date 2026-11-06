"use server";
import type {
  OperationPlan,
  OperationPlanSummary,
  TimetableOperation,
} from "@/types/operation";
type Result<T> = { data: T; error?: never } | { data?: never; error: string };
async function request<T>(
  path: string,
  method = "GET",
  body?: unknown,
): Promise<Result<T>> {
  try {
    const response = await fetch(`http://127.0.0.1:8080/api/${path}`, {
      method,
      cache: "no-store",
      headers: { "Content-Type": "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!response.ok)
      return {
        error: (await response.text()).trim() || "運用の処理に失敗しました",
      };
    return {
      data: response.status === 204 ? (null as T) : await response.json(),
    };
  } catch {
    return { error: "運用の処理に失敗しました" };
  }
}
export async function loadOperationPlans(timetableId: string) {
  return request<OperationPlanSummary[]>(
    `operation-plans?timetableId=${encodeURIComponent(timetableId)}`,
  );
}
export async function loadOperationPlan(id: string) {
  return request<OperationPlan>(`operation-plans/${encodeURIComponent(id)}`);
}
export async function saveOperationPlan(
  plan: Pick<OperationPlan, "id" | "timetableId" | "displayName">,
): Promise<Result<OperationPlan>> {
  const displayName = plan.displayName.trim();
  if (!displayName) return { error: "運用計画名を入力してください" };
  const result = await request<OperationPlan>(
    `operation-plans${plan.id ? `/${encodeURIComponent(plan.id)}` : ""}`,
    plan.id ? "PATCH" : "POST",
    plan.id ? { displayName } : { displayName, timetableId: plan.timetableId },
  );
  return result.error !== undefined || !plan.id
    ? result
    : loadOperationPlan(plan.id);
}
export async function deleteOperationPlan(id: string) {
  return request<null>(`operation-plans/${encodeURIComponent(id)}`, "DELETE");
}
export async function deleteOperation(id: string) {
  return request<null>(`operations/${encodeURIComponent(id)}`, "DELETE");
}
export async function loadOperation(id: string) {
  return request<TimetableOperation>(`operations/${encodeURIComponent(id)}`);
}
export async function saveOperation(
  operation: TimetableOperation,
): Promise<Result<TimetableOperation>> {
  const operationNumber = operation.operationNumber.trim();
  if (!operationNumber) return { error: "運用番号を入力してください" };
  const trains = operation.trains.map(({ trainId }, sequenceIndex) => ({
    trainId,
    sequenceIndex,
  }));
  const result = await request<TimetableOperation>(
    `operations${operation.id ? `/${encodeURIComponent(operation.id)}` : ""}`,
    operation.id ? "PATCH" : "POST",
    operation.id
      ? { operationNumber, trains }
      : { operationPlanId: operation.operationPlanId, operationNumber, trains },
  );
  return result.error !== undefined || !operation.id
    ? result
    : loadOperation(operation.id);
}
