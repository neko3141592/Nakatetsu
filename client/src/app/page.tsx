import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

import CtcLayout from "@/components/ctc/CtcLayout";
import type { CircuitSvgMap, RouteDefinition } from "@/components/ctc/diagram/TrackDiagram";
import lineLayout from "@/data/diagrams/NtLine.json";
import type { DiagramLayout, DiagramScene } from "@/lib/diagramTypes";
import { getRouteCategory } from "@/lib/routeCategory";

function readJson<T>(filePath: string): T {
  return JSON.parse(readFileSync(filePath, "utf8"));
}

type Placement = { id: string; stationId?: string; x: number; y: number };
const placements: readonly Placement[] = lineLayout.diagrams;
const diagramLayouts = import.meta.glob<DiagramLayout>("../data/diagrams/*.layout.json", {
  eager: true,
  import: "default",
});
const diagrams = placements.map((placement) => {
  const layout = diagramLayouts[`../data/diagrams/${placement.id}.layout.json`];
  if (!layout) throw new Error(`Missing diagram layout: ${placement.id}`);
  const interlockingPath = placement.stationId
    ? path.join(
        process.cwd(),
        "../SourceAssets/Track/Network/NakatetsuInterlocking/Stations",
        `${placement.stationId}.json`,
      )
    : undefined;
  return {
    ...placement,
    svg: readFileSync(
      path.join(process.cwd(), "public/ctc/diagram", `${placement.id}.svg`),
      "utf8",
    ),
    circuitSvgMap: readJson<CircuitSvgMap>(
      path.join(process.cwd(), "src/data/diagrams", `${placement.id}.json`),
    ),
    layout,
    routeDefinitions: interlockingPath && existsSync(interlockingPath)
      ? readJson<{ routes: RouteDefinition[] }>(interlockingPath).routes.map((route) => ({
          ...route,
          category: getRouteCategory(route.category),
        }))
      : [],
  };
});
const scene: DiagramScene = {
  width: Math.max(...diagrams.map((diagram) => diagram.x + diagram.layout.width)),
  height: Math.max(...diagrams.map((diagram) => diagram.y + diagram.layout.height)),
  diagrams,
};

export default function Home() {
  return <CtcLayout scene={scene} />;
}
