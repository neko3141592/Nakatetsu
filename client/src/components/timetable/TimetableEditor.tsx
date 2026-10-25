"use client";

import Link from "next/link";
import { useState } from "react";
import { toast } from "@/components/ui/toast";

import { deleteTimetable, loadTimetable, saveTimetable } from "@/app/timetables/actions";
import { DiagramViewer } from "@/components/timetable/DiagramViewer";
import { OperationInput } from "@/components/timetable/OperationInput";
import { TimetableInput } from "@/components/timetable/TimetableInput";
import { TrainTypeInput } from "@/components/timetable/TrainTypeInput";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { TimetableDiagramStation } from "@/types/timetableDiagram";
import type { Timetable, TimetableSummary } from "@/types/timetable";
import type { TimetableOperationSummary } from "@/types/operation";

const editorTabs = [
  { value: "timetables", label: "ダイヤ編集" },
  { value: "operations", label: "列番・運用編集" },
  { value: "train-types", label: "列車種別" },
];

const tabClassName =
  "h-8 min-w-0 flex-1 rounded-none border-0 border-t-[5px] border-transparent bg-zinc-900 px-2 py-0 text-xs font-normal text-zinc-500 shadow-none transition-none data-active:border-orange-300 data-active:bg-zinc-950 data-active:text-zinc-200 group-data-[variant=default]/tabs-list:data-active:shadow-none sm:text-base";

type Props = {
  stations: TimetableDiagramStation[];
  timetables: TimetableSummary[];
  operations: TimetableOperationSummary[];
};

function createNewTimetable(): Timetable {
  return {
    id: "",
    displayName: "新規ダイヤ",
    revision: 0,
    trains: [],
    temporaryTrainNumbers: [],
    operations: [],
  };
}

