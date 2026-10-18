import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

import CtcLayout from "@/components/ctc/CtcLayout";
import type { CircuitSvgMap, RouteDefinition } from "@/components/ctc/TrackDiagram";
import lineLayout from "@/data/diagrams/NtLine.json";
import type { DiagramLayout, DiagramScene } from "@/lib/diagramTypes";

function readJson<T>(filePath: string): T {
  return JSON.parse(readFileSync(filePath, "utf8"));
}

type Placement = { id: string; stationId?: string; x: number; y: number };
const placements: readonly Placement[] = lineLayout.diagrams;
const diagrams = placements.map((placement) => {
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
    layout: readJson<DiagramLayout>(
      path.join(process.cwd(), "src/data/diagrams", `${placement.id}.layout.json`),
    ),
    routeDefinitions: interlockingPath && existsSync(interlockingPath)
      ? readJson<{ routes: RouteDefinition[] }>(interlockingPath).routes
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
