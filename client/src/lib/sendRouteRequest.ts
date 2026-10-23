import { create, fromJson, toJson } from "@bufbuild/protobuf";

import {
  InterlockingRouteOperation,
  InterlockingRouteRequestSchema,
  InterlockingRouteRequestResultSchema,
} from "@/gen/ctc_pb";

export async function sendRouteRequest(
  stationId: string,
  routeId: string,
  operation: InterlockingRouteOperation = InterlockingRouteOperation.SET,
) {
  const request = create(InterlockingRouteRequestSchema, {
    stationId,
    routeId,
    operation,
  });
  const response = await fetch("/api/route-requests", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toJson(InterlockingRouteRequestSchema, request)),
    signal: AbortSignal.timeout(8000),
  });
  if (!response.ok) {
    throw new Error((await response.text()).trim() || `HTTP ${response.status}`);
  }
  return fromJson(InterlockingRouteRequestResultSchema, await response.json());
}
