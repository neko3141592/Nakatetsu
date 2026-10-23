import { TrainLabel } from "@/components/ctc/diagram/TrainLabel";
import type { CircuitSvgMap } from "@/components/ctc/diagram/TrackDiagram";
import { TurnoutPosition, type CtcSnapshot } from "@/gen/ctc_pb";
import type { DiagramLayout } from "@/lib/diagramTypes";
import type { InspectorSelection } from "@/lib/inspectorTypes";

type Props = {
  markers: DiagramLayout["trainLabels"];
  circuitSvgMap: CircuitSvgMap;
  circuitsById: CtcSnapshot["circuitsById"] | undefined;
  trainsById: CtcSnapshot["trainsById"] | undefined;
  turnoutsById: CtcSnapshot["turnoutsById"] | undefined;
  selectedTrainId: string | undefined;
  onSelect: (selection: NonNullable<InspectorSelection>) => void;
};

export function DiagramTrainLabels({
  markers, circuitSvgMap, circuitsById, trainsById, turnoutsById,
  selectedTrainId, onSelect,
}: Props) {
  const displayedTrainIds = new Set<string>();

  return (
    <g aria-label="列車情報">
      {markers.map((marker) => {
        const circuit = circuitsById?.[marker.circuitId];
        if (!circuit?.isOccupied || !circuit.trainId || displayedTrainIds.has(circuit.trainId)) return null;
        const train = trainsById?.[circuit.trainId];
        const mapping = circuitSvgMap[marker.circuitId];
        if (!train || !mapping) return null;

        if ("turnoutId" in mapping) {
          const turnout = turnoutsById?.[mapping.turnoutId];
          if (!turnout || turnout.isMoving) return null;
          const activeSvgId = turnout.actualPosition === TurnoutPosition.NORMAL
            ? mapping.normalSvgId
            : turnout.actualPosition === TurnoutPosition.REVERSE ? mapping.reverseSvgId : undefined;
          if (marker.svgId !== activeSvgId) return null;
        } else if (marker.svgId !== mapping.svgId) {
          return null;
        }

        displayedTrainIds.add(circuit.trainId);
        const trainNumber = train.trainNumber.trim();
        const label = trainNumber ? `${trainNumber}レ` : "列番未設定";
        return (
          <g
            key={marker.id}
            role="button"
            tabIndex={0}
            aria-label={`${label}を選択`}
            aria-pressed={selectedTrainId === circuit.trainId}
            className="cursor-pointer focus-visible:outline-1 focus-visible:outline-offset-2 focus-visible:outline-green-400"
            data-train-label-marker-id={marker.id}
            data-train-id={circuit.trainId}
            data-train-circuit-id={marker.circuitId}
            transform={`translate(${marker.x} ${marker.y})`}
            onClick={() => onSelect({ type: "train", trainId: circuit.trainId })}
            onKeyDown={(event) => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                onSelect({ type: "train", trainId: circuit.trainId });
              }
            }}
          >
            <TrainLabel train={train} />
          </g>
        );
      })}
    </g>
  );
}
