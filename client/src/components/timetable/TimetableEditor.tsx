"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "@/components/ui/toast";

import { deleteTimetable, loadTimetable, saveTimetable } from "@/app/timetables/actions";
import { DiagramViewer } from "@/components/timetable/DiagramViewer";
import { OperationInput } from "@/components/timetable/OperationInput";
import { TimetableSelect } from "@/components/timetable/TimetableSelect";
import { TimetableInput } from "@/components/timetable/TimetableInput";
import { TrainTypeInput } from "@/components/timetable/TrainTypeInput";
import { TrainEditWindow } from "@/components/timetable/TrainEditWindow";
import { UnsavedChangesProvider, useDiscardChanges, useUnsavedChanges } from "@/components/timetable/UnsavedChanges";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import type { TimetableDiagramStation } from "@/types/timetableDiagram";
import type { Timetable, TimetableSummary } from "@/types/timetable";
import type { TimetableTrain } from "@/types/train";
import type { TrainType } from "@/types/masters";

const editorTabs = [
  { value: "timetables", label: "ダイヤ編集" },
  { value: "operations", label: "運用編集" },
  { value: "train-types", label: "列車種別" },
];

const tabClassName =
  "h-8 min-w-0 flex-1 rounded-none border-0 border-t-[5px] border-transparent bg-zinc-900 px-2 py-0 text-xs font-normal text-zinc-500 shadow-none transition-none data-active:border-orange-300 data-active:bg-zinc-950 data-active:text-zinc-200 group-data-[variant=default]/tabs-list:data-active:shadow-none sm:text-base";

type Props = {
  stations: TimetableDiagramStation[];
  timetables: TimetableSummary[];
};

function createNewTimetable(): Timetable {
  return {
    id: "",
    displayName: "新規ダイヤ",
    revision: 0,
    trains: [],
  };
}

export function TimetableEditor({ stations, timetables }: Props) {
  return <UnsavedChangesProvider><TimetableWorkspace stations={stations} timetables={timetables} /></UnsavedChangesProvider>;
}

function TimetableWorkspace({ stations, timetables }: Props) {
  const router = useRouter();
  const { confirmDiscard } = useDiscardChanges();
  const [activeTab, setActiveTab] = useState("timetables");
  const [timetable, setTimetable] = useState<Timetable>(createNewTimetable);
  const [savedTimetable, setSavedTimetable] = useState<Timetable>(createNewTimetable);
  const [timetableList, setTimetableList] = useState(timetables);
  const [operationTrains, setOperationTrains] = useState<TimetableTrain[]>([]);
  const [editingTrain, setEditingTrain] = useState<string | null>(null);
  const [selectedTimetableId, setSelectedTimetableId] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const { markSaved } = useUnsavedChanges("timetable", timetableDraft(timetable), busy);

  function replaceTimetable(next: Timetable) {
    setTimetable(next);
    setSavedTimetable(next);
    markSaved(timetableDraft(next));
  }

  function updateTrainType(type: TrainType) {
    const update = (trains: TimetableTrain[]) => trains.map((train) => train.trainTypeId === type.trainTypeId ? { ...train, trainType: type } : train);
    setTimetable((current) => ({ ...current, trains: update(current.trains) }));
    setSavedTimetable((current) => ({ ...current, trains: update(current.trains) }));
    setOperationTrains(update);
  }

  async function editTrain(number: string | null) {
    if (busy) return false;
    if (number === editingTrain) return true;
    if (number === "" && !timetable.id) { toast.add({title:"先にダイヤを保存してください",type:"info"}); return false; }
    if (!await confirmDiscard(["train"])) return false;
    setEditingTrain(number);
    return true;
  }

  async function refreshTimetableTrains() {
    if (!timetable.id) return;
    const result=await loadTimetable(timetable.id);
    if(result.error!==undefined){toast.add({title:result.error,type:"error"});return;}
    setTimetable((current)=>({...current,trains:result.data.trains}));
    setSavedTimetable((current)=>({...current,trains:result.data.trains}));
  }

  async function refreshTrain() { await refreshTimetableTrains(); }

  async function selectTimetable(id: string | null) {
    if (id === selectedTimetableId || !await confirmDiscard(["timetable", "train", "operation", "operation-plan"])) return;
    if (id === null) {
      setSelectedTimetableId(null);
      replaceTimetable(createNewTimetable());
      setEditingTrain(null);
      return;
    }
    setBusy(true);
    try {
      const result = await loadTimetable(id);
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      replaceTimetable(result.data);
      setEditingTrain(null);
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
      });
      if (result.error !== undefined) {
        toast.add({ title: result.error, type: "error" });
        return;
      }
      const saved = result.data;
      replaceTimetable(saved);
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
      replaceTimetable(createNewTimetable());
      setSelectedTimetableId(null);
      setEditingTrain(null);
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
      <Tabs value={activeTab} onValueChange={async (value) => {
        if (typeof value !== "string" || value === activeTab || !await confirmDiscard()) return;
        replaceTimetable(savedTimetable);
        setEditingTrain(null);
        setActiveTab(value);
      }} className="flex min-h-0 flex-1 flex-col gap-0">
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
            onNavigate={(event) => {
              event.preventDefault();
              void confirmDiscard().then((discard) => { if (discard) router.push("/"); });
            }}
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
                    trains={tab.value === "timetables" ? timetable.trains : operationTrains}
                    stations={stations}
                    startSeconds={4 * 3600}
                    endSeconds={21 * 3600}
                    defaultPixelsPerMinute={16}
                    onTrainClick={(number) => { void editTrain(number); }}
                  />
                </ResizablePanel>
                <ResizableHandle withHandle aria-label={`${tab.label}の表示と入力の幅を変更`} className="w-1 bg-zinc-300" />
                <ResizablePanel id={`${tab.value}-input`} defaultSize="30%" minSize="20%" className="min-w-0">
                  {tab.value === "timetables" ? <TimetableInput
                    timetables={timetableList} timetable={timetable} stations={stations}
                    onSelectTimetable={selectTimetable}
                    onDisplayNameChange={(displayName)=>setTimetable((current)=>({...current,displayName}))}
                    onSave={save} onDelete={async()=>{if(!await confirmDiscard(["timetable","train","operation","operation-plan"]))return false;const error=await remove();if(error)toast.add({title:error,type:"error"});return error===null;}}
                    onEditTrain={editTrain} onCloseTrain={()=>setEditingTrain(null)} onTrainsChanged={refreshTimetableTrains} busy={busy}
                  /> : <div className="flex h-full min-h-0 flex-col">
                    <header className="flex h-7 shrink-0 bg-black"><TimetableSelect timetables={timetableList} value={timetable.id} onChange={selectTimetable} disabled={busy}/></header>
                    {timetable.id ? <OperationInput key={timetable.id} timetableId={timetable.id} trains={timetable.trains} stations={stations} onTrainsChange={setOperationTrains} onEditTrain={editTrain} onCloseTrain={()=>setEditingTrain(null)}/> : <p className="p-3 text-xs text-zinc-400">対象のダイヤを選択してください</p>}
                  </div>}

                </ResizablePanel>
              </ResizablePanelGroup>
            ) : <TrainTypeInput stations={stations} onSaved={updateTrainType} />}
          </TabsContent>
        ))}
      </Tabs>
      {editingTrain !== null ? <TrainEditWindow key={editingTrain} trainId={editingTrain || undefined} timetableId={timetable.id} onSaved={refreshTrain} onClose={() => setEditingTrain(null)} /> : null}
    </main>
  );
}

function timetableDraft(timetable: Timetable) {
  return { displayName: timetable.displayName };
}
