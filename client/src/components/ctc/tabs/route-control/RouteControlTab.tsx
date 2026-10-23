"use client";

import { useMemo, useRef, useState } from "react";

import { DiagramSceneContent, type DiagramSceneProps } from "@/components/ctc/diagram/DiagramSceneContent";
import { DiagramViewport } from "@/components/ctc/diagram/DiagramViewport";
import type { RouteDirection } from "@/components/ctc/diagram/TrackDiagram";
import { InspectorToggle, InspectorWorkspace } from "@/components/ctc/inspector/InspectorPanel";
import { RouteControlInspector } from "@/components/ctc/tabs/route-control/RouteControlInspector";
import { useSidebar } from "@/components/ui/sidebar";
import type { InspectorSelection } from "@/lib/inspectorTypes";
import { RouteControlHeader } from "@/components/ctc/tabs/route-control/RouteControlHeader";
import { InterlockingRouteOperation } from "@/gen/ctc_pb";
import routeControl from "@/data/diagrams/RouteControl.json";
import { getStationDiagramScene } from "@/lib/getStationDiagramScene";
import { getLockedTurnoutIds } from "@/lib/getLockedTurnoutIds";
import { sendRouteRequest } from "@/lib/sendRouteRequest";

type Props = Pick<DiagramSceneProps, "scene" | "snapshot"> & {
  active: boolean;
  onMessage: (message: string) => void;
};

export function RouteControlTab(props: Props) {
  return (
    <InspectorWorkspace active={props.active}>
      <RouteControlTabContent {...props} />
    </InspectorWorkspace>
  );
}

