"use client";

import { useLayoutEffect, useMemo, useRef, type SyntheticEvent } from "react";

import { TurnoutPosition, type CtcSnapshot, type InterlockingStationStatus } from "@/gen/ctc_pb";

export type CircuitSvgMap = Readonly<Record<string,
  | { svgId: string }
  | { turnoutId: string; normalSvgId: string; reverseSvgId: string }
>>;

export type RouteDefinition = {
  routeId: string;
  startTrackCircuitId: string;
  destinationTrackCircuitId: string;
  destinationCode: string;
  trackCircuitIds: readonly string[];
  requiredTurnouts: readonly {
    connectionId: string;
    requiredPosition: TurnoutPosition;
  }[];
};

export type RouteDirection = "R" | "L";

type Props = {
  /** 管理下のSVGファイルの全文。URLではなく文字列を渡す。 */
  svg: string;
  circuitSvgMap: CircuitSvgMap;
  circuitsById?: CtcSnapshot["circuitsById"];
  turnoutsById?: CtcSnapshot["turnoutsById"];
  station?: InterlockingStationStatus;
  routeDefinitions: readonly RouteDefinition[];
  selectedOriginCircuitId?: string;
  selectedOriginDirection?: RouteDirection;
  availableDestinationCircuitIds?: readonly string[];
  isSendingRoute?: boolean;
  onSelectOrigin?: (circuitId: string, direction?: RouteDirection) => void;
  onSelectDestination?: (circuitId: string) => void;
  label?: string;
};

const INACTIVE_COLOR = "#ffffff";
const OCCUPIED_COLOR = "#ef4444";
const RESERVED_COLOR = "#ffd517";
const NO_DESTINATIONS: readonly string[] = [];

