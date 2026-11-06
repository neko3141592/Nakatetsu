"use client";

import { useEffect, useId, useState } from "react";
import { loadTrain, loadTrainTypes, loadStations, saveTrain } from "@/app/timetables/train-actions";
import { loadTrainType } from "@/app/timetables/train-type-actions";
import { loadRunningPattern, loadRunningPatterns } from "@/app/timetables/running-pattern-actions";
import { TrainStopTable } from "@/components/timetable/TrainStopTable";
import { useDiscardChanges, useUnsavedChanges } from "@/components/timetable/UnsavedChanges";
import { FloatingWindow } from "@/components/ui/floating-window";
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import stationLayout from "@/data/timetable-diagrams/NtLine.json";
import { applyTrainTypeHandlings, buildServiceStops, buildTrainPoints, createTrainStop, formatTrainTime, getStopDraftValues, parseTrainTime, type TrainStopDraft } from "@/lib/trainTimetable";
import { runningPatternToStops } from "@/lib/runningPattern";
import type { StationDetail, TrainType, TrainTypeStationHandling } from "@/types/masters";
import type { TimetableTrainBasics } from "@/types/train";
import type { RunningPatternSummary } from "@/types/runningPattern";

function trainDraft(train: TimetableTrainBasics, stops: TrainStopDraft[], depotExit: TrainStopDraft[], depotEntry: TrainStopDraft[]) {
  return { train, stops: getStopDraftValues(stops), depotExit: getStopDraftValues(depotExit), depotEntry: getStopDraftValues(depotEntry) };
}

