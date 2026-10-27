"use client";

import { useEffect, useState } from "react";
import { loadStations } from "@/app/timetables/train-actions";
import { loadRunningPattern, saveRunningPattern } from "@/app/timetables/running-pattern-actions";
import { TrainStopTable } from "@/components/timetable/TrainStopTable";
import { useDiscardChanges, useUnsavedChanges } from "@/components/timetable/UnsavedChanges";
import { FloatingWindow } from "@/components/ui/floating-window";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/toast";
import stationLayout from "@/data/timetable-diagrams/NtLine.json";
import { buildServiceStops, getStopDraftValues, type TrainStopDraft } from "@/lib/trainTimetable";
import { buildRunningPatternPoints, runningPatternToStops } from "@/lib/runningPattern";
import type { StationDetail, TrainTypeDetail } from "@/types/masters";
import type { RunningPattern } from "@/types/runningPattern";

type Props = { patternId?: string; trainType: TrainTypeDetail; onSaved: (pattern: RunningPattern) => void; onClose: () => void };

function patternDraft(displayName: string, endpoints: { origin: string; destination: string }, stops: TrainStopDraft[]) {
  return { displayName, endpoints, stops: getStopDraftValues(stops) };
}

export function RunningPatternWindow({ patternId, trainType, onSaved, onClose }: Props) {
  const { confirmDiscard } = useDiscardChanges();
  const [savedId, setSavedId] = useState(patternId ?? "");
  const [displayName, setDisplayName] = useState("");
  const [endpoints, setEndpoints] = useState({ origin: "", destination: "" });
  const [stations, setStations] = useState<StationDetail[]>([]);
  const [stops, setStops] = useState<TrainStopDraft[]>([]);
  const [defaults] = useState(trainType.stationHandlings);
  const [busy, setBusy] = useState(true);
  const [ready, setReady] = useState(false);
  const { markSaved } = useUnsavedChanges("pattern", patternDraft(displayName, endpoints, stops), busy);
  const trainTypeId = trainType.trainTypeId;

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const [catalog, detail] = await Promise.all([loadStations(), patternId ? loadRunningPattern(patternId) : null]);
        if (!active) return;
        if (catalog.error !== undefined) { toast.add({ title: catalog.error, type: "error" }); return; }
        if (detail?.error !== undefined) { toast.add({ title: detail.error, type: "error" }); return; }
        const byId = new Map(catalog.data.map((station) => [station.stationId, station]));
        const ordered = stationLayout.stations.flatMap(({ stationId }) => byId.has(stationId) ? [byId.get(stationId)!] : []);
        setStations(ordered);
        if (detail) {
          if (detail.data.trainTypeId !== trainTypeId) throw new Error("種別が一致しません");
          const loaded = runningPatternToStops(detail.data.points);
          const origin = loaded[0]?.stationId ?? "";
          const destination = loaded.at(-1)?.stationId ?? "";
          setDisplayName(detail.data.displayName);
          setEndpoints({ origin, destination });
          const nextStops = buildServiceStops(ordered, origin, destination, loaded, defaults);
          setStops(nextStops);
          markSaved(patternDraft(detail.data.displayName, { origin, destination }, nextStops));
        }
        setReady(true);
      } catch { if (active) toast.add({ title: "走行パターンの取得に失敗しました", type: "error" }); }
      finally { if (active) setBusy(false); }
    }
    void load();
    return () => { active = false; };
  }, [patternId, trainTypeId, defaults, markSaved]);

  function changeEndpoint(field: "origin" | "destination", value: string | null) {
    const next = { ...endpoints, [field]: value ?? "" };
    setEndpoints(next);
    setStops((current) => buildServiceStops(stations, next.origin, next.destination, current, defaults));
  }

  async function save() {
    if (busy || !ready || !displayName.trim() || stops.length === 0) return;
    const points = buildRunningPatternPoints(stops);
    if (points.some((point) => !stations.find((station) => station.stationId === point.stationId)?.tracks.some((track) => track.trackCircuitId === point.trackCircuitId))) {
      toast.add({ title: "各駅の番線を選択してください", type: "error" }); return;
    }
    if (points.some((point) => Number.isNaN(point.arrivalOffsetSeconds) || Number.isNaN(point.departureOffsetSeconds))) {
      toast.add({ title: "相対時刻はHH:mmまたはHH:mm:ssで入力してください", type: "error" }); return;
    }
    setBusy(true);
    try {
      const result = await saveRunningPattern({ id: savedId, trainTypeId, displayName, points });
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setSavedId(result.data.id);
        setDisplayName(result.data.displayName);
        markSaved(patternDraft(result.data.displayName, endpoints, stops));
        onSaved(result.data);
        toast.add({ title: "保存しました", type: "success" });
      }
    } catch { toast.add({ title: "走行パターンの保存に失敗しました", type: "error" }); }
    finally { setBusy(false); }
  }

  const items = stations.map((station) => ({ value: station.stationId, label: station.displayName }));
  return <FloatingWindow title={`${savedId ? "走行パターン編集" : "新規走行パターン"} — ${trainType.displayName}`} onClose={async () => { if (await confirmDiscard(["pattern"])) onClose(); }} closeLabel="走行パターン編集を閉じる">
    <div className="flex min-h-full flex-col gap-4">
      <label className="grid grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs"><span>名称</span><Input autoFocus value={displayName} onChange={(event) => setDisplayName(event.target.value)} disabled={busy} className="h-7 border-zinc-500 bg-zinc-900 text-sm dark:bg-zinc-900" /></label>
      {([['origin', '始発駅'], ['destination', '終着駅']] as const).map(([field, label]) => <label key={field} className="grid grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs"><span>{label}</span>
        <Select items={items} value={endpoints[field] || null} onValueChange={(value) => changeEndpoint(field, value)} disabled={busy || !ready}>
          <SelectTrigger aria-label={label} size="sm" className="w-full border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
          <SelectContent>{items.map((item) => <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>)}</SelectContent>
        </Select>
      </label>)}
      <TrainStopTable section="service" title="相対時刻" rows={stops} stations={stations} disabled={busy || !ready} zeroDeparture onChange={setStops} />
      <div className="mt-auto flex items-end justify-between gap-3 pt-4">
        <span className="text-xs text-zinc-400">{busy ? "読み込み中…" : ""}</span>
        <Button type="button" size="sm" onClick={save} disabled={busy || !ready || !displayName.trim() || stops.length === 0} className="min-w-24 bg-green-600 text-white hover:bg-green-600">適用</Button>
      </div>
    </div>
  </FloatingWindow>;
}