/** 全体の <svg> 内に配置する、駅・駅間1つ分の配線図。 */
export function TrackDiagram({
  svg,
  circuitSvgMap,
  circuitsById,
  turnoutsById,
  station,
  routeDefinitions,
  selectedOriginCircuitId,
  selectedOriginDirection,
  availableDestinationCircuitIds = NO_DESTINATIONS,
  isSendingRoute = false,
  onSelectOrigin,
  onSelectDestination,
  label = "配線図",
}: Props) {
  const rootRef = useRef<SVGGElement>(null);
  const markup = useMemo(() => ({ __html: svg }), [svg]);
  const interactive = Boolean(onSelectOrigin && onSelectDestination);

  useLayoutEffect(() => {
    if (!interactive) return;
    const root = rootRef.current;
    if (!root) return;
    const candidates = new Set(availableDestinationCircuitIds);
    const markers = root.querySelectorAll<SVGGElement>('g[id^="Origin_"], g[id^="Destination_"]');
    for (const marker of markers) {
      const isOrigin = marker.id.startsWith("Origin_");
      const circuitId = marker.dataset.circuitId;
      const available = Boolean(circuitId && candidates.has(circuitId));
      const enabled = (isOrigin || available) && !isSendingRoute;
      const selected = isOrigin && circuitId === selectedOriginCircuitId && marker.dataset.routeDirection === selectedOriginDirection;
      marker.toggleAttribute("data-route-button", enabled);
      marker.toggleAttribute("data-route-candidate", !isOrigin && available);
      marker.toggleAttribute("data-route-selected", selected);
      marker.setAttribute("role", "button");
      marker.setAttribute("tabindex", enabled ? "0" : "-1");
      marker.setAttribute("aria-disabled", String(!enabled));
      marker.setAttribute("aria-label", isOrigin ? `発点 ${circuitId}${marker.dataset.routeDirection ? ` ${marker.dataset.routeDirection}` : ""}` : `着点 ${marker.dataset.destinationCode ?? circuitId}`);
      if (isOrigin) marker.setAttribute("aria-pressed", String(selected));
    }
  }, [svg, interactive, selectedOriginCircuitId, selectedOriginDirection, availableDestinationCircuitIds, isSendingRoute]);

  function getButton(event: SyntheticEvent<SVGGElement>) {
    if (!(event.target instanceof Element)) return null;
    const marker = event.target.closest<SVGGElement>("[data-route-button]");
    return marker && rootRef.current?.contains(marker) ? marker : null;
  }

  function selectButton(marker: SVGGElement) {
    const circuitId = marker.dataset.circuitId;
    if (!circuitId) return;
    const direction = marker.dataset.routeDirection;
    if (marker.id.startsWith("Origin_")) onSelectOrigin?.(circuitId, direction === "R" || direction === "L" ? direction : undefined);
    else onSelectDestination?.(circuitId);
  }

  useLayoutEffect(() => {
    const root = rootRef.current;
    if (!root) return;

    const elements = new Map(
      Array.from(root.querySelectorAll<SVGElement>("[id]"), (element) => [element.id, element]),
    );

    const originalStrokes = new Map<SVGElement, { value: string; priority: string }>();

    function setStroke(svgId: string, occupied: boolean, reserved: boolean) {
      const element = elements.get(svgId);
      if (!element) return;

      if (!originalStrokes.has(element)) {
        originalStrokes.set(element, {
          value: element.style.getPropertyValue("stroke"),
          priority: element.style.getPropertyPriority("stroke"),
        });
      }
      const color = occupied ? OCCUPIED_COLOR : reserved ? RESERVED_COLOR : INACTIVE_COLOR;
      element.style.setProperty("stroke", color);
    }

    const reservedRoutes = station?.isInitialized && station.hasOutput
      ? routeDefinitions.filter((route) => station.routesById[route.routeId]?.isRouteSet === true)
      : [];

    for (const [circuitId, mapping] of Object.entries(circuitSvgMap)) {
      const occupied = circuitsById?.[circuitId]?.isOccupied === true;
      const circuitReservedRoutes = reservedRoutes.filter((route) => route.trackCircuitIds.includes(circuitId));
      const reserved = circuitReservedRoutes.length > 0;

      if ("turnoutId" in mapping) {
        const turnout = turnoutsById?.[mapping.turnoutId];
        const position = turnout?.isMoving ? undefined : turnout?.actualPosition;
        const normalReserved = circuitReservedRoutes.some((route) => route.requiredTurnouts.some(
          (required) => required.connectionId === mapping.turnoutId && required.requiredPosition === TurnoutPosition.NORMAL,
        ));
        const reverseReserved = circuitReservedRoutes.some((route) => route.requiredTurnouts.some(
          (required) => required.connectionId === mapping.turnoutId && required.requiredPosition === TurnoutPosition.REVERSE,
        ));

        setStroke(mapping.normalSvgId, occupied && position === TurnoutPosition.NORMAL, normalReserved);
        setStroke(mapping.reverseSvgId, occupied && position === TurnoutPosition.REVERSE, reverseReserved);
      } else {
        setStroke(mapping.svgId, occupied, reserved);
      }
    }

    return () => {
      for (const [element, { value, priority }] of originalStrokes) {
        if (value) element.style.setProperty("stroke", value, priority);
        else element.style.removeProperty("stroke");
      }
    };
  }, [svg, circuitSvgMap, circuitsById, turnoutsById, station, routeDefinitions]);

  return (
    <g
      ref={rootRef}
      role={interactive ? "group" : "img"}
      aria-label={label}
      aria-busy={interactive ? isSendingRoute : undefined}
      className={interactive ? "ctc-route-controls" : undefined}
      onMouseDown={(event) => { if (getButton(event)) event.stopPropagation(); }}
      onTouchStart={(event) => { if (getButton(event)) event.stopPropagation(); }}
      onClick={(event) => {
        const button = getButton(event);
        if (!button) return;
        event.stopPropagation();
        selectButton(button);
      }}
      onKeyDown={(event) => {
        if (event.key !== "Enter" && event.key !== " ") return;
        const button = getButton(event);
        if (!button) return;
        event.preventDefault();
        event.stopPropagation();
        selectButton(button);
      }}
      dangerouslySetInnerHTML={markup}
    />
  );
}
