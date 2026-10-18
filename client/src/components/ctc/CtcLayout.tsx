"use client";

import { DiagramViewport } from "@/components/ctc/DiagramViewport";
import { SimulationClock } from "@/components/ctc/SimulationClock";
import { StationRouteIndicators } from "@/components/ctc/StationRouteIndicators";
import { TrackDiagram } from "@/components/ctc/TrackDiagram";
import { TurnoutDirectionIndicator } from "@/components/ctc/TurnoutDirectionIndicator";
import { Card } from "@/components/ui/card";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { DiagramScene } from "@/lib/diagramTypes";
import { useCtcSnapshot } from "@/lib/useCtcSnapshot";

const tabClassName =
  "h-8 min-w-0 flex-1 rounded-none border-0 border-t-[5px] border-transparent bg-zinc-400 px-2 py-0 text-xs font-normal text-zinc-800 shadow-none transition-none hover:text-zinc-800 data-active:border-orange-300 data-active:bg-zinc-950 data-active:text-zinc-200 data-active:hover:text-zinc-200 group-data-[variant=default]/tabs-list:data-active:shadow-none sm:text-base";

const TURNOUT_INDICATOR_SCALE = 0.16;
type Props = {
  scene: DiagramScene;
};

export default function CtcLayout({ scene }: Props) {
  const snapshot = useCtcSnapshot();

  return (
    <main className="flex h-dvh min-h-0 w-full flex-col overflow-hidden bg-zinc-950 font-sans">
      <h1 className="sr-only">中鉄CTC</h1>

      <Tabs
        defaultValue="traffic"
        className="flex min-h-0 flex-1 flex-col gap-0"
      >
        <header className="flex h-8 shrink-0 items-center justify-between gap-3 bg-zinc-200">
          <TabsList
            aria-label="画面切替"
            className="h-8 w-88 max-w-[62%] shrink-0 justify-start gap-0 rounded-none bg-transparent p-0"
          >
            <TabsTrigger value="traffic" className={tabClassName}>
              列車在線情報
            </TabsTrigger>
            <TabsTrigger value="timetable" className={tabClassName}>
              ダイヤ管理
            </TabsTrigger>
          </TabsList>

          <SimulationClock worldTime={snapshot?.worldTime} />
        </header>

        <TabsContent
          value="traffic"
          keepMounted
          className="relative m-0 min-h-0 flex-1 overflow-hidden bg-zinc-950"
        >
          <section
            aria-label="配線図キャンバス"
            className="absolute inset-y-0 left-0 right-[min(19rem,40vw)]"
          >
            <DiagramViewport width={scene.width} height={scene.height}>
              {scene.diagrams.map((diagram) => {
                const station = diagram.stationId
                  ? snapshot?.interlocking?.stationsById[diagram.stationId]
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
                      label={`${diagram.id}の配線図`}
                    />
                    {diagram.layout.turnoutIndicators.length > 0 ? (
                      <g aria-label={`${diagram.id}の転轍機方向表示灯`}>
                        {/* 表示灯の共通座標枠（200×248）の中心を目印に合わせる。 */}
                        {diagram.layout.turnoutIndicators.map(({ id, turnoutId, label, x, y }) => (
                          <g
                            key={id}
                            data-turnout-indicator-id={id}
                            data-turnout-id={turnoutId}
                            transform={`translate(${x} ${y}) scale(${TURNOUT_INDICATOR_SCALE}) translate(-100 -124)`}
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
                        />
                      </g>
                    ) : null}
                  </g>
                );
              })}
            </DiagramViewport>
          </section>

          <aside
            aria-labelledby="selection-panel-title"
            className="absolute inset-y-0 right-0 w-[min(19rem,40vw)]"
          >
            <Card className="h-full gap-0 overflow-hidden rounded-none border-0 bg-zinc-800 py-0 text-zinc-200 shadow-none ring-0">
              <h2 id="selection-panel-title" className="sr-only">
                選択した列車・進路・転轍機の詳細と操作
              </h2>
              <ScrollArea className="h-full w-full" />
            </Card>
          </aside>
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
        className="flex h-6 shrink-0 items-center bg-zinc-300 px-2 text-xs text-zinc-800 sm:text-sm"
      >
        待機中
      </footer>
    </main>
  );
}
