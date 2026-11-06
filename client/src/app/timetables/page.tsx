import type { Metadata } from "next";
import { Suspense } from "react";

import { TimetableEditor } from "@/components/timetable/TimetableEditor";
import stationLayout from "@/data/timetable-diagrams/NtLine.json";
import type { Station } from "@/types/masters";
import type { TimetableSummary } from "@/types/timetable";
import type { TimetableDiagramDefinition, TimetableDiagramStation } from "@/types/timetableDiagram";

export const metadata: Metadata = {
  title: "ダイヤ編集 | Nakatetsu CTC",
};

export default function TimetablesPage() {
  return (
    <Suspense fallback={<div className="h-dvh bg-zinc-950 p-4 text-zinc-400">一覧を読み込んでいます…</div>}>
      <TimetableWorkspace />
    </Suspense>
  );
}

async function TimetableWorkspace() {
  const [stationResponse, timetableResponse] = await Promise.all([
    fetch("http://127.0.0.1:8080/api/stations", { cache: "no-store" }),
    fetch("http://127.0.0.1:8080/api/timetable", { cache: "no-store" }),
  ]);
  if (!stationResponse.ok) throw new Error("駅一覧の取得に失敗しました");
  if (!timetableResponse.ok) throw new Error("ダイヤ一覧の取得に失敗しました");

  const [stations, timetables]: [Station[], TimetableSummary[]] = await Promise.all([
    stationResponse.json(),
    timetableResponse.json(),
  ]);
  const stationsById = new Map(stations.map((station) => [station.stationId, station]));
  const diagramDefinition: TimetableDiagramDefinition = stationLayout;
  const diagramStations: TimetableDiagramStation[] = diagramDefinition.stations.map((layout) => {
    const displayName = stationsById.get(layout.stationId)?.displayName ?? layout.displayName;
    if (!displayName) throw new Error(`駅が見つかりません: ${layout.stationId}`);
    return { ...layout, displayName };
  });

  return <TimetableEditor stations={diagramStations} timetables={timetables} />;
}
