"use client";
import { useState } from "react";
import { formatTrainTime } from "@/lib/trainTimetable";
import {
  copyTrain,
  deleteTrain,
  loadTrainList,
} from "@/app/timetables/train-actions";
import { DeleteConfirmation } from "@/components/timetable/DeleteConfirmation";
import { TimetableSelect } from "@/components/timetable/TimetableSelect";
import { useDiscardChanges } from "@/components/timetable/UnsavedChanges";
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { EditorTable } from "@/components/ui/editor-table";
import type { Timetable, TimetableSummary } from "@/types/timetable";
import type { TimetableTrainSummary } from "@/types/train";
import type { TimetableDiagramStation } from "@/types/timetableDiagram";

type Props = {
  timetables: readonly TimetableSummary[];
  timetable: Timetable;
  stations: readonly TimetableDiagramStation[];
  onSelectTimetable: (id: string | null) => void;
  onDisplayNameChange: (name: string) => void;
  onSave: () => void;
  onDelete: () => Promise<boolean>;
  onEditTrain: (id: string | null) => Promise<boolean>;
  onCloseTrain: () => void;
  onTrainsChanged: () => Promise<void>;
  busy: boolean;
};
export function TimetableInput({
  timetables,
  timetable,
  stations,
  onSelectTimetable,
  onDisplayNameChange,
  onSave,
  onDelete,
  onEditTrain,
  onCloseTrain,
  onTrainsChanged,
  busy,
}: Props) {
  const { confirmDiscard } = useDiscardChanges();
  const [selected, setSelected] = useState<string | null>(null);
  const [removeOpen, setRemoveOpen] = useState(false);
  const [removing, setRemoving] = useState<string | null>(null);
  const [copyOpen, setCopyOpen] = useState(false);
  const [candidates, setCandidates] = useState<TimetableTrainSummary[]>([]);
  const [query, setQuery] = useState("");
  const [pending, setPending] = useState(false);
  const stationName = (id: string) =>
    stations.find((station) => station.stationId === id)?.displayName ?? id;
  async function openCopy() {
    setPending(true);
    try {
      const result = await loadTrainList();
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      setCandidates(result.data);
      setQuery("");
      setCopyOpen(true);
    } finally {
      setPending(false);
    }
  }
  async function copy(id: string) {
    setPending(true);
    try {
      const result = await copyTrain(id, timetable.id);
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      setCopyOpen(false);
      await onTrainsChanged();
      toast.add({ title: "列車を複製しました", type: "success" });
    } finally {
      setPending(false);
    }
  }
  const disabled = busy || pending;
  return (
    <section
      className="flex h-full min-h-0 flex-col bg-zinc-950 text-zinc-200"
      aria-label="ダイヤ入力"
    >
      <header className="flex h-7 shrink-0 items-center gap-2 bg-black pr-2">
        <TimetableSelect
          timetables={timetables}
          value={timetable.id}
          onChange={onSelectTimetable}
          disabled={disabled}
        />
        <Button
          size="xs"
          onClick={onSave}
          disabled={disabled || !timetable.displayName.trim()}
          className="ml-auto bg-zinc-800 text-zinc-200 hover:bg-zinc-800"
        >
          保存
        </Button>
      </header>
      <div className="min-h-0 flex-1 space-y-3 overflow-auto p-3">
        <label className="flex flex-col gap-1.5 text-xs">
          名称
          <Input
            value={timetable.displayName}
            onChange={(e) => onDisplayNameChange(e.target.value)}
            disabled={disabled}
            className="border-zinc-700"
          />
        </label>
        <h2 className="text-xs">列車</h2>
        <EditorTable
          rows={timetable.trains}
          columns={[
            {
              id: "number",
              header: "列番",
              cell: (train) => train.trainNumber,
            },
            {
              id: "type",
              header: "種別",
              cell: (train) => train.trainType?.displayName,
            },
            {
              id: "destination",
              header: "行先",
              cell: (train) => stationName(train.destinationStationId),
            },
          ]}
          getRowId={(train) => train.id}
          selectedId={selected}
          onSelectionChange={(id) => {
            setSelected(id);
            if (id) void onEditTrain(id);
          }}
          onAdd={() => {
            void onEditTrain("");
          }}
          onRemove={(id) => {
            setRemoving(id);
            setRemoveOpen(true);
          }}
          label="ダイヤの列車"
          addLabel="新規列車を作成"
          removeLabel="選択した列車を削除"
          emptyMessage="列車が追加されていません"
          disabled={disabled || !timetable.id}
        />
        {!timetable.id ? (
          <p className="text-xs text-zinc-400">先にダイヤを保存してください</p>
        ) : null}
        <Button
          size="sm"
          onClick={openCopy}
          disabled={disabled || !timetable.id}
          className="w-full bg-zinc-800 text-zinc-200 hover:bg-zinc-800"
        >
          既存列車を複製して追加
        </Button>
        <DeleteConfirmation
          label="ダイヤを削除"
          description={`「${timetable.displayName}」と、このダイヤの列車・運用計画を削除しますか？`}
          disabled={disabled || !timetable.id}
          onDelete={onDelete}
        />
      </div>
      <Dialog open={removeOpen} onOpenChange={setRemoveOpen}>
        <DialogContent
          className="border-zinc-700 bg-zinc-950 text-zinc-200"
          aria-describedby={undefined}
        >
          <DialogTitle className="text-sm font-normal">列車を削除</DialogTitle>
          <p className="text-sm">
            選択した列車を削除しますか？運用に割り当て済みの列車は、先に運用から外してください。
          </p>
          <Button
            disabled={disabled}
            className="bg-red-400 text-white hover:bg-red-400"
            onClick={async () => {
              if (!removing || !(await confirmDiscard(["train"]))) return;
              setPending(true);
              try {
                const result = await deleteTrain(removing);
                if (result.error !== undefined) {
                  toast.add({ title: result.error, type: "error" });
                  return;
                }
                onCloseTrain();
                await onTrainsChanged();
                setSelected(null);
                setRemoveOpen(false);
                toast.add({ title: "削除しました", type: "success" });
              } finally {
                setPending(false);
              }
            }}
          >
            削除する
          </Button>
        </DialogContent>
      </Dialog>
      <Dialog open={copyOpen} onOpenChange={setCopyOpen}>
        <DialogContent
          aria-describedby={undefined}
          className="border-zinc-700 bg-zinc-950 text-zinc-200"
        >
          <DialogTitle className="text-sm font-normal">
            列車を複製して追加
          </DialogTitle>
          <Input
            aria-label="列番で検索"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          <ul className="max-h-64 overflow-auto">
            {candidates
              .filter((train) =>
                train.trainNumber
                  .toLowerCase()
                  .includes(query.trim().toLowerCase()),
              )
              .map((train) => (
                <li key={train.id}>
                  <Button
                    variant="ghost"
                    disabled={pending}
                    onClick={() => void copy(train.id)}
                    className="h-auto w-full justify-start whitespace-normal text-left text-zinc-200 hover:bg-zinc-800"
                  >
                    {train.trainNumber}　
                    {train.departureSeconds == null
                      ? ""
                      : formatTrainTime(train.departureSeconds)}
                    　{stationName(train.originStationId)} →{" "}
                    {stationName(train.destinationStationId)}　
                    {
                      timetables.find((t) => t.id === train.timetableId)
                        ?.displayName
                    }
                  </Button>
                </li>
              ))}
          </ul>
        </DialogContent>
      </Dialog>
    </section>
  );
}
