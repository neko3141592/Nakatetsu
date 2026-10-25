import type { Metadata } from "next";
import { Suspense } from "react";

import { TimetableEditor } from "@/components/timetable/TimetableEditor";
import stationLayout from "@/data/timetable-diagrams/NtLine.json";
import type { Station } from "@/types/masters";
import type { TimetableOperationSummary } from "@/types/operation";
import type { TimetableSummary } from "@/types/timetable";
import type { TimetableDiagramStation } from "@/types/timetableDiagram";

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
  const [stationResponse, timetableResponse, operationResponse] = await Promise.all([
    fetch("http://127.0.0.1:8080/api/stations", { cache: "no-store" }),
    fetch("http://127.0.0.1:8080/api/timetable", { cache: "no-store" }),
    fetch("http://127.0.0.1:8080/api/operations", { cache: "no-store" }),
  ]);
  if (!stationResponse.ok) throw new Error("駅一覧の取得に失敗しました");
  if (!timetableResponse.ok) throw new Error("ダイヤ一覧の取得に失敗しました");
  if (!operationResponse.ok) throw new Error("運用一覧の取得に失敗しました");

  const [stations, timetables, operations]: [Station[], TimetableSummary[], TimetableOperationSummary[]] = await Promise.all([
    stationResponse.json(),
    timetableResponse.json(),
    operationResponse.json(),
  ]);
  const stationsById = new Map(stations.map((station) => [station.stationId, station]));
  const diagramStations: TimetableDiagramStation[] = stationLayout.stations.map((layout) => {
    const station = stationsById.get(layout.stationId);
    if (!station) throw new Error(`駅が見つかりません: ${layout.stationId}`);
    return { ...layout, displayName: station.displayName };
  });

  return <TimetableEditor stations={diagramStations} timetables={timetables} operations={operations} />;
}
