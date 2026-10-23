import type { DiagramScene } from "@/lib/diagramTypes";

export type RouteControlStation = {
  stationId: string;
  displayName: string;
  diagramIds: readonly string[];
};

export function getStationDiagramScene(
  scene: DiagramScene,
  station: RouteControlStation,
): DiagramScene {
  if (station.diagramIds.length === 0) {
    throw new Error(`No diagrams configured for station "${station.stationId}".`);
  }

  const diagramsById = new Map(scene.diagrams.map((diagram) => [diagram.id, diagram]));
  const selectedIds = new Set<string>();
  const selected = station.diagramIds.map((id) => {
    if (selectedIds.has(id)) {
      throw new Error(`Duplicate diagram "${id}" for station "${station.stationId}".`);
    }
    selectedIds.add(id);

    const diagram = diagramsById.get(id);
    if (!diagram) {
      throw new Error(`Diagram "${id}" for station "${station.stationId}" was not found.`);
    }
    return diagram;
  });

  const left = Math.min(...selected.map((diagram) => diagram.x));
  const top = Math.min(...selected.map((diagram) => diagram.y));

  return {
    width: Math.max(...selected.map((diagram) => diagram.x + diagram.layout.width)) - left,
    height: Math.max(...selected.map((diagram) => diagram.y + diagram.layout.height)) - top,
    diagrams: selected.map((diagram) => ({
      ...diagram,
      x: diagram.x - left,
      y: diagram.y - top,
    })),
  };
}
