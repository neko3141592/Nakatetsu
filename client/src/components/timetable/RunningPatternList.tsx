"use client";

import { useEffect, useMemo, useState } from "react";
import { deleteRunningPattern, loadRunningPattern, loadRunningPatterns } from "@/app/timetables/running-pattern-actions";
import { RunningPatternWindow } from "@/components/timetable/RunningPatternWindow";
import { useDiscardChanges } from "@/components/timetable/UnsavedChanges";
import { DiagramViewer } from "@/components/timetable/DiagramViewer";
import { EditorTable } from "@/components/ui/editor-table";
import { Button } from "@/components/ui/button";
import { Dialog, DialogClose, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";
import { ResizableHandle, ResizablePanel, ResizablePanelGroup } from "@/components/ui/resizable";
import { runningPatternToDiagram } from "@/lib/runningPattern";
import type { TrainTypeDetail } from "@/types/masters";
import type { RunningPattern, RunningPatternSummary } from "@/types/runningPattern";
import type { TimetableDiagramStation } from "@/types/timetableDiagram";

export function RunningPatternList({ trainType, stations }: { trainType: TrainTypeDetail; stations: readonly TimetableDiagramStation[] }) {
  const { confirmDiscard } = useDiscardChanges();
  const [patterns, setPatterns] = useState<RunningPatternSummary[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<RunningPatternSummary | null>(null);
  const [busy, setBusy] = useState(false);
  const [ready, setReady] = useState(false);
  const [selectedPattern, setSelectedPattern] = useState<RunningPattern | null>(null);
  const trainTypeId = trainType.trainTypeId;
  const diagramTrains = useMemo(() => selectedPattern ? [runningPatternToDiagram(selectedPattern, trainType.color)] : [], [selectedPattern, trainType.color]);
  const endSeconds = Math.max(30 * 60, Math.ceil(Math.max(0, ...diagramTrains.flatMap((train) => (train.points ?? []).flatMap((point) => [point.arrivalSeconds ?? 0, point.departureSeconds ?? 0]))) / 600) * 600);

  useEffect(() => {
    let active = true;
    if (!trainTypeId) return;
    void loadRunningPatterns(trainTypeId).then((result) => {
      if (!active) return;
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else { setPatterns(result.data); setReady(true); }
    }).catch(() => { if (active) toast.add({ title: "走行パターン一覧の取得に失敗しました", type: "error" }); });
    return () => { active = false; };
  }, [trainTypeId]);

  useEffect(() => {
    let active = true;
    if (!selectedId) return;
    void loadRunningPattern(selectedId).then((result) => {
      if (!active) return;
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else setSelectedPattern(result.data);
    }).catch(() => { if (active) toast.add({ title: "走行パターンの取得に失敗しました", type: "error" }); });
    return () => { active = false; };
  }, [selectedId]);

  async function remove() {
    if (!deleting || busy) return;
    if (editingId !== null && !await confirmDiscard(["pattern"])) return;
    setBusy(true);
    try {
      const result = await deleteRunningPattern(deleting.id);
      if (result.error !== undefined) toast.add({ title: result.error, type: "error" });
      else {
        setPatterns((current) => current.filter((pattern) => pattern.id !== deleting.id));
        setEditingId(null);
        if (selectedId === deleting.id) { setSelectedId(null); setSelectedPattern(null); }
        setDeleting(null);
        toast.add({ title: "削除しました", type: "success" });
      }
    } catch { toast.add({ title: "走行パターンの削除に失敗しました", type: "error" }); }
    finally { setBusy(false); }
  }

  async function edit(id: string | null) {
    if (id === null || id === editingId || !await confirmDiscard(["pattern"])) return;
    if (id && id !== selectedId) setSelectedPattern(null);
    if (id) setSelectedId(id);
    setEditingId(id);
  }

  return <>
    <ResizablePanelGroup orientation="vertical" id="running-pattern-workspace" className="min-h-0">
    <ResizablePanel id="running-pattern-list" defaultSize="40%" minSize="20%" className="min-h-0">
    <section aria-label="種別の走行パターン" className="flex h-full min-h-0 min-w-0 flex-col gap-3 p-3 text-zinc-200">
    <h2 className="shrink-0 text-xs">走行パターン</h2>
    <EditorTable rows={patterns} columns={[{ id: "name", header: "名称", cell: (pattern) => pattern.displayName }]} getRowId={(pattern) => pattern.id} selectedId={selectedId}
      onSelectionChange={(id) => { if (id !== null) void edit(id); }} onAdd={() => void edit("")}
      onRemove={(id) => setDeleting(patterns.find((pattern) => pattern.id === id) ?? null)}
      label="走行パターン一覧" addLabel="走行パターンを追加" removeLabel="走行パターンを削除" disabled={busy || !ready} fillHeight
      emptyMessage={!trainTypeId ? "保存済みの列車種別を選択してください" : ready ? "走行パターンがありません" : "読み込み中…"} />
    </section>
    </ResizablePanel>
    <ResizableHandle withHandle aria-label="走行パターン一覧とスジ表示の高さを変更" className="h-1 bg-zinc-700" />
    <ResizablePanel id="running-pattern-diagram" defaultSize="60%" minSize="20%" className="min-h-0">
      <section aria-label="走行パターンのスジ" className="h-full min-h-0">
        <DiagramViewer trains={diagramTrains} stations={stations} startSeconds={0} endSeconds={endSeconds} defaultPixelsPerMinute={16} />
      </section>
    </ResizablePanel>
    </ResizablePanelGroup>
    {editingId !== null ? <RunningPatternWindow key={editingId} patternId={editingId || undefined} trainType={trainType} onClose={() => setEditingId(null)} onSaved={(saved) => {
      setPatterns((current) => [...current.filter((pattern) => pattern.id !== saved.id), saved].sort((a, b) => a.displayName.localeCompare(b.displayName)));
      setSelectedId(saved.id);
      setSelectedPattern(saved);
    }} /> : null}
    <Dialog open={deleting !== null} onOpenChange={(open) => { if (!open && !busy) setDeleting(null); }}>
      <DialogContent showCloseButton={false} className="border border-zinc-700 bg-zinc-950 text-zinc-200 shadow-none ring-0">
        <DialogTitle className="text-sm font-normal">走行パターンを削除</DialogTitle>
        <DialogDescription className="text-sm text-zinc-200">「{deleting?.displayName}」を削除しますか？</DialogDescription>
        <div className="flex justify-end gap-2">
          <DialogClose disabled={busy} render={<Button type="button" size="sm" className="bg-zinc-800 text-zinc-200 hover:bg-zinc-800" />}>キャンセル</DialogClose>
          <Button type="button" size="sm" disabled={busy} onClick={remove} className="bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400">削除する</Button>
        </div>
      </DialogContent>
    </Dialog>
  </>;
}
