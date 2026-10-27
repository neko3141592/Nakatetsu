"use client";

import Link from "next/link";
import { useState } from "react";
import { toast } from "@/components/ui/toast";

import { RouteControlTab } from "@/components/ctc/tabs/route-control/RouteControlTab";
import { TrafficTab } from "@/components/ctc/tabs/traffic/TrafficTab";
import { OperatingPlanTab } from "@/components/ctc/tabs/operating-plan/OperatingPlanTab";
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
            <TabsTrigger value="operating-plan" className={tabClassName}>
              運転計画変更
            </TabsTrigger>
          </TabsList>

          <div className="flex h-full shrink-0 items-center gap-3 pr-1">
            <Link
              href="/timetables"
              className="inline-flex h-6 shrink-0 items-center rounded-xs bg-zinc-800 px-2 text-xs text-zinc-200 hover:bg-zinc-700 focus-visible:outline-2 focus-visible:outline-orange-300"
            >
              ダイヤ編集
            </Link>
            <button
              type="button"
              disabled
              title="準備中"
              className="inline-flex h-6 shrink-0 cursor-not-allowed items-center rounded-xs bg-zinc-800 px-2 text-xs text-zinc-500"
            >
              運転記録
            </button>
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
              onMessage={(title, type = "info") => toast.add({ title, type, id: "route-control", timeout: type === "loading" ? 0 : 5000 })}
            />
          </TabsContent>
          <TabsContent
            value="operating-plan"
            className="relative m-0 min-h-0 flex-1 overflow-hidden bg-zinc-950"
          >
            <OperatingPlanTab />
          </TabsContent>

        </div>
      </Tabs>

      <footer
        className="flex h-6 shrink-0 items-center bg-zinc-950 px-2 text-xs text-zinc-200 sm:text-sm"
      >
        <span role="status">待機中</span>
      </footer>
    </main>
  );
}
