import { InspectorPanel } from "@/components/ctc/inspector/InspectorPanel";
import { TrainInspector } from "@/components/ctc/inspector/TrainInspector";
import type { CtcTrainState } from "@/gen/ctc_pb";
import type { InspectorSelection } from "@/lib/inspectorTypes";

type Props = {
  active: boolean;
  selection: InspectorSelection;
  train: CtcTrainState | undefined;
};

export function TrafficInspector({ selection, train, ...panelProps }: Props) {
  return (
    <InspectorPanel {...panelProps} id="traffic-inspector" title="列車在線情報のインスペクタ">
      {selection ? (
        <TrainInspector trainId={selection.trainId} train={train} />
      ) : (
        <p className="p-4 text-sm text-zinc-400">列番を選択してください。</p>
      )}
    </InspectorPanel>
  );
}
