"use client";

import { useState } from "react";
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { EditorTable } from "@/components/ui/editor-table";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { TimetableSummary } from "@/types/timetable";
import type { TimetableOperationSummary } from "@/types/operation";

type Props = {
  timetables: readonly TimetableSummary[];
  operations: readonly TimetableOperationSummary[];
  selectedOperationNumbers: readonly string[];
  onAddOperation: (operationNumber: string) => void;
  onRemoveOperation: (operationNumber: string) => void;
  selectedTimetableId: string | null;
  onSelectTimetable: (id: string | null) => void;
  displayName: string;
  onDisplayNameChange: (displayName: string) => void;
  onSave: () => void;
  onDelete: () => Promise<string | null>;
  busy: boolean;
};

export function TimetableInput({ timetables, operations, selectedOperationNumbers, onAddOperation, onRemoveOperation, selectedTimetableId, onSelectTimetable, displayName, onDisplayNameChange, onSave, onDelete, busy }: Props) {
  const [operationQuery, setOperationQuery] = useState("");
  const [activeOperationNumber, setActiveOperationNumber] = useState<string | null>(null);
  const [operationDialogOpen, setOperationDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const selectedOperations = new Set(selectedOperationNumbers);
  const query = operationQuery.trim().toLowerCase();
  const filteredOperations = operations.filter((operation) => !selectedOperations.has(operation.operationNumber) && operation.operationNumber.toLowerCase().includes(query));
  const items = [
    { value: "", label: "新規ダイヤ" },
    ...timetables.map((timetable) => ({
      value: timetable.id,
      label: timetable.displayName || timetable.id,
    })),
  ];

  return (
    <section className="flex h-full min-h-0 flex-col bg-zinc-950 text-zinc-200" aria-label="ダイヤ入力">
      <header className="flex h-7 shrink-0 items-center gap-2  bg-black pr-2">
        <Select items={items} value={selectedTimetableId ?? ""} onValueChange={(id) => onSelectTimetable(id || null)} disabled={busy}>
          <SelectTrigger
            aria-label="ダイヤを選択"
            className="min-w-0 justify-start gap-2 rounded-none bg-black px-3 text-sm font-normal text-white aria-expanded:hover:bg-zinc-900 focus-visible:ring-inset focus-visible:ring-zinc-400 data-placeholder:text-white data-[size=default]:h-7 dark:bg-black dark:aria-expanded:hover:bg-zinc-900 [&_svg]:text-white"
          >
            <SelectValue placeholder="新規ダイヤ" className="min-w-0 max-w-[calc(100%-1rem)] flex-none text-left" />
          </SelectTrigger>
          <SelectContent align="start" alignItemWithTrigger={false} className="bg-zinc-900 text-zinc-200 ring-zinc-700">
            {items.map((item) => (
              <SelectItem key={item.value} value={item.value} className="focus-visible:outline-zinc-400 data-highlighted:bg-zinc-700 data-highlighted:text-white">
                {item.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Button type="button" size="xs" onClick={onSave} disabled={busy || !displayName.trim()} className="ml-auto rounded-xs bg-zinc-800 text-zinc-200 hover:bg-zinc-700">
          保存
        </Button>
      </header>
      <div className="min-h-0 flex-1 space-y-3 overflow-auto p-3">
        <label className="flex flex-col gap-1.5 text-xs">
          名称
          <Input value={displayName} onChange={(event) => onDisplayNameChange(event.target.value)} disabled={busy} className="rounded-xs border-zinc-700 text-sm" />
        </label>
        <section aria-label="ダイヤの運用" className="space-y-2">
          <h2 className="text-xs">運用</h2>
          <Dialog open={operationDialogOpen} onOpenChange={setOperationDialogOpen}>
            <EditorTable
              rows={selectedOperationNumbers}
              columns={[{ id: "operationNumber", header: "運用番号", cell: (number) => number }]}
              getRowId={(number) => number}
              selectedId={activeOperationNumber}
              onSelectionChange={setActiveOperationNumber}
              onAdd={() => setOperationDialogOpen(true)}
              onRemove={onRemoveOperation}
              label="ダイヤに所属する運用"
              addLabel="運用を追加"
              removeLabel="選択した運用を外す"
              emptyMessage="運用が追加されていません"
              disabled={busy}
            />
            <DialogContent
              aria-describedby={undefined}
              showCloseButton={false}
              className="gap-0 overflow-hidden rounded-xs border border-zinc-700 bg-zinc-950 p-0 text-zinc-200 shadow-none ring-0 sm:max-w-md"
            >
              <div className="border-b border-zinc-600 bg-neutral-900 px-3 py-2">
                <DialogTitle className="text-sm font-normal tracking-wider">運用を追加</DialogTitle>
              </div>
              <div className="space-y-3 p-4">
                <label className="flex items-center gap-3 text-sm">
                  <span className="shrink-0">運用番号</span>
                  <Input
                    placeholder="番号を入力"
                    value={operationQuery}
                    onChange={(event) => setOperationQuery(event.target.value)}
                    className="h-7 rounded-xs border border-zinc-700 bg-black px-2 text-zinc-100 shadow-none transition-none focus-visible:border-zinc-400 focus-visible:ring-0 dark:bg-black"
                  />
                </label>
                <div className="overflow-hidden rounded-xs border border-zinc-600">
                  <div className="border-b border-zinc-600 px-2 py-1 text-xs tracking-wider">運用番号</div>
                  <ul aria-label="運用一覧" className="h-56 overflow-y-auto bg-black text-sm">
                    {filteredOperations.map((operation) => (
                      <li key={operation.operationNumber} className="border-b border-zinc-800">
                        <DialogClose
                          onClick={() => {
                            onAddOperation(operation.operationNumber);
                            setActiveOperationNumber(operation.operationNumber);
                          }}
                          disabled={busy}
                          render={<Button type="button" variant="ghost" className="h-7 w-full justify-start rounded-xs px-2 font-normal text-zinc-200 transition-none hover:bg-[#15334a] hover:text-white dark:hover:bg-[#15334a]" />}
                        >
                          {operation.operationNumber}
                        </DialogClose>
                      </li>
                    ))}
                  </ul>
                </div>
                {filteredOperations.length === 0 ? <p role="status" className="text-xs text-zinc-400">{operations.length === 0 ? "運用が登録されていません" : "追加できる運用が見つかりません"}</p> : null}
                <div className="flex items-center justify-between border-t border-zinc-700 pt-3">
                  <span className="text-xs text-zinc-400">{filteredOperations.length} 件</span>
                  <DialogClose render={<Button type="button" className="h-7 min-w-24 rounded-xs bg-zinc-800 text-sm font-normal text-zinc-200 transition-none hover:bg-zinc-800" />}>
                    閉じる
                  </DialogClose>
                </div>
              </div>
            </DialogContent>
          </Dialog>
        </section>
        <Dialog open={deleteDialogOpen} onOpenChange={(open) => {
          if (busy) return;
          setDeleteDialogOpen(open);
        }}>
          <DialogTrigger
            disabled={busy || selectedTimetableId === null}
            render={<Button type="button" size="sm" variant="destructive" className="w-full rounded-xs bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400" />}
          >
            ダイヤを削除
          </DialogTrigger>
          <DialogContent showCloseButton={false} className="gap-0 overflow-hidden rounded-xs border border-zinc-700 bg-zinc-950 p-0 text-zinc-200 shadow-none ring-0 sm:max-w-md">
            <div className="border-b border-zinc-600 bg-neutral-900 px-3 py-2">
              <DialogTitle className="text-sm font-normal tracking-wider">ダイヤを削除</DialogTitle>
            </div>
            <div className="space-y-4 p-4">
              <DialogDescription className="break-words text-sm text-zinc-200">
                「{displayName}」を削除しますか？
              </DialogDescription>
              <div className="flex justify-end gap-2">
                <DialogClose disabled={busy} render={<Button type="button" size="sm" className="rounded-xs bg-zinc-800 text-zinc-200 hover:bg-zinc-800" />}>
                  キャンセル
                </DialogClose>
                <Button
                  type="button"
                  size="sm"
                  disabled={busy}
                  onClick={async () => {
                    const error = await onDelete();
                    if (error) toast.add({ title: error, type: "error" });
                    else setDeleteDialogOpen(false);
                  }}
                  className="rounded-xs bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400"
                >
                  {busy ? "削除中…" : "削除する"}
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      </div>
    </section>
  );
}