function RouteControlTabContent({ scene, snapshot, active, onMessage }: Props) {
  const [selection, setSelection] = useState<InspectorSelection>(null);
  const { isMobile, setOpen, setOpenMobile } = useSidebar();
  const [stationId, setStationId] = useState(routeControl.stations[0].stationId);
  const [origin, setOrigin] = useState<{ stationId: string; circuitId: string; direction?: RouteDirection } | null>(null);
  const [isSendingRoute, setIsSendingRoute] = useState(false);
  const requestPending = useRef(false);
  const station = routeControl.stations.find((item) => item.stationId === stationId)!;
  const stationScene = useMemo(() => getStationDiagramScene(scene, station), [scene, station]);
  const stationDiagram = stationScene.diagrams.find((diagram) => diagram.stationId === stationId);
  const stationStatus = snapshot?.interlocking?.stationsById[stationId];
  const turnouts = stationDiagram?.layout.turnoutIndicators ?? [];
  const lockedTurnoutIds = getLockedTurnoutIds(
    stationStatus,
    stationDiagram?.routeDefinitions ?? [],
  );
  const availableRoutes = useMemo(() => origin
    ? scene.diagrams.find((diagram) => diagram.stationId === origin.stationId)?.routeDefinitions
      .filter((route) => route.startTrackCircuitId === origin.circuitId &&
        (!origin.direction || route.routeId.match(/^\d+([RL])/)?.[1] === origin.direction)) ?? []
    : [], [origin, scene]);
  const availableDestinations = useMemo(
    () => Array.from(new Set(availableRoutes.map((route) => route.destinationTrackCircuitId))),
    [availableRoutes],
  );

  function selectTrain(nextSelection: NonNullable<InspectorSelection>) {
    setSelection(nextSelection);
    if (isMobile) setOpenMobile(true);
    else setOpen(true);
  }

  function selectOrigin(stationId: string, circuitId: string, direction?: RouteDirection) {
    if (requestPending.current) return;
    if (origin?.stationId === stationId && origin.circuitId === circuitId && origin.direction === direction) {
      setOrigin(null);
      onMessage("発点の選択を解除しました。");
      return;
    }
    setOrigin({ stationId, circuitId, direction });
    onMessage("発点が選択されました");
  }

  async function selectDestination(stationId: string, circuitId: string) {
    if (requestPending.current || origin?.stationId !== stationId) return;
    const routes = availableRoutes.filter((route) => route.destinationTrackCircuitId === circuitId);
    if (routes.length !== 1) {
      onMessage("進路を1つに特定できません。進路定義を確認してください。");
      return;
    }
    await requestRoute(stationId, routes[0].routeId, InterlockingRouteOperation.SET);
  }

  async function cancelRoute(routeId: string) {
    if (requestPending.current || !stationStatus?.isInitialized || !stationStatus.hasOutput) return;
    const route = stationStatus.routesById[routeId];
    if (!route?.isRouteSet || route.cancelPending || !stationDiagram?.routeDefinitions.some((definition) => definition.routeId === routeId)) return;
    await requestRoute(stationId, routeId, InterlockingRouteOperation.CANCEL);
  }

  async function requestRoute(stationId: string, routeId: string, operation: InterlockingRouteOperation) {
    if (requestPending.current) return;
    const action = operation === InterlockingRouteOperation.CANCEL ? "取り消し" : "設定";
    requestPending.current = true;
    setIsSendingRoute(true);
    onMessage(`${stationId} 進路 ${routeId} の${action}を要求しています…`);
    try {
      const result = await sendRouteRequest(stationId, routeId, operation);
      if (result.accepted) {
        if (operation === InterlockingRouteOperation.SET) setOrigin(null);
        onMessage(`${stationId} 進路 ${routeId} ${action}：受付済み。${result.message ?? ""}`);
      } else {
        onMessage(`${stationId} 進路 ${routeId} ${action}：拒否。${result.message || "要求は受け付けられませんでした。"}`);
      }
    } catch (error) {
      onMessage(`${stationId} 進路 ${routeId} ${action}：送信結果を確認できません。${error instanceof Error ? error.message : "通信エラー"}`);
    } finally {
      requestPending.current = false;
      setIsSendingRoute(false);
    }
  }

  return (
    <div className="absolute inset-0 flex min-h-0">
      <section
        aria-label="進路制御ワークスペース"
        data-station-id={stationId}
        className="relative flex min-h-0 min-w-0 flex-1 flex-col"
        onKeyDown={(event) => {
          if (event.key === "Escape" && origin && !requestPending.current) {
            setOrigin(null);
            onMessage("発点の選択を解除しました。");
          }
        }}
      >
        <RouteControlHeader
          stations={routeControl.stations}
          stationId={stationId}
          onStationChange={(nextStationId) => {
            setStationId(nextStationId);
            setOrigin(null);
          }}
          turnouts={turnouts}
          turnoutsById={snapshot?.turnoutsById}
          lockedTurnoutIds={lockedTurnoutIds}
        />
        <div className="relative min-h-0 flex-1">
          <InspectorToggle
            id="route-control-inspector"
            title="進路制御のインスペクタ"
            className="absolute right-2 top-2 z-20"
          />
          {active ? (
            <DiagramViewport
              key={stationId}
              width={stationScene.width}
              height={stationScene.height}
              label={`${station.displayName}の配線図`}
              fitLabel="駅全体を表示"
            >
              <DiagramSceneContent
                snapshot={snapshot}
                selectedTrainId={selection?.trainId}
                onSelectTrain={selectTrain}
                mode="route-control"
                scene={stationScene}
                origin={origin}
                availableDestinationCircuitIds={availableDestinations}
                isSendingRoute={isSendingRoute}
                onSelectOrigin={selectOrigin}
                onSelectDestination={(stationId, circuitId) => { void selectDestination(stationId, circuitId); }}
              />
            </DiagramViewport>
          ) : null}
        </div>
      </section>
      <RouteControlInspector
        active={active}
        routes={stationDiagram?.routeDefinitions ?? []}
        circuitsById={snapshot?.circuitsById}
        station={stationStatus}
        onCancelRoute={(routeId) => { void cancelRoute(routeId); }}
        isSendingRoute={isSendingRoute}
      />
    </div>
  );
}
