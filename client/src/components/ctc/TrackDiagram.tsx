"use client";

import { useLayoutEffect, useMemo, useRef } from "react";

import { TurnoutPosition, type CtcSnapshot, type InterlockingStationStatus } from "@/gen/ctc_pb";

export type CircuitSvgMap = Readonly<Record<string,
  | { svgId: string }
  | { turnoutId: string; normalSvgId: string; reverseSvgId: string }
>>;

export type RouteDefinition = {
  routeId: string;
  trackCircuitIds: readonly string[];
  requiredTurnouts: readonly {
    connectionId: string;
    requiredPosition: TurnoutPosition;
  }[];
};

type Props = {
  /** 管理下のSVGファイルの全文。URLではなく文字列を渡す。 */
  svg: string;
  circuitSvgMap: CircuitSvgMap;
  circuitsById?: CtcSnapshot["circuitsById"];
  turnoutsById?: CtcSnapshot["turnoutsById"];
  station?: InterlockingStationStatus;
  routeDefinitions: readonly RouteDefinition[];
  label?: string;
};

const INACTIVE_COLOR = "#ffffff";
const OCCUPIED_COLOR = "#ef4444";
const RESERVED_COLOR = "#ffd517";

/** 全体の <svg> 内に配置する、駅・駅間1つ分の配線図。 */
export function TrackDiagram({
  svg,
  circuitSvgMap,
  circuitsById,
  turnoutsById,
  station,
  routeDefinitions,
  label = "配線図",
}: Props) {
  const rootRef = useRef<SVGGElement>(null);
  const markup = useMemo(() => ({ __html: svg }), [svg]);

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
        // 転換中・位置不明・未取得では在線する分岐を特定できないため、両側に在線を表示する。
        const positionKnown = position === TurnoutPosition.NORMAL || position === TurnoutPosition.REVERSE;
        const normalReserved = circuitReservedRoutes.some((route) => route.requiredTurnouts.some(
          (required) => required.connectionId === mapping.turnoutId && required.requiredPosition === TurnoutPosition.NORMAL,
        ));
        const reverseReserved = circuitReservedRoutes.some((route) => route.requiredTurnouts.some(
          (required) => required.connectionId === mapping.turnoutId && required.requiredPosition === TurnoutPosition.REVERSE,
        ));

        setStroke(mapping.normalSvgId, occupied && (!positionKnown || position === TurnoutPosition.NORMAL), normalReserved);
        setStroke(mapping.reverseSvgId, occupied && (!positionKnown || position === TurnoutPosition.REVERSE), reverseReserved);
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

  return <g ref={rootRef} role="img" aria-label={label} dangerouslySetInnerHTML={markup} />;
}
