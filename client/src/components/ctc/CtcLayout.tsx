"use client";

import { useMemo, useRef, useState, type CSSProperties } from "react";

import { DiagramViewport } from "@/components/ctc/DiagramViewport";
import { DiagramTrainLabels } from "@/components/ctc/DiagramTrainLabels";
import { SimulationClock } from "@/components/ctc/SimulationClock";
import { SimulationPauseOverlay } from "@/components/ctc/SimulationPauseOverlay";
import { StationRouteIndicators } from "@/components/ctc/StationRouteIndicators";
import { TrackDiagram, type RouteDirection } from "@/components/ctc/TrackDiagram";
import { TurnoutDirectionIndicator } from "@/components/ctc/TurnoutDirectionIndicator";
import { TrainInspector } from "@/components/ctc/inspector/TrainInspector";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Sidebar, SidebarProvider, SidebarTrigger, useSidebar } from "@/components/ui/sidebar";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { DiagramScene } from "@/lib/diagramTypes";
import type { InspectorSelection } from "@/lib/inspectorTypes";
import { useCtcSnapshot } from "@/lib/useCtcSnapshot";
import { sendRouteRequest } from "@/lib/sendRouteRequest";

const tabClassName =
  "h-8 min-w-0 flex-1 rounded-none border-0 border-t-[5px] border-transparent bg-zinc-900 px-2 py-0 text-xs font-normal text-zinc-500 shadow-none transition-none hover:text-zinc-800 data-active:border-orange-300 data-active:bg-zinc-950 data-active:text-zinc-200 data-active:hover:text-zinc-200 group-data-[variant=default]/tabs-list:data-active:shadow-none sm:text-base";

type Props = {
  scene: DiagramScene;
};

const NO_DESTINATIONS: readonly string[] = [];

const sidebarTriggerClassName =
  "size-8 rounded-none bg-transparent text-inherit shadow-none transition-none hover:bg-transparent! hover:text-inherit! aria-expanded:bg-transparent! aria-expanded:text-inherit! [&_svg]:rotate-180";

export default function CtcLayout(props: Props) {
  return (
    <SidebarProvider
      className="h-dvh min-h-0! overflow-hidden"
      style={{ "--sidebar-width": "min(19rem, 40vw)" } as CSSProperties}
    >
      <CtcLayoutContent {...props} />
    </SidebarProvider>
  );
}