export function TrainEditWindow({ trainId, timetableId, onClose, onSaved }: { trainId?: string; timetableId: string; onClose: () => void; onSaved?: (trainId: string) => void }) {
  const { confirmDiscard } = useDiscardChanges();
  const departureTimeId = useId();
  const [savedNumber, setSavedNumber] = useState(trainId ?? null);
  const [train, setTrain] = useState<TimetableTrainBasics>({ trainNumber: "", trainTypeId: "", originStationId: "", destinationStationId: "" });
  const [types, setTypes] = useState<TrainType[]>([]);
  const [stationHandlings, setStationHandlings] = useState<TrainTypeStationHandling[]>([]);
  const [patterns, setPatterns] = useState<RunningPatternSummary[]>([]);
  const [patternId, setPatternId] = useState<string | null>(null);
  const [patternDepartureTime, setPatternDepartureTime] = useState("");
  const [stations, setStations] = useState<StationDetail[]>([]);
  const [stops, setStops] = useState<TrainStopDraft[]>([]);
  const [depotExit, setDepotExit] = useState<TrainStopDraft[]>([]);
  const [depotEntry, setDepotEntry] = useState<TrainStopDraft[]>([]);
  const [busy, setBusy] = useState(true);
  const [ready, setReady] = useState(false);
  const { markSaved } = useUnsavedChanges("train", trainDraft(train, stops, depotExit, depotEntry), busy);
  const patternStartSeconds = parseTrainTime(patternDepartureTime);
  const valid = ready && !!train.trainNumber.trim() && types.some((type) => type.trainTypeId === train.trainTypeId)
    && stations.some((station) => station.stationId === train.originStationId) && stations.some((station) => station.stationId === train.destinationStationId) && stops.length > 0;

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const [detail, catalog, stationList] = await Promise.all([trainId ? loadTrain(trainId) : null, loadTrainTypes(), loadStations()]);
        if (!active) return;
        for (const result of [detail, catalog, stationList]) {
          if (result?.error !== undefined) toast.add({ title: result.error, type: "error" });
        }
        if (detail?.error !== undefined || catalog.error !== undefined || stationList.error !== undefined) return;
        const typeId = detail?.data.trainTypeId;
        const [type, patternList] = await Promise.all([typeId ? loadTrainType(typeId) : null, typeId ? loadRunningPatterns(typeId) : null]);
        if (!active) return;
        if (type?.error !== undefined) {
          toast.add({ title: type.error, type: "error" });
          return;
        }
        setStationHandlings(type?.data.stationHandlings ?? []);
        if (patternList?.error !== undefined) toast.add({ title: patternList.error, type: "error" });
        else setPatterns(patternList?.data ?? []);
        const byId = new Map(stationList.data.map((station) => [station.stationId, station]));
        const orderedStations = stationLayout.stations.flatMap(({ stationId }) => byId.has(stationId) ? [byId.get(stationId)!] : []);
        if (detail) {
          const nextTrain = { trainNumber: detail.data.trainNumber, trainTypeId: detail.data.trainTypeId, originStationId: detail.data.originStationId, destinationStationId: detail.data.destinationStationId };
          setTrain(nextTrain);
          const loaded = (detail.data.points ?? []).toSorted((a, b) => a.sequenceIndex - b.sequenceIndex).map((point) => createTrainStop(point.stationId, point));
          const exit = loaded.filter((stop) => stop.section === "depot_exit");
          const entry = loaded.filter((stop) => stop.section === "depot_entry");
          setDepotExit(exit);
          setDepotEntry(entry);
          const service = loaded.filter((stop) => stop.section === "service");
          const nextStops = buildServiceStops(orderedStations, detail.data.originStationId, detail.data.destinationStationId, service);
          setStops(nextStops);
          markSaved(trainDraft(nextTrain, nextStops, exit, entry));
          setPatternDepartureTime(service[0]?.departureTime ?? "");
        }
        setTypes(catalog.data);
        setStations(orderedStations);
        setReady(true);
      } catch {
        if (active) toast.add({ title: "列車・マスターの取得に失敗しました", type: "error" });
      } finally {
        if (active) setBusy(false);
      }
    }
    void load();
    return () => { active = false; };
  }, [trainId, markSaved]);

  function changeEndpoints(patch: Partial<Pick<TimetableTrainBasics, "originStationId" | "destinationStationId">>) {
    const next = { ...train, ...patch };
    setTrain(next);
    setStops((current) => buildServiceStops(stations, next.originStationId, next.destinationStationId, current, stationHandlings));
  }

  async function selectType(id: string | null) {
    if (!id) return;
    setBusy(true);
    try {
      const [result, patternList] = await Promise.all([loadTrainType(id), loadRunningPatterns(id)]);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setTrain((current) => ({ ...current, trainTypeId: id }));
        setStationHandlings(result.data.stationHandlings);
        setPatternId(null);
        setPatterns(patternList.data ?? []);
        if (patternList.error !== undefined) toast.add({ title: patternList.error, type: "error" });
        if (savedNumber === null) setStops((current) => applyTrainTypeHandlings(current, result.data.stationHandlings));
      }
    } catch {
      toast.add({ title: "列車種別の取得に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  async function applyPattern() {
    if (busy || !patternId) return;
    if (patternStartSeconds === null || !Number.isFinite(patternStartSeconds)) {
      toast.add({ title: "始発時刻をHH:mmまたはHH:mm:ssで入力してください", type: "error" }); return;
    }
    setBusy(true);
    try {
      const result = await loadRunningPattern(patternId);
      if (result.error !== undefined) { toast.add({ title: result.error, type: "error" }); return; }
      if (result.data.trainTypeId !== train.trainTypeId) {
        toast.add({ title: "同じ種別の走行パターンを選択してください", type: "error" }); return;
      }
      const next = runningPatternToStops(result.data.points, patternStartSeconds);
      const origin = next[0]?.stationId;
      const destination = next.at(-1)?.stationId;
      if (!origin || !destination || next.some((stop) => !stations.some((station) => station.stationId === stop.stationId))) {
        toast.add({ title: "走行パターンの駅が見つかりません", type: "error" }); return;
      }
      setTrain((current) => ({ ...current, originStationId: origin, destinationStationId: destination }));
      setStops(buildServiceStops(stations, origin, destination, next, stationHandlings));
      setPatternDepartureTime(formatTrainTime(patternStartSeconds));
    } catch { toast.add({ title: "走行パターンの適用に失敗しました", type: "error" }); }
    finally { setBusy(false); }
  }

  async function save() {
    if (!valid || busy) return;
    const allStops = [...depotExit, ...stops, ...depotEntry];
    const points = buildTrainPoints(allStops);
    let lastTime = -1;
    for (const [index, stop] of allStops.entries()) {
      const station = stations.find((item) => item.stationId === stop.stationId);
      if (!station) {
        toast.add({ title: `地点${index + 1}の駅を選択してください`, type: "error" });
        return;
      }
      const point = points[index];
      if (!station.tracks.some((track) => track.trackCircuitId === point.trackCircuitId)) {
        toast.add({ title: `${station.displayName}の番線を選択してください`, type: "error" });
        return;
      }
      for (const time of [point.arrivalSeconds, point.departureSeconds]) {
        if (Number.isNaN(time)) {
          toast.add({ title: `${station.displayName}の時刻はHH:mmまたはHH:mm:ssで入力してください`, type: "error" });
          return;
        }
        if (time !== null) {
          if (time < lastTime) {
            toast.add({ title: `${station.displayName}の時刻が前の時刻より早くなっています`, type: "error" });
            return;
          }
          lastTime = time;
        }
      }
    }
    setBusy(true);
    try {
      const result = await saveTrain({ ...train, timetableId, points }, savedNumber ?? undefined);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        const nextTrain = { trainNumber: result.data.trainNumber, trainTypeId: result.data.trainTypeId, originStationId: result.data.originStationId, destinationStationId: result.data.destinationStationId };
        setTrain(nextTrain);
        markSaved(trainDraft(nextTrain, stops, depotExit, depotEntry));
        setSavedNumber(result.data.id);
        onSaved?.(result.data.id);
        toast.add({ title: savedNumber === null ? "列車を作成しました" : "保存しました", type: "success" });
      }
    } catch {
      toast.add({ title: "列車の保存に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  return <FloatingWindow title={savedNumber === null ? "新規列車を作成" : `列番編集 — ${train.trainNumber}`} onClose={async () => { if (await confirmDiscard(["train"])) onClose(); }}>
    <div className="flex min-h-full flex-col gap-4">
      <div className="space-y-4">
        {<label className="grid max-w-104 grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs"><span>列番</span><Input autoFocus required value={train.trainNumber} onChange={(event) => setTrain({ ...train, trainNumber: event.target.value })} disabled={busy} className="h-7 border-zinc-500 bg-zinc-900 text-sm dark:bg-zinc-900" /></label>}
        <div role="group" aria-label="種別行先" className="grid grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs">
          <span aria-hidden="true">種別行先</span>
          <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,1.5fr)] gap-2">
            <Select items={types.map((type) => ({ value: type.trainTypeId, label: type.displayName || type.trainTypeId }))} value={train.trainTypeId || null} onValueChange={selectType} disabled={busy || !ready}>
              <SelectTrigger aria-label="列車種別" aria-required="true" size="sm" className="w-full min-w-0 border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
              <SelectContent>{types.map((type) => <SelectItem key={type.trainTypeId} value={type.trainTypeId}>{type.displayName || type.trainTypeId}</SelectItem>)}</SelectContent>
            </Select>
            <Select items={stations.map((station) => ({ value: station.stationId, label: station.displayName }))} value={train.destinationStationId || null} onValueChange={(value) => changeEndpoints({ destinationStationId: value ?? "" })} disabled={busy || !ready}>
              <SelectTrigger aria-label="終着駅" aria-required="true" size="sm" className="w-full min-w-0 border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
              <SelectContent>{stations.map((station) => <SelectItem key={station.stationId} value={station.stationId}>{station.displayName}</SelectItem>)}</SelectContent>
            </Select>
          </div>
        </div>
        <label className="grid max-w-104 grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs"><span>始発駅</span>
          <Select items={stations.map((station) => ({ value: station.stationId, label: station.displayName }))} value={train.originStationId || null} onValueChange={(value) => changeEndpoints({ originStationId: value ?? "" })} disabled={busy || !ready}>
            <SelectTrigger aria-label="始発駅" aria-required="true" size="sm" className="w-full min-w-0 border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
            <SelectContent>{stations.map((station) => <SelectItem key={station.stationId} value={station.stationId}>{station.displayName}</SelectItem>)}</SelectContent>
          </Select>
        </label>
        <label className="grid grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs"><span>走行パターン</span>
          <Select items={patterns.map((pattern) => ({ value: pattern.id, label: pattern.displayName }))} value={patternId} onValueChange={setPatternId} disabled={busy || !ready || patterns.length === 0}>
            <SelectTrigger aria-label="走行パターン" size="sm" className="w-full min-w-0 border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
            <SelectContent>{patterns.map((pattern) => <SelectItem key={pattern.id} value={pattern.id}>{pattern.displayName}</SelectItem>)}</SelectContent>
          </Select>
        </label>
        <div className="grid grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs">
          <label htmlFor={departureTimeId}>始発時刻</label>
          <div className="flex flex-wrap items-center gap-2">
            <Input id={departureTimeId} value={patternDepartureTime} onChange={(event) => setPatternDepartureTime(event.target.value)} disabled={busy || !ready} aria-invalid={patternDepartureTime !== "" && Number.isNaN(patternStartSeconds)} className="h-7 w-32 border-zinc-500 bg-zinc-900 text-center font-mono text-xs dark:bg-zinc-900" />
            <Button type="button" size="xs" onClick={applyPattern} disabled={busy || !ready || !patternId || patternStartSeconds === null || !Number.isFinite(patternStartSeconds)} className="bg-zinc-700 text-zinc-100 hover:bg-zinc-700">パターンを適用</Button>
          </div>
        </div>
      </div>
      <TrainStopTable section="depot_exit" rows={depotExit} stations={stations} disabled={busy || !ready} lastPoint={false} onChange={setDepotExit} />
      <div className="flex justify-end"><Button type="button" size="xs" disabled={busy || !ready || !train.trainTypeId || stops.length === 0} onClick={() => setStops((current) => applyTrainTypeHandlings(current, stationHandlings))} className="bg-zinc-700 text-zinc-100 hover:bg-zinc-700">停車駅を標準に戻す</Button></div>
      <TrainStopTable section="service" rows={stops} stations={stations} disabled={busy || !ready} firstPoint={depotExit.length === 0} lastPoint={depotEntry.length === 0} onChange={setStops} />
      <TrainStopTable section="depot_entry" rows={depotEntry} stations={stations} disabled={busy || !ready} firstPoint={false} onChange={setDepotEntry} />
      <div className="mt-auto flex items-end justify-between gap-3 pt-4">
        <div>
          {!busy && ready && types.length === 0 ? <p className="text-xs text-zinc-400">列車種別が登録されていません</p> : null}
          {busy ? <p role="status" className="text-xs text-zinc-400">読み込み中…</p> : null}
        </div>
        <Button type="button" size="sm" onClick={save} disabled={busy || !valid} className="min-w-24 bg-green-600 text-white hover:bg-green-600">適用</Button>
      </div>
    </div>
  </FloatingWindow>;
}
