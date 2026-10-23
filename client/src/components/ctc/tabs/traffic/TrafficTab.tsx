"use client";

import { useState } from "react";

import { DiagramSceneContent, type DiagramSceneProps } from "@/components/ctc/diagram/DiagramSceneContent";
import { DiagramViewport } from "@/components/ctc/diagram/DiagramViewport";
import { InspectorToggle, InspectorWorkspace } from "@/components/ctc/inspector/InspectorPanel";
import { TrafficInspector } from "@/components/ctc/tabs/traffic/TrafficInspector";
import { useSidebar } from "@/components/ui/sidebar";
import type { InspectorSelection } from "@/lib/inspectorTypes";

type Props = Pick<DiagramSceneProps, "scene" | "snapshot"> & {
  active: boolean;
};

export function TrafficTab(props: Props) {
  return (
    <InspectorWorkspace active={props.active}>
      <TrafficTabContent {...props} />
    </InspectorWorkspace>
  );
}

function TrafficTabContent({ scene, snapshot, active }: Props) {
  const [selection, setSelection] = useState<InspectorSelection>(null);
  const { isMobile, setOpen, setOpenMobile } = useSidebar();

  function selectTrain(nextSelection: NonNullable<InspectorSelection>) {
    setSelection(nextSelection);
    if (isMobile) setOpenMobile(true);
    else setOpen(true);
  }

  return (
    <div className="absolute inset-0 flex min-h-0">
      <section
        aria-label="配線図キャンバス"
        className="relative min-h-0 min-w-0 flex-1"
      >
        <InspectorToggle
          id="traffic-inspector"
          title="列車在線情報のインスペクタ"
          className="absolute right-2 top-2 z-20"
        />
        <DiagramViewport width={scene.width} height={scene.height}>
          <DiagramSceneContent
            scene={scene}
            snapshot={snapshot}
            mode="traffic"
            selectedTrainId={selection?.trainId}
            onSelectTrain={selectTrain}
          />
        </DiagramViewport>
      </section>
      <TrafficInspector
        active={active}
        selection={selection}
        train={selection ? snapshot?.trainsById[selection.trainId] : undefined}
      />
    </div>
  );
}