function CtcLayoutContent({ scene }: Props) {
  const snapshot = useCtcSnapshot();
  const [selection, setSelection] = useState<InspectorSelection>(null);
  const [origin, setOrigin] = useState<{ stationId: string; circuitId: string; direction?: RouteDirection } | null>(null);
  const [isSendingRoute, setIsSendingRoute] = useState(false);
  const requestPending = useRef(false);
  const [message, setMessage] = useState("待機中");
  const availableRoutes = useMemo(() => origin
    ? scene.diagrams.find((diagram) => diagram.stationId === origin.stationId)?.routeDefinitions
      .filter((route) => route.startTrackCircuitId === origin.circuitId &&
        (!origin.direction || route.routeId.match(/^\d+([RL])/)?.[1] === origin.direction)) ?? []
    : [], [origin, scene]);
  const availableDestinations = useMemo(
    () => Array.from(new Set(availableRoutes.map((route) => route.destinationTrackCircuitId))),
    [availableRoutes],
  );
  const { open, openMobile, isMobile, setOpen, setOpenMobile } = useSidebar();
  const panelOpen = isMobile ? openMobile : open;
  const panelToggleLabel = panelOpen ? "右側パネルを閉じる" : "右側パネルを開く";

  function handleSelect(selection: NonNullable<InspectorSelection>) {
    setSelection(selection);
    if (isMobile) setOpenMobile(true);
    else setOpen(true);
  }

  function selectOrigin(stationId: string, circuitId: string, direction?: RouteDirection) {
    if (requestPending.current) return;
    if (origin?.stationId === stationId && origin.circuitId === circuitId && origin.direction === direction) {
      setOrigin(null);
      setMessage("発点の選択を解除しました。");
      return;
    }
    setOrigin({ stationId, circuitId, direction });
    setMessage(`発点が選択されました`);
  }

  async function selectDestination(stationId: string, circuitId: string) {
    if (requestPending.current || origin?.stationId !== stationId) return;
    const routes = availableRoutes.filter((route) => route.destinationTrackCircuitId === circuitId);
    if (routes.length !== 1) {
      setMessage("進路を1つに特定できません。進路定義を確認してください。");
      return;
    }
    const route = routes[0];
    requestPending.current = true;
    setIsSendingRoute(true);
    setMessage(`${stationId} 進路 ${route.routeId} を要求しています…`);
    try {
      const result = await sendRouteRequest(stationId, route.routeId);
      if (result.accepted) {
        setOrigin(null);
        setMessage(`${stationId} 進路 ${route.routeId}：受付済み。${result.message ?? ""}`);
      } else {
        setMessage(`${stationId} 進路 ${route.routeId}：拒否。${result.message || "要求は受け付けられませんでした。"}`);
      }
    } catch (error) {
      setMessage(`${stationId} 進路 ${route.routeId}：送信結果を確認できません。${error instanceof Error ? error.message : "通信エラー"}`);
    } finally {
      requestPending.current = false;
      setIsSendingRoute(false);
    }
  }

  return (
    <main
      className="flex h-dvh min-h-0 w-full flex-col overflow-hidden bg-zinc-900 font-sans"
      onKeyDown={(event) => {
        if (event.key === "Escape" && origin && !requestPending.current) {
          setOrigin(null);
          setMessage("発点の選択を解除しました。");
        }
      }}
    >
      <h1 className="sr-only">中鉄CTC</h1>
      {snapshot?.worldTime?.isPaused ? <SimulationPauseOverlay /> : null}

      <Tabs
        defaultValue="traffic"
        className="flex min-h-0 flex-1 flex-col gap-0"
      >
        <header className="flex h-8 shrink-0 items-center justify-between gap-3 bg-zinc-900 text-zinc-800">
          <TabsList
            aria-label="画面切替"
            className="h-8 w-88 min-w-0 max-w-[62%] justify-start gap-0 rounded-none bg-transparent p-0"
          >
            <TabsTrigger value="traffic" className={tabClassName}>
              列車在線情報
            </TabsTrigger>
            <TabsTrigger value="timetable" className={tabClassName}>
              ダイヤ管理
            </TabsTrigger>
          </TabsList>

          <div className="flex h-full shrink-0 items-center pr-1">
            <SimulationClock worldTime={snapshot?.worldTime} />
            <SidebarTrigger
              aria-label={panelToggleLabel}
              title={panelToggleLabel}
              aria-controls="selection-panel-content"
              aria-expanded={panelOpen}
              className={sidebarTriggerClassName}
            />
          </div>
        </header>

        <TabsContent
          value="traffic"
          keepMounted
          className="relative m-0 min-h-0 flex-1 overflow-hidden bg-zinc-950"
        >
          <div className="absolute inset-0 flex min-h-0">
            <section
              aria-label="配線図キャンバス"
              className="relative min-h-0 min-w-0 flex-1"
            >
              <DiagramViewport width={scene.width} height={scene.height}>
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
                        svg={diagram.svg}
                        circuitSvgMap={diagram.circuitSvgMap}
                        circuitsById={snapshot?.circuitsById}
                        turnoutsById={snapshot?.turnoutsById}
                        station={station}
                        routeDefinitions={diagram.routeDefinitions}
                        selectedOriginCircuitId={origin?.stationId === stationId ? origin?.circuitId : undefined}
                        selectedOriginDirection={origin?.stationId === stationId ? origin?.direction : undefined}
                        availableDestinationCircuitIds={origin?.stationId === stationId ? availableDestinations : NO_DESTINATIONS}
                        isSendingRoute={isSendingRoute}
                        onSelectOrigin={stationId ? (circuitId, direction) => selectOrigin(stationId, circuitId, direction) : undefined}
                        onSelectDestination={stationId ? (circuitId) => { void selectDestination(stationId, circuitId); } : undefined}
                        label={`${diagram.id}の配線図`}
                      />
                      {diagram.layout.turnoutIndicators.length > 0 ? (
                        <g aria-label={`${diagram.id}の転轍機位置`}>
                          {diagram.layout.turnoutIndicators.map(({ id, turnoutId, label, x, y }) => (
                            <g
                              key={id}
                              data-turnout-indicator-id={id}
                              data-turnout-id={turnoutId}
                              transform={`translate(${x} ${y})`}
                            >
                              <TurnoutDirectionIndicator
                                label={label}
                                turnout={snapshot?.turnoutsById[turnoutId]}
                              />
                            </g>
                          ))}
                        </g>
                      ) : null}
                      {diagram.layout.routeIndicator ? (
                        <g
                          aria-label={`${diagram.id}の進路予約表示灯`}
                          data-route-indicators-id={diagram.layout.routeIndicator.id}
                          transform={`translate(${diagram.layout.routeIndicator.x} ${diagram.layout.routeIndicator.y})`}
                        >
                          <StationRouteIndicators
                            station={station}
                            routeIds={diagram.layout.routeIds}
                            spacing={20}
                            rowsPerColumn={4}
                          />
                        </g>
                      ) : null}
                      <DiagramTrainLabels
                        markers={diagram.layout.trainLabels}
                        circuitSvgMap={diagram.circuitSvgMap}
                        circuitsById={snapshot?.circuitsById}
                        trainsById={snapshot?.trainsById}
                        turnoutsById={snapshot?.turnoutsById}
                        selectedTrainId={selection?.trainId}
                        onSelect={handleSelect}
                      />
                    </g>
                  );
                })}
              </DiagramViewport>
            </section>

            <Sidebar
              side="right"
              collapsible="offcanvas"
              className="absolute! inset-y-0! h-full! border-0!"
            >
              <aside
                id="selection-panel-content"
                aria-labelledby="selection-panel-title"
                aria-hidden={!panelOpen}
                inert={!panelOpen}
                className="flex h-full min-h-0 w-full flex-col overflow-hidden bg-zinc-900 text-zinc-200"
              >
                <h2 id="selection-panel-title" className="sr-only">
                  選択した列車・進路・転轍機の詳細と操作
                </h2>
                <SidebarTrigger
                  aria-label={panelToggleLabel}
                  title={panelToggleLabel}
                  aria-controls="selection-panel-content"
                  aria-expanded={panelOpen}
                  className={`${sidebarTriggerClassName} ml-auto md:hidden`}
                />
                <ScrollArea className="min-h-0 w-full flex-1">
                  {selection ? (
                    <TrainInspector
                      trainId={selection.trainId}
                      train={snapshot?.trainsById[selection.trainId]}
                    />
                  ) : (
                    <p className="p-4 text-sm text-zinc-400">列番を選択してください。</p>
                  )}
                </ScrollArea>
              </aside>
            </Sidebar>
          </div>
        </TabsContent>

        <TabsContent
          value="timetable"
          className="relative m-0 min-h-0 flex-1 overflow-hidden bg-zinc-950"
        >
          <section
            aria-label="ダイヤ管理ワークスペース"
            className="absolute inset-0"
          />
        </TabsContent>
      </Tabs>

      <footer
        role="status"
        className="flex h-6 shrink-0 items-center bg-gray-950 px-2 text-xs text-zinc-200 sm:text-sm"
      >
        {message}
      </footer>
    </main>
  );
}
