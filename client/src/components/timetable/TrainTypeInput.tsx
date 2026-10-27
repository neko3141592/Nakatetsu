"use client";

import { useEffect, useState } from "react";
import { loadTrainTypes } from "@/app/timetables/train-actions";
import { saveTrainType } from "@/app/timetables/train-type-actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { toast } from "@/components/ui/toast";
import type { TrainType } from "@/types/masters";

const newTrainType: TrainType = { trainTypeId: "", displayName: "" };

export function TrainTypeInput() {
  const [types, setTypes] = useState<TrainType[]>([]);
  const [trainType, setTrainType] = useState(newTrainType);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [busy, setBusy] = useState(true);
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

  async function save() {
    if (busy || !trainType.displayName.trim()) return;
    setBusy(true);
    try {
      const result = await saveTrainType(trainType, selectedId === null);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        const saved = result.data;
        setTypes((current) => [...current.filter((item) => item.trainTypeId !== saved.trainTypeId), saved].sort((a, b) => a.trainTypeId.localeCompare(b.trainTypeId)));
        setTrainType(saved);
        setSelectedId(saved.trainTypeId);
        toast.add({ title: "保存しました", type: "success" });
      }
    } catch {
      toast.add({ title: "列車種別の保存に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-label="列車種別の編集" className="flex h-full max-w-xl flex-col bg-zinc-950 text-zinc-200">
      <header className="flex h-9 shrink-0 items-center gap-2 bg-black px-3">
        <Select items={items} value={selectedId ?? ""} disabled={busy} onValueChange={(id) => {
          const selected = types.find((item) => item.trainTypeId === id);
          setSelectedId(selected?.trainTypeId ?? null);
          setTrainType(selected ?? newTrainType);
        }}>
          <SelectTrigger aria-label="列車種別を選択" size="sm" className="min-w-0 border-0 bg-transparent px-0 text-zinc-200 shadow-none dark:bg-transparent">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>{items.map((item) => <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>)}</SelectContent>
        </Select>
        <Button type="button" size="xs" onClick={save} disabled={busy || !trainType.displayName.trim()} className="ml-auto bg-green-600 text-white hover:bg-green-600">適用</Button>
      </header>
      <div className="space-y-3 p-3">
        <label className="flex flex-col gap-1.5 text-xs">
          名称
          <Input value={trainType.displayName} disabled={busy} onChange={(event) => setTrainType((current) => ({ ...current, displayName: event.target.value }))} className="border-zinc-700" />
        </label>
      </div>
    </section>
  );
}
