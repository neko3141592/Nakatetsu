"use client";

import { useEffect, useState } from "react";
import { loadTrainTypes } from "@/app/timetables/train-actions";
import { loadTrainType, saveTrainType } from "@/app/timetables/train-type-actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/toast";
import { RunningPatternList } from "@/components/timetable/RunningPatternList";
import { useDiscardChanges, useUnsavedChanges } from "@/components/timetable/UnsavedChanges";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import type { Station, TrainType, TrainTypeDetail } from "@/types/masters";
import type { TimetableDiagramStation } from "@/types/timetableDiagram";

function withStations(stations: readonly Station[], type?: TrainTypeDetail): TrainTypeDetail {
  return {
    trainTypeId: type?.trainTypeId ?? "", displayName: type?.displayName ?? "",
    color: type?.color ?? "#2563eb",
    stationHandlings: stations.map(({ stationId }) => ({ stationId, handling: type?.stationHandlings.find((item) => item.stationId === stationId)?.handling ?? "stop" })),
  };
}

export function TrainTypeInput({ stations, onSaved }: { stations: readonly TimetableDiagramStation[]; onSaved?: (type: TrainType) => void }) {
  const { confirmDiscard } = useDiscardChanges();
  const [types, setTypes] = useState<TrainType[]>([]);
  const [trainType, setTrainType] = useState(() => withStations(stations));
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [busy, setBusy] = useState(true);
  const { markSaved } = useUnsavedChanges("train-type", trainType, busy);
  const validColor = /^#[0-9a-f]{6}$/i.test(trainType.color);
  const items = [{ value: "", label: "新規種別" }, ...types.map((item) => ({ value: item.trainTypeId, label: item.displayName }))];

  useEffect(() => {
    let active = true;
    loadTrainTypes().then((result) => {
      if (!active) return;
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else setTypes(result.data);
      setBusy(false);
    }).catch(() => {
      if (!active) return;
      toast.add({ title: "列車種別の取得に失敗しました", type: "error" });
      setBusy(false);
    });
    return () => { active = false; };
  }, []);

  async function select(id: string | null) {
    if ((id || null) === selectedId || !await confirmDiscard(["train-type", "pattern"])) return;
    if (!id) {
      setSelectedId(null);
      const next = withStations(stations);
      setTrainType(next);
      markSaved(next);
      return;
    }
    setBusy(true);
    try {
      const result = await loadTrainType(id);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setSelectedId(id);
        const next = withStations(stations, result.data);
        setTrainType(next);
        markSaved(next);
      }
    } catch {
      toast.add({ title: "列車種別の取得に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  async function save() {
    if (busy || !trainType.displayName.trim() || !validColor) return;
    setBusy(true);
    try {
      const result = await saveTrainType(trainType, selectedId === null);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        const saved = result.data;
        setTypes((current) => [...current.filter((item) => item.trainTypeId !== saved.trainTypeId), saved].sort((a, b) => a.trainTypeId.localeCompare(b.trainTypeId)));
        setTrainType(saved);
        markSaved(saved);
        setSelectedId(saved.trainTypeId);
        onSaved?.(saved);
        toast.add({ title: "保存しました", type: "success" });
      }
    } catch {
      toast.add({ title: "列車種別の保存に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  return (
    <ResizablePanelGroup orientation="horizontal" id="train-type-workspace" className="min-h-0 bg-zinc-950">
    <ResizablePanel id="train-type-input" defaultSize="40%" minSize="20%" className="min-w-0">
    <section aria-label="列車種別の編集" className="flex h-full min-h-0 min-w-0 flex-col text-zinc-200">
      <header className="flex h-9 shrink-0 items-center gap-2 bg-black px-3">
        <Select items={items} value={selectedId ?? ""} disabled={busy} onValueChange={select}>
          <SelectTrigger aria-label="列車種別を選択" size="sm" className="min-w-0 border-0 bg-transparent px-0 text-zinc-200 shadow-none dark:bg-transparent">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>{items.map((item) => <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>)}</SelectContent>
        </Select>
        <Button type="button" size="xs" onClick={save} disabled={busy || !trainType.displayName.trim() || !validColor} className="ml-auto bg-green-600 text-white hover:bg-green-600">適用</Button>
      </header>
      <div className="min-h-0 flex-1 space-y-3 overflow-auto p-3">
        <label className="flex flex-col gap-1.5 text-xs">
          名称
          <Input value={trainType.displayName} disabled={busy} onChange={(event) => setTrainType((current) => ({ ...current, displayName: event.target.value }))} className="border-zinc-700" />
        </label>
        <div role="group" aria-label="色" className="space-y-1.5 text-xs">
          <span>色</span>
          <div className="flex items-center gap-2">
            <Input type="color" aria-label="スジの色を選択" value={validColor ? trainType.color : "#2563eb"} disabled={busy}
              onChange={(event) => setTrainType((current) => ({ ...current, color: event.target.value }))} className="h-8 w-12 shrink-0 cursor-pointer border-zinc-700 p-1" />
            <Input aria-label="スジの色コード" value={trainType.color} maxLength={7} aria-invalid={!validColor} disabled={busy}
              onChange={(event) => setTrainType((current) => ({ ...current, color: event.target.value }))} className="h-8 w-28 border-zinc-700 font-mono text-xs" />
          </div>
        </div>
        <div className="overflow-hidden rounded-xs border border-zinc-600">
          <table aria-label="種別の停車・通過設定" className="w-full border-collapse text-xs">
            <thead className="bg-zinc-900"><tr><th className="px-3 py-2 text-left font-normal">駅名</th><th className="px-3 py-2 text-left font-normal">停車／通過</th></tr></thead>
            <tbody>{trainType.stationHandlings.map((item) => <tr key={item.stationId} className="odd:bg-zinc-800 even:bg-zinc-700/50">
              <td className="px-3 py-1">{stations.find((station) => station.stationId === item.stationId)?.displayName}</td>
              <td className="p-1"><Button type="button" size="sm" disabled={busy} aria-label={`${stations.find((station) => station.stationId === item.stationId)?.displayName}の停車／通過`} aria-pressed={item.handling === "pass"}
                onClick={() => setTrainType((current) => ({ ...current, stationHandlings: current.stationHandlings.map((row) => row.stationId === item.stationId ? { ...row, handling: row.handling === "stop" ? "pass" : "stop" } : row) }))}
                className={`h-7 w-full border border-zinc-500 text-xs ${item.handling === "pass" ? "bg-yellow-300 text-black hover:bg-yellow-300" : "bg-zinc-900 text-zinc-100 hover:bg-zinc-900"}`}>{item.handling === "pass" ? "通過" : "停車"}</Button></td>
            </tr>)}</tbody>
          </table>
        </div>
      </div>
    </section>
    </ResizablePanel>
    <ResizableHandle withHandle aria-label="種別編集と走行パターンの幅を変更" className="w-1 bg-zinc-700" />
    <ResizablePanel id="train-type-patterns" defaultSize="60%" minSize="30%" className="min-w-0">
      <RunningPatternList key={selectedId ?? "new"} trainType={trainType} stations={stations} />
    </ResizablePanel>
    </ResizablePanelGroup>
  );
}