export function TimetableEditor({ stations, timetables, operations }: Props) {
  const [timetable, setTimetable] = useState<Timetable>(createNewTimetable);
  const [timetableList, setTimetableList] = useState(timetables);
  const [operationList, setOperationList] = useState(operations);
  const [selectedTimetableId, setSelectedTimetableId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function selectTimetable(id: string | null) {
    if (id === null) {
      setSelectedTimetableId(null);
      setTimetable(createNewTimetable());
      return;
    }
    setBusy(true);
    try {
      const result = await loadTimetable(id);
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      setTimetable(result.data);
      setSelectedTimetableId(id);
    } catch {
      toast.add({ title: "ダイヤの取得に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  async function save() {
    setBusy(true);
    try {
      const result = await saveTimetable({
        id: timetable.id,
        displayName: timetable.displayName,
        revision: timetable.revision,
        operationNumbers: timetable.operations.map((operation) => operation.operationNumber),
      });
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      const saved = result.data;
      setTimetable(saved);
      setSelectedTimetableId(saved.id);
      const summary = { id: saved.id, displayName: saved.displayName, revision: saved.revision };
      setTimetableList((current) => [...current.filter((item) => item.id !== saved.id), summary].sort((a, b) => a.id.localeCompare(b.id)));
      toast.add({ title: "保存しました", type: "success" });
    } catch {
      toast.add({ title: "ダイヤの保存に失敗しました", type: "error" });
    } finally {
      setBusy(false);
    }
  }

  async function remove(): Promise<string | null> {
    if (!timetable.id) return "保存済みのダイヤを選択してください";
    setBusy(true);
    try {
      const result = await deleteTimetable(timetable.id);
      if (result.error !== undefined) return result.error;
      setTimetableList((current) => current.filter((item) => item.id !== timetable.id));
      setTimetable(createNewTimetable());
      setSelectedTimetableId(null);
      toast.add({ title: "削除しました", type: "success" });
      return null;
    } catch {
      return "ダイヤの削除に失敗しました";
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="flex h-dvh min-h-0 w-full flex-col overflow-hidden bg-zinc-900 font-sans">
      <h1 className="sr-only">ダイヤ編集</h1>
      <Tabs defaultValue="timetables" className="flex min-h-0 flex-1 flex-col gap-0">
        <header className="flex h-8 shrink-0 items-center justify-between gap-3 bg-zinc-900">
          <TabsList
            aria-label="ダイヤ編集項目"
            className="h-8 w-176 min-w-0 max-w-[62%] justify-start gap-0 rounded-none bg-transparent p-0"
          >
            {editorTabs.map((tab) => (
              <TabsTrigger key={tab.value} value={tab.value} className={tabClassName}>
                {tab.label}
              </TabsTrigger>
            ))}
          </TabsList>
          <Link
            href="/"
            className="mr-3 inline-flex h-6 shrink-0 items-center rounded-xs bg-zinc-800 px-2 text-xs text-zinc-200 hover:bg-zinc-700 focus-visible:outline-2 focus-visible:outline-orange-300"
          >
            運行管理画面
          </Link>
        </header>
        {editorTabs.map((tab) => (
          <TabsContent
            key={tab.value}
            value={tab.value}
            aria-label={`${tab.label}編集ワークスペース`}
            className="relative m-0 min-h-0 flex-1 overflow-auto bg-zinc-950"
          >
            {tab.value === "timetables" || tab.value === "operations" ? (
              <ResizablePanelGroup orientation="horizontal" className="min-h-0" id={`${tab.value}-workspace`}>
                <ResizablePanel id={`${tab.value}-diagram`} defaultSize="70%" minSize="30%" className="min-w-0">
                  <DiagramViewer
                    trains={tab.value === "timetables" ? timetable.trains : []}
                    stations={stations}
                    startSeconds={4 * 3600}
                    endSeconds={21 * 3600}
                    defaultPixelsPerMinute={16}
                  />
                </ResizablePanel>
                <ResizableHandle withHandle aria-label={`${tab.label}の表示と入力の幅を変更`} className="w-1 bg-zinc-300" />
                <ResizablePanel id={`${tab.value}-input`} defaultSize="30%" minSize="20%" className="min-w-0">
                  {tab.value === "timetables" ? <TimetableInput
                    timetables={timetableList}
                    operations={operationList}
                    selectedOperationNumbers={timetable.operations.map((operation) => operation.operationNumber)}
                    onAddOperation={(operationNumber) => {
                      setTimetable((current) => current.operations.some((operation) => operation.operationNumber === operationNumber) ? current : {
                        ...current,
                        operations: [...current.operations, { operationNumber, trains: null }],
                      });
                      toast.add({ title: "運用を追加しました。保存すると反映されます", type: "info" });
                    }}
                    onRemoveOperation={(operationNumber) => {
                      setTimetable((current) => ({ ...current, operations: current.operations.filter((operation) => operation.operationNumber !== operationNumber) }));
                      toast.add({ title: "運用を外しました。保存すると反映されます", type: "info" });
                    }}
                    selectedTimetableId={selectedTimetableId}
                    onSelectTimetable={selectTimetable}
                    displayName={timetable.displayName}
                    onDisplayNameChange={(displayName) => {
                      setTimetable((current) => ({ ...current, displayName }));
                    }}
                    onSave={save}
                    onDelete={remove}
                    busy={busy}
                  /> : <OperationInput operations={operationList} onSaved={(saved) => {
                    setOperationList((current) => [...current.filter((item) => item.operationNumber !== saved.operationNumber), saved].sort((a, b) => a.operationNumber.localeCompare(b.operationNumber)));
                  }} onDeleted={(number) => {
                    setOperationList((current) => current.filter((item) => item.operationNumber !== number));
                    setTimetable((current) => ({ ...current, operations: current.operations.filter((operation) => operation.operationNumber !== number) }));
                  }} />}
                </ResizablePanel>
              </ResizablePanelGroup>
            ) : <TrainTypeInput />}
          </TabsContent>
        ))}
      </Tabs>
    </main>
  );
}
