"use client";
import { formatTrainTime } from "@/lib/trainTimetable";
import { useEffect, useState } from "react";
import {
  deleteOperation,
  deleteOperationPlan,
  loadOperationPlan,
  loadOperationPlans,
  saveOperation,
  saveOperationPlan,
} from "@/app/timetables/operation-actions";
import { DeleteConfirmation } from "@/components/timetable/DeleteConfirmation";
import {
  useDiscardChanges,
  useUnsavedChanges,
} from "@/components/timetable/UnsavedChanges";
import { toast } from "@/components/ui/toast";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { EditorTable } from "@/components/ui/editor-table";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type {
  OperationPlan,
  OperationPlanSummary,
  TimetableOperation,
} from "@/types/operation";
import type { TimetableTrain } from "@/types/train";
import type { TimetableDiagramStation } from "@/types/timetableDiagram";

const operationDraft = (operation: TimetableOperation) => ({
  operationNumber: operation.operationNumber,
  trains: operation.trains.map(({ trainId, sequenceIndex }) => ({
    trainId,
    sequenceIndex,
  })),
});
const blankOperation = (plan: OperationPlan): TimetableOperation => ({
  id: "",
  operationPlanId: plan.id,
  timetableId: plan.timetableId,
  operationNumber: "",
  trains: [],
});
function Choice({
  label,
  value,
  items,
  onChange,
  disabled,
}: {
  label: string;
  value: string;
  items: { value: string; label: string }[];
  onChange: (id: string) => void;
  disabled?: boolean;
}) {
  return (
    <Select
      items={items}
      value={value}
      onValueChange={(id) => onChange(id ?? "")}
      disabled={disabled}
    >
      <SelectTrigger
        aria-label={label}
        size="sm"
        className="w-full bg-zinc-900 text-zinc-200 dark:bg-zinc-900"
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        {items.map((item) => (
          <SelectItem key={item.value} value={item.value}>
            {item.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
export function OperationInput({
  timetableId,
  trains,
  stations,
  onEditTrain,
  onCloseTrain,
  onTrainsChange,
}: {
  timetableId: string;
  trains: TimetableTrain[];
  stations: readonly TimetableDiagramStation[];
  onEditTrain: (id: string | null) => Promise<boolean>;
  onCloseTrain: () => void;
  onTrainsChange: (trains: TimetableTrain[]) => void;
}) {
  const { confirmDiscard } = useDiscardChanges();
  const [plans, setPlans] = useState<OperationPlanSummary[]>([]);
  const [plan, setPlan] = useState<OperationPlan>({
    id: "",
    timetableId,
    displayName: "新規運用計画",
    operations: [],
  });
  const [operation, setOperation] = useState<TimetableOperation>(() =>
    blankOperation(plan),
  );
  const [busy, setBusy] = useState(false);
  const [selected, setSelected] = useState<string | null>(null);
  const [dialog, setDialog] = useState(false);
  const [query, setQuery] = useState("");
  const planDraft = { id: plan.id, displayName: plan.displayName };
  const { markSaved: markPlanSaved } = useUnsavedChanges(
    "operation-plan",
    planDraft,
    busy,
  );
  const { markSaved: markOperationSaved } = useUnsavedChanges(
    "operation",
    operationDraft(operation),
    busy,
  );
  useEffect(() => {
    let active = true;
    void loadOperationPlans(timetableId).then((result) => {
      if (!active) return;
      if (result.error !== undefined)
        toast.add({ title: result.error, type: "error" });
      else setPlans(result.data);
    });
    return () => {
      active = false;
    };
  }, [timetableId]);
  useEffect(() => {
    const ids = new Set(operation.trains.map((train) => train.trainId));
    onTrainsChange(trains.filter((train) => ids.has(train.id)));
  }, [operation.trains, trains, onTrainsChange]);
  function replaceOperation(next: TimetableOperation) {
    setOperation(next);
    markOperationSaved(operationDraft(next));
    setSelected(null);
  }
  function replacePlan(next: OperationPlan) {
    setPlan(next);
    markPlanSaved({ id: next.id, displayName: next.displayName });
    replaceOperation(blankOperation(next));
  }
  async function selectPlan(id: string) {
    if (!(await confirmDiscard(["operation-plan", "operation", "train"])))
      return;
    onCloseTrain();
    if (!id) {
      replacePlan({
        id: "",
        timetableId,
        displayName: "新規運用計画",
        operations: [],
      });
      return;
    }
    setBusy(true);
    try {
      const result = await loadOperationPlan(id);
      if (result.error !== undefined)
        toast.add({ title: result.error, type: "error" });
      else replacePlan(result.data);
    } finally {
      setBusy(false);
    }
  }
  async function savePlan() {
    setBusy(true);
    try {
      const result = await saveOperationPlan(plan);
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      setPlan(result.data);
      markPlanSaved({
        id: result.data.id,
        displayName: result.data.displayName,
      });
      setOperation((current) => ({
        ...current,
        operationPlanId: result.data.id,
      }));
      setPlans((current) => [
        ...current.filter((p) => p.id !== result.data.id),
        result.data,
      ]);
      toast.add({ title: "保存しました", type: "success" });
    } finally {
      setBusy(false);
    }
  }
  async function selectOperation(id: string) {
    if (!(await confirmDiscard(["operation", "train"]))) return;
    onCloseTrain();
    replaceOperation(
      plan.operations.find((o) => o.id === id) ?? blankOperation(plan),
    );
  }
  async function save() {
    setBusy(true);
    try {
      const result = await saveOperation(operation);
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      replaceOperation(result.data);
      setPlan((current) => ({
        ...current,
        operations: [
          ...current.operations.filter((o) => o.id !== result.data.id),
          result.data,
        ],
      }));
      toast.add({ title: "保存しました", type: "success" });
    } finally {
      setBusy(false);
    }
  }
  function updateTrains(ids: string[]) {
    setOperation((current) => ({
      ...current,
      trains: ids.map((trainId, sequenceIndex) => ({
        operationId: current.id,
        operationPlanId: plan.id,
        timetableId,
        trainId,
        sequenceIndex,
        train: trains.find((train) => train.id === trainId)!,
      })),
    }));
  }
  function move(id: string, direction: -1 | 1) {
    const ids = operation.trains.map((train) => train.trainId);
    const i = ids.indexOf(id);
    if (i < 0 || i + direction < 0 || i + direction >= ids.length) return;
    [ids[i], ids[i + direction]] = [ids[i + direction], ids[i]];
    updateTrains(ids);
  }
  const assigned = new Set(
    plan.operations
      .filter((o) => o.id !== operation.id)
      .flatMap((o) => o.trains.map((train) => train.trainId)),
  );
  const candidates = trains.filter(
    (train) =>
      !assigned.has(train.id) &&
      !operation.trains.some((item) => item.trainId === train.id) &&
      train.trainNumber.toLowerCase().includes(query.trim().toLowerCase()),
  );
  const stationName = (id: string) =>
    stations.find((station) => station.stationId === id)?.displayName ?? id;
  return (
    <section
      aria-label="運用計画の編集"
      className="min-h-0 flex-1 space-y-3 overflow-auto bg-zinc-950 p-3 text-zinc-200"
    >
      <label className="flex flex-col gap-1.5 text-xs">
        運用計画
        <Choice
          label="運用計画を選択"
          value={plan.id}
          items={[
            { value: "", label: "新規運用計画" },
            ...plans.map((p) => ({ value: p.id, label: p.displayName })),
          ]}
          onChange={(id) => void selectPlan(id)}
          disabled={busy}
        />
      </label>
      <label className="flex flex-col gap-1.5 text-xs">
        計画名
        <Input
          value={plan.displayName}
          onChange={(e) => setPlan({ ...plan, displayName: e.target.value })}
          disabled={busy}
        />
      </label>
      <Button
        size="sm"
        disabled={busy || !plan.displayName.trim()}
        onClick={savePlan}
        className="w-full bg-zinc-800 text-zinc-200 hover:bg-zinc-800"
      >
        運用計画を保存
      </Button>
      <div className="space-y-3 border-t border-zinc-800 pt-3">
        <Choice
          label="運用を選択"
          value={operation.id}
          items={[
            { value: "", label: "新規運用" },
            ...plan.operations.map((o) => ({
              value: o.id,
              label: o.operationNumber,
            })),
          ]}
          onChange={(id) => void selectOperation(id)}
          disabled={busy || !plan.id}
        />
        <label className="flex flex-col gap-1.5 text-xs">
          運用番号
          <Input
            value={operation.operationNumber}
            onChange={(e) =>
              setOperation({ ...operation, operationNumber: e.target.value })
            }
            disabled={busy || !plan.id}
          />
        </label>
        <EditorTable
          rows={operation.trains}
          columns={[
            {
              id: "number",
              header: "列番",
              cell: (row) =>
                trains.find((train) => train.id === row.trainId)?.trainNumber ??
                "",
            },
            {
              id: "destination",
              header: "行先",
              cell: (row) =>
                stationName(
                  trains.find((train) => train.id === row.trainId)
                    ?.destinationStationId ?? "",
                ),
            },
          ]}
          getRowId={(train) => train.trainId}
          selectedId={selected}
          onSelectionChange={(id) => {
            setSelected(id);
            if (id) void onEditTrain(id);
          }}
          onAdd={() => {
            setQuery("");
            setDialog(true);
          }}
          onRemove={(id) =>
            updateTrains(
              operation.trains
                .filter((train) => train.trainId !== id)
                .map((train) => train.trainId),
            )
          }
          onMove={move}
          disabled={busy || !plan.id}
          label="運用に所属する列車"
          addLabel="対象ダイヤの列車を追加"
          emptyMessage="列車が追加されていません"
        />
        <Button
          size="sm"
          disabled={busy || !plan.id || !operation.operationNumber.trim()}
          onClick={save}
          className="w-full bg-zinc-800 text-zinc-200 hover:bg-zinc-800"
        >
          運用を保存
        </Button>
        <DeleteConfirmation
          label="運用を削除"
          description={`運用「${operation.operationNumber}」を削除しますか？列車は残ります。`}
          disabled={busy || !operation.id}
          onDelete={async () => {
            if (!(await confirmDiscard(["operation", "train"]))) return false;
            const result = await deleteOperation(operation.id);
            if (result.error !== undefined) {
              toast.add({ title: result.error, type: "error" });
              return false;
            }
            setPlan((p) => ({
              ...p,
              operations: p.operations.filter((o) => o.id !== operation.id),
            }));
            replaceOperation(blankOperation(plan));
            onCloseTrain();
            toast.add({ title: "削除しました", type: "success" });
            return true;
          }}
        />
      </div>
      <DeleteConfirmation
        label="運用計画を削除"
        description={`「${plan.displayName}」の運用計画を削除しますか？ダイヤと列車は残ります。`}
        disabled={busy || !plan.id}
        onDelete={async () => {
          if (!(await confirmDiscard(["operation-plan", "operation", "train"])))
            return false;
          const result = await deleteOperationPlan(plan.id);
          if (result.error !== undefined) {
            toast.add({ title: result.error, type: "error" });
            return false;
          }
          setPlans((current) => current.filter((p) => p.id !== plan.id));
          replacePlan({
            id: "",
            timetableId,
            displayName: "新規運用計画",
            operations: [],
          });
          onCloseTrain();
          toast.add({ title: "削除しました", type: "success" });
          return true;
        }}
      />
      <Dialog open={dialog} onOpenChange={setDialog}>
        <DialogContent
          aria-describedby={undefined}
          className="border-zinc-700 bg-zinc-950 text-zinc-200"
        >
          <DialogTitle className="text-sm font-normal">列車を追加</DialogTitle>
          <Input
            aria-label="列番で検索"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          <ul className="max-h-64 overflow-auto">
            {candidates.map((train) => (
              <li key={train.id}>
                <Button
                  variant="ghost"
                  onClick={() => {
                    updateTrains([
                      ...operation.trains.map((t) => t.trainId),
                      train.id,
                    ]);
                    setDialog(false);
                  }}
                  className="h-auto w-full justify-start whitespace-normal text-left text-zinc-200 hover:bg-zinc-800"
                >
                  {train.trainNumber}　
                  {formatTrainTime(
                    train.points?.find((point) => point.section === "service")
                      ?.departureSeconds ?? null,
                  )}
                  　{stationName(train.originStationId)} →{" "}
                  {stationName(train.destinationStationId)}
                </Button>
              </li>
            ))}
          </ul>
          {candidates.length === 0 ? (
            <p className="text-xs text-zinc-400">追加できる列車がありません</p>
          ) : null}
        </DialogContent>
      </Dialog>
    </section>
  );
}
