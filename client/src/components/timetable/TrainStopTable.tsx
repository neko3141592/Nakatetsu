"use client";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { parseTrainTime, type TrainStopDraft } from "@/lib/trainTimetable";
import type { StationDetail } from "@/types/masters";

const fieldClass = "h-7 w-full min-w-0 border-zinc-500 bg-zinc-900 text-xs text-zinc-100 dark:bg-zinc-900";

export function TrainStopTable({ rows, disabled, onChange }: {
  rows: { station: StationDetail; stop: TrainStopDraft }[];
  disabled: boolean;
  onChange: (stationId: string, stop: TrainStopDraft) => void;
}) {
  return <div className="overflow-x-auto rounded-xs border border-zinc-600">
    <table aria-label="各駅の時刻・番線" className="w-full min-w-140 table-fixed border-collapse text-xs">
      <thead className="bg-zinc-950 text-zinc-200"><tr>
        {["駅名", "到着時刻", "出発時刻", "番線", "通過／停車"].map((label) => <th key={label} scope="col" className="px-2 py-2 text-left font-normal">{label}</th>)}
      </tr></thead>
      <tbody>{rows.map(({ station, stop }) => <tr key={station.stationId} className="odd:bg-zinc-800 even:bg-zinc-700/50">
        <th scope="row" className="px-2 py-1 text-left font-normal">{station.displayName}</th>
        {(["arrivalTime", "departureTime"] as const).map((field, index) => <td key={field} className="p-1">
          <Input aria-label={`${station.displayName} ${index === 0 ? "到着時刻" : "出発時刻"}`} value={stop[field]} disabled={disabled}
            aria-invalid={Number.isNaN(parseTrainTime(stop[field]))} onChange={(event) => onChange(station.stationId, { ...stop, [field]: event.target.value })}
            className={`${fieldClass} px-1 text-center font-mono`} />
        </td>)}
        <td className="p-1">
          <Select items={station.tracks.map((track) => ({ value: track.trackCircuitId, label: track.displayName }))} value={stop.trackCircuitId || null}
            disabled={disabled} onValueChange={(value) => onChange(station.stationId, { ...stop, trackCircuitId: value ?? "" })}>
            <SelectTrigger aria-label={`${station.displayName} 番線`} size="sm" className={fieldClass}><SelectValue placeholder="" /></SelectTrigger>
            <SelectContent>{station.tracks.map((track) => <SelectItem key={track.trackId} value={track.trackCircuitId}>{track.displayName}</SelectItem>)}</SelectContent>
          </Select>
        </td>
        <td className="p-1">
          <Button type="button" size="sm" aria-label={`${station.displayName} 通過／停車`} aria-pressed={stop.handling === "pass"} disabled={disabled}
            onClick={() => onChange(station.stationId, { ...stop, handling: stop.handling === "stop" ? "pass" : "stop" })}
            className={`h-7 w-full min-w-0 border-zinc-500 text-xs ${stop.handling === "pass" ? "bg-yellow-300 text-black hover:bg-yellow-300 dark:bg-yellow-300" : "bg-zinc-900 text-zinc-100 hover:bg-zinc-900 dark:bg-zinc-900"}`}>
            {stop.handling === "pass" ? "通過" : stop.handling === "stop" ? "停車" : ""}
          </Button>
        </td>
      </tr>)}</tbody>
    </table>
  </div>;
}
