import type { RouteDefinition } from "@/components/ctc/TrackDiagram";
import type { InterlockingStationStatus } from "@/gen/ctc_pb";

export function getLockedTurnoutIds(
  station: InterlockingStationStatus | undefined,
  routeDefinitions: readonly RouteDefinition[],
): Set<string> {
  if (!station?.isInitialized || !station.hasOutput) return new Set();

  return new Set(routeDefinitions.flatMap((route) => {
    const status = station.routesById[route.routeId];
    return status?.routeLocked || status?.approachLocked
      ? route.requiredTurnouts.map((turnout) => turnout.connectionId)
      : [];
  }));
}
