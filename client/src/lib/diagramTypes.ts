import type { CircuitSvgMap, RouteDefinition } from "@/components/ctc/diagram/TrackDiagram";

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
  trainLabels: readonly {
    id: string;
    circuitId: string;
    svgId: string;
    x: number;
    y: number;
  }[];
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
