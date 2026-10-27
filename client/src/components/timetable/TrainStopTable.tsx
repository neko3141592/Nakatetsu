"use client";

import { useState } from "react";
import { ArrowDown, ArrowUp, Minus, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { createTrainStop, parseTrainTime, type TrainStopDraft } from "@/lib/trainTimetable";
import type { StationDetail } from "@/types/masters";
import type { PointSection } from "@/types/train";

const fieldClass = "h-7 w-full min-w-0 border-zinc-500 bg-zinc-900 text-xs text-zinc-100 dark:bg-zinc-900";
const controlClass = "rounded-none bg-transparent text-zinc-300 hover:bg-transparent hover:text-zinc-300 dark:hover:bg-transparent";

export function TrainStopTable({ section, rows, stations, disabled, firstPoint = true, lastPoint = true, zeroDeparture = false, title: customTitle, onChange }: {
  section: PointSection;
  rows: TrainStopDraft[];
  stations: StationDetail[];
  disabled: boolean;
  firstPoint?: boolean;
  lastPoint?: boolean;
  zeroDeparture?: boolean;
  title?: string;
  onChange: (rows: TrainStopDraft[]) => void;
}) {
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const editable = section !== "service";
  const title = customTitle ?? (section === "depot_exit" ? "出庫" : section === "depot_entry" ? "入庫" : "時刻表");
  const selectedIndex = rows.findIndex((row) => row.rowId === selectedId);
  const stationItems = stations.map((station) => ({ value: station.stationId, label: station.displayName }));

  function changePath(next: TrainStopDraft[]) {
    onChange(next.map((row) => ({ ...row, routes: [] })));
  }

  function update(rowId: string, patch: Partial<TrainStopDraft>) {
    const next = rows.map((row) => row.rowId === rowId ? { ...row, ...patch } : row);
    if (patch.stationId !== undefined || patch.trackCircuitId !== undefined) changePath(next);
    else onChange(next);
  }

  function add() {
    const row = createTrainStop(null, undefined, section);
    const index = selectedIndex < 0 ? rows.length : selectedIndex + 1;
    changePath([...rows.slice(0, index), row, ...rows.slice(index)]);
    setSelectedId(row.rowId);
  }

  function remove() {
    if (selectedIndex < 0) return;
    changePath(rows.filter((row) => row.rowId !== selectedId));
    setSelectedId(rows[selectedIndex + 1]?.rowId ?? rows[selectedIndex - 1]?.rowId ?? null);
  }

  function move(direction: -1 | 1) {
    const target = selectedIndex + direction;
    if (selectedIndex < 0 || target < 0 || target >= rows.length) return;
    const next = [...rows];
    [next[selectedIndex], next[target]] = [next[target], next[selectedIndex]];
    changePath(next);
  }

  return <section aria-label={title} className="space-y-1">
    <h3 className="text-xs">{title}</h3>
    <div className="overflow-hidden rounded-xs border border-zinc-600">
    <div className="overflow-x-auto">
    <table aria-label={title} className="w-full min-w-140 table-fixed border-collapse text-xs">
      <colgroup>{editable ? <col className="w-8" /> : null}<col /><col /><col /><col />{!editable ? <col /> : null}</colgroup>
      <thead className="bg-zinc-950 text-zinc-200"><tr>
        {editable ? <th scope="col" className="py-2 font-normal">順</th> : null}
        {["駅名", "到着時刻", "出発時刻", "番線", ...(!editable ? ["通過／停車"] : [])].map((label) => <th key={label} scope="col" className="px-2 py-2 text-left font-normal">{label}</th>)}
      </tr></thead>
      <tbody>{rows.map((stop, index) => {
        const station = stations.find((item) => item.stationId === stop.stationId);
        const label = `${title} 地点${index + 1}`;
        const tracks = station?.tracks ?? [];
        return <tr key={stop.rowId} onFocusCapture={() => setSelectedId(stop.rowId)} className={editable && selectedId === stop.rowId ? "bg-sky-950" : "odd:bg-zinc-800 even:bg-zinc-700/50"}>
        {editable ? <th scope="row" className="p-1 font-normal">
          <Button type="button" size="icon-xs" variant="ghost" aria-label={`${label}を選択`} aria-pressed={selectedId === stop.rowId} disabled={disabled}
            onClick={() => setSelectedId(stop.rowId)} className={controlClass}>{index + 1}</Button>
        </th> : null}
        <td className="p-1">
          {editable ? <Select items={stationItems} value={stop.stationId} disabled={disabled} onValueChange={(value) => update(stop.rowId, { stationId: value, trackCircuitId: "" })}>
            <SelectTrigger aria-label={`${label} 駅名`} size="sm" className={fieldClass}><SelectValue placeholder="" /></SelectTrigger>
            <SelectContent>{stationItems.map((item) => <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>)}</SelectContent>
          </Select> : <span className="block px-1">{station?.displayName}</span>}
        </td>
        {(["arrivalTime", "departureTime"] as const).map((field, timeIndex) => {
          const passArrival = field === "arrivalTime" && stop.handling === "pass";
          const zeroOrigin = zeroDeparture && field === "departureTime" && index === 0;
          const endpoint = (field === "arrivalTime" && firstPoint && index === 0) || (field === "departureTime" && lastPoint && index === rows.length - 1);
          return <td key={field} className="p-1">
          <Input aria-label={`${label} ${timeIndex === 0 ? "到着時刻" : "出発時刻"}`} placeholder="非探時" value={zeroOrigin ? "00:00:00" : endpoint ? "===" : passArrival ? "レ" : stop[field]} readOnly={endpoint || passArrival || zeroOrigin} disabled={disabled}
            aria-invalid={!endpoint && !passArrival && !zeroOrigin && Number.isNaN(parseTrainTime(stop[field]))} onChange={(event) => update(stop.rowId, { [field]: event.target.value })}
            className={`${fieldClass} px-1 text-center font-mono`} />
        </td>;
        })}
        <td className="p-1">
          <Select items={tracks.map((track) => ({ value: track.trackCircuitId, label: track.displayName }))} value={stop.trackCircuitId || null}
            disabled={disabled || !station} onValueChange={(value) => update(stop.rowId, { trackCircuitId: value ?? "" })}>
            <SelectTrigger aria-label={`${label} 番線`} size="sm" className={fieldClass}><SelectValue placeholder="" /></SelectTrigger>
            <SelectContent>{tracks.map((track) => <SelectItem key={track.trackId} value={track.trackCircuitId}>{track.displayName}</SelectItem>)}</SelectContent>
          </Select>
        </td>
        {!editable ? <td className="p-1">
          <Button type="button" size="sm" aria-label={`${label} 通過／停車`} aria-pressed={stop.handling === "pass"} disabled={disabled || (zeroDeparture && (index === 0 || index === rows.length - 1))}
            onClick={() => update(stop.rowId, { handling: stop.handling === "stop" ? "pass" : "stop" })}
            className={`h-7 w-full min-w-0 border-zinc-500 text-xs ${stop.handling === "pass" ? "bg-yellow-300 text-black hover:bg-yellow-300 dark:bg-yellow-300" : "bg-zinc-900 text-zinc-100 hover:bg-zinc-900 dark:bg-zinc-900"}`}>
            {stop.handling === "pass" ? "通過" : stop.handling === "stop" ? "停車" : ""}
          </Button>
        </td> : null}
      </tr>;
      })}
      {rows.length === 0 ? <tr><td colSpan={5} className="h-10 text-center text-zinc-400">{editable ? "走行地点を追加してください" : "始発駅・終着駅を設定してください"}</td></tr> : null}
      </tbody>
    </table>
    </div>
    {editable ? <div className="flex h-8 items-center border-t border-zinc-600 bg-zinc-900 px-1">
      <Button type="button" size="icon-xs" variant="ghost" aria-label={`${title}の走行地点を追加`} disabled={disabled} onClick={add} className={controlClass}><Plus aria-hidden="true" /></Button>
      <span aria-hidden="true" className="mx-1 h-4 w-px bg-zinc-700" />
      <Button type="button" size="icon-xs" variant="ghost" aria-label={`${title}の選択した走行地点を削除`} disabled={disabled || selectedIndex < 0} onClick={remove} className={controlClass}><Minus aria-hidden="true" /></Button>
      <div className="ml-auto flex items-center gap-1">
        <Button type="button" size="icon-xs" variant="ghost" aria-label={`${title}の走行地点を上へ`} disabled={disabled || selectedIndex <= 0} onClick={() => move(-1)} className={controlClass}><ArrowUp aria-hidden="true" /></Button>
        <Button type="button" size="icon-xs" variant="ghost" aria-label={`${title}の走行地点を下へ`} disabled={disabled || selectedIndex < 0 || selectedIndex === rows.length - 1} onClick={() => move(1)} className={controlClass}><ArrowDown aria-hidden="true" /></Button>
      </div>
    </div> : null}
    </div>
  </section>;
}
