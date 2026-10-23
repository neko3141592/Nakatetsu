"use client";

import { useState } from "react";

import { RouteControlTab } from "@/components/ctc/tabs/route-control/RouteControlTab";
import { TrafficTab } from "@/components/ctc/tabs/traffic/TrafficTab";
import { TimetableTab } from "@/components/ctc/tabs/timetable/TimetableTab";
import { SimulationClock } from "@/components/ctc/header/SimulationClock";
import { SimulationPauseOverlay } from "@/components/ctc/overlays/SimulationPauseOverlay";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { DiagramScene } from "@/lib/diagramTypes";
import { useCtcSnapshot } from "@/lib/useCtcSnapshot";

const tabClassName =
  "h-8 min-w-0 flex-1 rounded-none border-0 border-t-[5px] border-transparent bg-zinc-900 px-2 py-0 text-xs font-normal text-zinc-500 shadow-none transition-none data-active:border-orange-300 data-active:bg-zinc-950 data-active:text-zinc-200 group-data-[variant=default]/tabs-list:data-active:shadow-none sm:text-base";

type Props = {
  scene: DiagramScene;
};

export default function CtcLayout({ scene }: Props) {
  const snapshot = useCtcSnapshot();
  const [activeTab, setActiveTab] = useState("traffic");
  const [message, setMessage] = useState("待機中");

  return (
    <main
      className="flex h-dvh min-h-0 w-full flex-col overflow-hidden bg-zinc-900 font-sans"
    >
      <h1 className="sr-only">中鉄CTC</h1>
      {snapshot?.worldTime?.isPaused ? <SimulationPauseOverlay /> : null}

      <Tabs
        value={activeTab}
        onValueChange={setActiveTab}
        className="flex min-h-0 flex-1 flex-col gap-0"
      >
        <header className="flex h-8 shrink-0 items-center justify-between gap-3 bg-zinc-900 text-zinc-800">
          <TabsList
            aria-label="画面切替"
            className="h-8 w-132 min-w-0 max-w-[62%] justify-start gap-0 rounded-none bg-transparent p-0"
          >
            <TabsTrigger value="traffic" className={tabClassName}>
              列車在線情報
            </TabsTrigger>
            <TabsTrigger value="routes" className={tabClassName}>
              進路制御
            </TabsTrigger>
            <TabsTrigger value="timetable" className={tabClassName}>
              ダイヤ管理
            </TabsTrigger>
          </TabsList>

          <div className="flex h-full shrink-0 items-center pr-1">
            <SimulationClock worldTime={snapshot?.worldTime} />
          </div>
        </header>

        <div className="relative flex min-h-0 flex-1">
          <TabsContent
            value="traffic"
            keepMounted
            className="relative m-0 min-h-0 flex-1 overflow-hidden bg-zinc-950"
          >
            <TrafficTab
              scene={scene}
              snapshot={snapshot}
              active={activeTab === "traffic"}
            />
          </TabsContent>

          <TabsContent
            value="routes"
            keepMounted
            className="relative m-0 min-h-0 flex-1 overflow-hidden bg-zinc-950"
          >
            <RouteControlTab
              scene={scene}
              snapshot={snapshot}
              active={activeTab === "routes"}
              onMessage={setMessage}
            />
          </TabsContent>
          <TabsContent
            value="timetable"
            className="relative m-0 min-h-0 flex-1 overflow-hidden bg-zinc-950"
          >
            <TimetableTab />
          </TabsContent>

        </div>
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
