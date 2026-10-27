"use client";

import { useEffect, useState } from "react";
import { loadTrain, loadTrainTypes, loadStations, saveTrain } from "@/app/timetables/train-actions";
import { TrainStopTable } from "@/components/timetable/TrainStopTable";
import { FloatingWindow } from "@/components/ui/floating-window";
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import stationLayout from "@/data/timetable-diagrams/NtLine.json";
import { createTrainStop, getTrainStations, trainStopToPoint, type TrainStopDraft } from "@/lib/trainTimetable";
import type { StationDetail, TrainType } from "@/types/masters";
import type { TimetableTrainBasics } from "@/types/train";

export function TrainEditWindow({ trainNumber, onClose }: { trainNumber?: string; onClose: () => void }) {
  const [savedNumber, setSavedNumber] = useState(trainNumber ?? null);
  const [train, setTrain] = useState<TimetableTrainBasics>({ trainNumber: trainNumber ?? "", trainTypeId: "", originStationId: "", destinationStationId: "" });
  const [types, setTypes] = useState<TrainType[]>([]);
  const [stations, setStations] = useState<StationDetail[]>([]);
  const [stops, setStops] = useState<Record<string, TrainStopDraft>>({});
  const [busy, setBusy] = useState(true);
  const [ready, setReady] = useState(false);
  const rows = getTrainStations(stations, train.originStationId, train.destinationStationId).map((station) => ({ station, stop: stops[station.stationId] ?? createTrainStop(station.stationId) }));
  const valid = ready && !!train.trainNumber.trim() && types.some((type) => type.trainTypeId === train.trainTypeId) && rows.length > 0;

  useEffect(() => {
    let active = true;
    async function load() {
      try {
        const [detail, catalog, stationList] = await Promise.all([trainNumber ? loadTrain(trainNumber) : null, loadTrainTypes(), loadStations()]);
        if (!active) return;
        for (const result of [detail, catalog, stationList]) {
          if (result?.error !== undefined) toast.add({ title: result.error, type: "error" });
        }
        if (detail?.error !== undefined || catalog.error !== undefined || stationList.error !== undefined) return;
        if (detail) {
          setTrain({ trainNumber: detail.data.trainNumber, trainTypeId: detail.data.trainTypeId, originStationId: detail.data.originStationId, destinationStationId: detail.data.destinationStationId });
          setStops(Object.fromEntries((detail.data.points ?? []).filter((point) => point.stationId !== null).map((point) => [point.stationId!, createTrainStop(point.stationId!, point)])));
        }
        setTypes(catalog.data);
        const byId = new Map(stationList.data.map((station) => [station.stationId, station]));
        setStations(stationLayout.stations.flatMap(({ stationId }) => byId.has(stationId) ? [byId.get(stationId)!] : []));
        setReady(true);
      } catch {
        if (active) toast.add({ title: "列車・マスターの取得に失敗しました", type: "error" });
      } finally {
        if (active) setBusy(false);
      }
    }
    void load();
    return () => { active = false; };
  }, [trainNumber]);

  async function save() {
    if (!valid || busy) return;
    const points = rows.map(({ stop }, index) => trainStopToPoint(stop, index));
    let lastTime = -1;
    for (const [index, { station }] of rows.entries()) {
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
      const result = await saveTrain({ ...train, points }, savedNumber === null);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setTrain(result.data);
        setSavedNumber(result.data.trainNumber);
        toast.add({ title: savedNumber === null ? "列車を作成しました" : "保存しました", type: "success" });
      }
    } catch {
      toast.add({ title: "列車の保存に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  return <FloatingWindow title={savedNumber === null ? "新規列車を作成" : `列番編集 — ${savedNumber}`} onClose={onClose}>
    <div className="flex min-h-full flex-col gap-8">
      <div className="space-y-4">
        {savedNumber === null ? <label className="grid max-w-104 grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs"><span>列番</span><Input autoFocus required value={train.trainNumber} onChange={(event) => setTrain({ ...train, trainNumber: event.target.value })} disabled={busy} className="h-7 border-zinc-500 bg-zinc-900 text-sm dark:bg-zinc-900" /></label> : null}
        <div role="group" aria-label="種別行先" className="grid grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs">
          <span aria-hidden="true">種別行先</span>
          <div className="grid grid-cols-[minmax(0,1fr)_minmax(0,1.5fr)] gap-2">
            <Select items={types.map((type) => ({ value: type.trainTypeId, label: type.displayName || type.trainTypeId }))} value={train.trainTypeId || null} onValueChange={(value) => setTrain({ ...train, trainTypeId: value ?? "" })} disabled={busy || !ready}>
              <SelectTrigger aria-label="列車種別" aria-required="true" size="sm" className="w-full min-w-0 border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
              <SelectContent>{types.map((type) => <SelectItem key={type.trainTypeId} value={type.trainTypeId}>{type.displayName || type.trainTypeId}</SelectItem>)}</SelectContent>
            </Select>
            <Select items={stations.map((station) => ({ value: station.stationId, label: station.displayName }))} value={train.destinationStationId || null} onValueChange={(value) => setTrain({ ...train, destinationStationId: value ?? "" })} disabled={busy || !ready}>
              <SelectTrigger aria-label="終着駅" aria-required="true" size="sm" className="w-full min-w-0 border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
              <SelectContent>{stations.map((station) => <SelectItem key={station.stationId} value={station.stationId}>{station.displayName}</SelectItem>)}</SelectContent>
            </Select>
          </div>
        </div>
        <label className="grid max-w-104 grid-cols-[7rem_minmax(0,1fr)] items-center gap-3 text-xs"><span>始発駅</span>
          <Select items={stations.map((station) => ({ value: station.stationId, label: station.displayName }))} value={train.originStationId || null} onValueChange={(value) => setTrain({ ...train, originStationId: value ?? "" })} disabled={busy || !ready}>
            <SelectTrigger aria-label="始発駅" aria-required="true" size="sm" className="w-full min-w-0 border border-zinc-500 bg-zinc-900 text-zinc-100 dark:bg-zinc-900"><SelectValue placeholder="" /></SelectTrigger>
            <SelectContent>{stations.map((station) => <SelectItem key={station.stationId} value={station.stationId}>{station.displayName}</SelectItem>)}</SelectContent>
          </Select>
        </label>
      </div>
      {rows.length > 0 ? <TrainStopTable rows={rows} disabled={busy} onChange={(stationId, stop) => setStops((current) => ({ ...current, [stationId]: stop }))} /> : null}
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
