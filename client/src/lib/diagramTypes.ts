import type { CircuitSvgMap, RouteDefinition } from "@/components/ctc/TrackDiagram";

export type DiagramLayout = {
  width: number;
  height: number;
  turnoutIndicators: readonly {
    id: string;
    turnoutId: string;
    label: string;
    x: number;
    y: number;
  }[];
  routeIndicator: { id: string; x: number; y: number } | null;
  routeIds: readonly string[];
};

export type DiagramScene = {
  width: number;
  height: number;
  diagrams: readonly {
    id: string;
    stationId?: string;
    x: number;
    y: number;
    svg: string;
    circuitSvgMap: CircuitSvgMap;
    routeDefinitions: readonly RouteDefinition[];
    layout: DiagramLayout;
  }[];
};
