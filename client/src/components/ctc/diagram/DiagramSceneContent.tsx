"use client";

import { DiagramTrainLabels } from "@/components/ctc/diagram/DiagramTrainLabels";
import { TrackDiagram, type DiagramMode, type RouteDirection } from "@/components/ctc/diagram/TrackDiagram";
import { TurnoutNumber } from "@/components/ctc/diagram/TurnoutNumber";
import type { CtcSnapshot } from "@/gen/ctc_pb";
import type { DiagramScene } from "@/lib/diagramTypes";
import type { InspectorSelection } from "@/lib/inspectorTypes";

export type DiagramSceneProps = {
  scene: DiagramScene;
  snapshot: CtcSnapshot | undefined;
  selectedTrainId?: string;
  onSelectTrain: (selection: NonNullable<InspectorSelection>) => void;
};

export type DiagramSceneContentProps = DiagramSceneProps & {
  mode: DiagramMode;
  origin?: {
    stationId: string;
    circuitId: string;
    direction?: RouteDirection;
  } | null;
  availableDestinationCircuitIds?: readonly string[];
  isSendingRoute?: boolean;
  onSelectOrigin?: (stationId: string, circuitId: string, direction?: RouteDirection) => void;
  onSelectDestination?: (stationId: string, circuitId: string) => void;
};

const NO_DESTINATIONS: readonly string[] = [];

export function DiagramSceneContent({
  mode,
  scene,
  snapshot,
  selectedTrainId,
  onSelectTrain,
  origin = null,
  availableDestinationCircuitIds = NO_DESTINATIONS,
  isSendingRoute = false,
  onSelectOrigin,
  onSelectDestination,
}: DiagramSceneContentProps) {
  return (
    <>
      {scene.diagrams.map((diagram) => {
        const stationId = diagram.stationId;
        const station = stationId
          ? snapshot?.interlocking?.stationsById[stationId]
          : undefined;
        return (
          <g
            key={diagram.id}
            data-diagram-id={diagram.id}
            transform={`translate(${diagram.x} ${diagram.y})`}
          >
            <TrackDiagram
              mode={mode}
              svg={diagram.svg}
              circuitSvgMap={diagram.circuitSvgMap}
              circuitsById={snapshot?.circuitsById}
              turnoutsById={snapshot?.turnoutsById}
              station={station}
              routeDefinitions={diagram.routeDefinitions}
              selectedOriginCircuitId={origin?.stationId === stationId ? origin?.circuitId : undefined}
              selectedOriginDirection={origin?.stationId === stationId ? origin?.direction : undefined}
              availableDestinationCircuitIds={origin?.stationId === stationId ? availableDestinationCircuitIds : NO_DESTINATIONS}
              isSendingRoute={isSendingRoute}
              onSelectOrigin={stationId && onSelectOrigin ? (circuitId, direction) => onSelectOrigin(stationId, circuitId, direction) : undefined}
              onSelectDestination={stationId && onSelectDestination ? (circuitId) => onSelectDestination(stationId, circuitId) : undefined}
              label={`${diagram.id}の配線図`}
            />
            {mode === "route-control" && diagram.layout.turnoutIndicators.length > 0 ? (
              <g aria-label={`${diagram.id}の転轍機番号`}>
                {diagram.layout.turnoutIndicators.map(({ id, turnoutId, label, x, y }) => (
                  <g
                    key={id}
                    data-turnout-indicator-id={id}
                    data-turnout-id={turnoutId}
                    transform={`translate(${x} ${y})`}
                  >
                    <TurnoutNumber label={label} />
                  </g>
                ))}
              </g>
            ) : null}
            <DiagramTrainLabels
              markers={diagram.layout.trainLabels}
              circuitSvgMap={diagram.circuitSvgMap}
              circuitsById={snapshot?.circuitsById}
              trainsById={snapshot?.trainsById}
              turnoutsById={snapshot?.turnoutsById}
              selectedTrainId={selectedTrainId}
              onSelect={onSelectTrain}
            />
          </g>
        );
      })}
    </>
  );
}
