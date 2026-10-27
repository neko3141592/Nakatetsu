"use client";

import { useState } from "react";
import { toast } from "@/components/ui/toast";
import { deleteOperation, loadOperation, loadTrainList, saveOperation } from "@/app/timetables/operation-actions";
import { TrainEditWindow } from "@/components/timetable/TrainEditWindow";
import { Button } from "@/components/ui/button";
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { EditorTable } from "@/components/ui/editor-table";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { TimetableOperationDetail, TimetableOperationSummary } from "@/types/operation";
import type { TimetableTrainSummary } from "@/types/train";

function createNewOperation(): TimetableOperationDetail {
  return { operationNumber: "", trains: [] };
}

type Props = {
  operations: readonly TimetableOperationSummary[];
  onSaved: (operation: TimetableOperationSummary) => void;
  onDeleted: (operationNumber: string) => void;
};

export function OperationInput({ operations, onSaved, onDeleted }: Props) {
  const [operation, setOperation] = useState<TimetableOperationDetail>(createNewOperation);
  const [selectedOperationNumber, setSelectedOperationNumber] = useState<string | null>(null);
  const [selectedTrain, setSelectedTrain] = useState<string | null>(null);
  const [editingTrain, setEditingTrain] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [trains, setTrains] = useState<TimetableTrainSummary[]>([]);
  const [query, setQuery] = useState("");
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const items = [{ value: "", label: "新規運用" }, ...operations.map(({ operationNumber }) => ({ value: operationNumber, label: operationNumber }))];
  const candidates = trains.filter((train) => !operation.trains.some((item) => item.trainNumber === train.trainNumber) && train.trainNumber.toLowerCase().includes(query.trim().toLowerCase()));

  async function select(number: string | null) {
    if (!number) {
      setOperation(createNewOperation());
      setSelectedOperationNumber(null);
      setSelectedTrain(null);
      setEditingTrain(null);
      return;
    }
    setBusy(true);
    try {
      const result = await loadOperation(number);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setOperation(result.data);
        setSelectedOperationNumber(number);
        setSelectedTrain(null);
        setEditingTrain(null);
      }
    } catch {
      toast.add({ title: "運用の取得に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  function updateTrains(numbers: string[]) {
    setOperation((current) => ({ ...current, trains: numbers.map((trainNumber, sequenceIndex) => ({ trainNumber, sequenceIndex })) }));
    toast.add({ title: "未保存の変更があります", type: "info", id: "operation-result" });
  }

  function move(number: string, direction: -1 | 1) {
    const numbers = operation.trains.map((train) => train.trainNumber);
    const index = numbers.indexOf(number);
    const next = index + direction;
    if (index < 0 || next < 0 || next >= numbers.length) return;
    [numbers[index], numbers[next]] = [numbers[next], numbers[index]];
    updateTrains(numbers);
  }

  async function save() {
    if (!operation.operationNumber.trim()) return;
    setBusy(true);
    try {
      const result = await saveOperation(operation, selectedOperationNumber === null);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setOperation(result.data);
        setSelectedOperationNumber(result.data.operationNumber);
        onSaved({ operationNumber: result.data.operationNumber });
        toast.add({ title: "保存しました", type: "success", id: "operation-result" });
      }
    } catch {
      toast.add({ title: "運用の保存に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  async function openTrainDialog() {
    setBusy(true);
    try {
      const result = await loadTrainList();
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setTrains(result.data);
        setQuery("");
        setDialogOpen(true);
      }
    } catch {
      toast.add({ title: "列車一覧の取得に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  async function remove() {
    if (selectedOperationNumber === null || busy) return;
    setBusy(true);
    try {
      const result = await deleteOperation(selectedOperationNumber);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        onDeleted(selectedOperationNumber);
        setOperation(createNewOperation());
        setSelectedOperationNumber(null);
        setSelectedTrain(null);
          setEditingTrain(null);
        setDeleteDialogOpen(false);
        toast.add({ title: "削除しました", type: "success", id: "operation-result" });
      }
    } catch {
      toast.add({ title: "運用の削除に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  return (
    <section aria-label="列番・運用の編集画面" className="flex h-full min-h-0 flex-col bg-zinc-950 text-zinc-200">
      <header className="flex h-7 shrink-0 items-center gap-2 bg-black pr-2">
        <Select items={items} value={selectedOperationNumber ?? ""} onValueChange={select} disabled={busy}>
          <SelectTrigger aria-label="運用を選択" className="min-w-0 rounded-none bg-black px-3 text-sm font-normal text-white aria-expanded:hover:bg-zinc-900 data-placeholder:text-white data-[size=default]:h-7 dark:bg-black dark:aria-expanded:hover:bg-zinc-900 [&_svg]:text-white">
            <SelectValue placeholder="新規運用" />
          </SelectTrigger>
          <SelectContent align="start" alignItemWithTrigger={false} className="bg-zinc-900 text-zinc-200 ring-zinc-700">
            {items.map((item) => <SelectItem key={item.value} value={item.value}>{item.label}</SelectItem>)}
          </SelectContent>
        </Select>
        <Button type="button" size="xs" onClick={save} disabled={busy || !operation.operationNumber.trim()} className="ml-auto bg-zinc-800 text-zinc-200 hover:bg-zinc-800">保存</Button>
      </header>
      <div className="min-h-0 flex-1 space-y-3 overflow-auto p-3">
        <label className="flex flex-col gap-1.5 text-xs">
          運用番号
          <Input value={operation.operationNumber} onChange={(event) => {
            setOperation((current) => ({ ...current, operationNumber: event.target.value }));
          }} disabled={busy || selectedOperationNumber !== null} className="border-zinc-700" />
        </label>
        <h2 className="text-xs">所属列車</h2>
        <EditorTable
          rows={operation.trains}
          columns={[{ id: "trainNumber", header: "列番", cell: (train) => train.trainNumber }]}
          getRowId={(train) => train.trainNumber}
          selectedId={selectedTrain}
          onSelectionChange={(number) => { setSelectedTrain(number); setEditingTrain(number); }}
          onAdd={openTrainDialog}
          onRemove={(number) => updateTrains(operation.trains.filter((train) => train.trainNumber !== number).map((train) => train.trainNumber))}
          onMove={move}
          label="運用に所属する列車"
          addLabel="既存列車を追加"
          removeLabel="選択した列車を外す"
          emptyMessage="列車が追加されていません"
          disabled={busy}
        />
        <Button type="button" size="sm" onClick={() => setEditingTrain("")} disabled={busy} className="w-full bg-zinc-800 text-zinc-200 hover:bg-zinc-800">新規列車を作成</Button>
        <Dialog open={deleteDialogOpen} onOpenChange={(open) => {
          if (busy) return;
          setDeleteDialogOpen(open);
        }}>
          <DialogTrigger disabled={busy || selectedOperationNumber === null} render={<Button type="button" size="sm" variant="destructive" className="w-full bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400" />}>
            運用を削除
          </DialogTrigger>
          <DialogContent showCloseButton={false} className="gap-0 overflow-hidden border border-zinc-700 bg-zinc-950 p-0 text-zinc-200 shadow-none ring-0 sm:max-w-md">
            <div className="border-b border-zinc-600 bg-neutral-900 px-3 py-2">
              <DialogTitle className="text-sm font-normal tracking-wider">運用を削除</DialogTitle>
            </div>
            <div className="space-y-4 p-4">
              <DialogDescription className="break-words text-sm text-zinc-200">
                運用「{selectedOperationNumber}」を削除しますか？ 所属する列車の定義は残ります。
              </DialogDescription>
              <div className="flex justify-end gap-2">
                <DialogClose disabled={busy} render={<Button type="button" size="sm" className="bg-zinc-800 text-zinc-200 hover:bg-zinc-800" />}>キャンセル</DialogClose>
                <Button type="button" size="sm" onClick={remove} disabled={busy} className="bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400">
                  {busy ? "削除中…" : "削除する"}
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      </div>
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent aria-describedby={undefined} className="border border-zinc-700 bg-zinc-950 text-zinc-200 shadow-none ring-0">
          <DialogTitle className="text-sm font-normal">列車を追加</DialogTitle>
          <Input aria-label="列番で検索" placeholder="列番で検索" value={query} onChange={(event) => setQuery(event.target.value)} className="border-zinc-700" />
          <ul aria-label="追加できる列車" className="max-h-64 overflow-y-auto">
            {candidates.map((train) => (
              <li key={train.trainNumber}>
                <DialogClose render={<Button type="button" variant="ghost" className="w-full justify-start text-zinc-200 hover:bg-zinc-800 hover:text-white" />} onClick={() => {
                  updateTrains([...operation.trains.map((item) => item.trainNumber), train.trainNumber]);
                  setSelectedTrain(train.trainNumber);
                }}>{train.trainNumber}</DialogClose>
              </li>
            ))}
          </ul>
          {candidates.length === 0 ? <p className="text-xs text-zinc-400">追加できる列車がありません</p> : null}
        </DialogContent>
      </Dialog>
      {editingTrain !== null ? <TrainEditWindow key={editingTrain} trainNumber={editingTrain || undefined} onClose={() => setEditingTrain(null)} /> : null}
    </section>
  );
}
