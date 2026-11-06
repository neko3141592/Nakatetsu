"use client";

import { createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { toast } from "@/components/ui/toast";

type Scope = "timetable" | "operation-plan" | "operation" | "train" | "train-type" | "pattern";
type Entry = { scope: Scope; dirty: boolean; busy: boolean };
type HistoryNavigation = EventTarget & { traverseTo: (key: string) => { finished: Promise<unknown> } };
type HistoryNavigationEvent = Event & { navigationType: string; destination: { sameDocument: boolean; key: string } };
type Guard = {
  register: (id: string, entry: Entry) => () => void;
  confirmDiscard: (scopes?: readonly Scope[]) => Promise<boolean>;
};

const UnsavedChangesContext = createContext<Guard | null>(null);

export function UnsavedChangesProvider({ children }: { children: ReactNode }) {
  const entries = useRef(new Map<string, Entry>());
  const pending = useRef<((discard: boolean) => void) | null>(null);
  const [open, setOpen] = useState(false);

  const register = useCallback((id: string, entry: Entry) => {
    entries.current.set(id, entry);
    return () => { entries.current.delete(id); };
  }, []);

  const confirmDiscard = useCallback((scopes?: readonly Scope[]) => {
    if (pending.current) return Promise.resolve(false);
    const changed = [...entries.current.values()].filter((entry) => entry.dirty && (!scopes || scopes.includes(entry.scope)));
    if (changed.length === 0) return Promise.resolve(true);
    if (changed.some((entry) => entry.busy)) {
      toast.add({ title: "処理が完了してから操作してください", type: "info" });
      return Promise.resolve(false);
    }
    setOpen(true);
    return new Promise<boolean>((resolve) => { pending.current = resolve; });
  }, []);

  function finish(discard: boolean) {
    const resolve = pending.current;
    pending.current = null;
    setOpen(false);
    resolve?.(discard);
  }

  useEffect(() => {
    const navigation = (window as Window & { navigation?: HistoryNavigation }).navigation;
    let allowedKey: string | null = null;
    function navigate(event: Event) {
      const next = event as HistoryNavigationEvent;
      if (next.navigationType !== "traverse" || !next.destination.sameDocument || !next.cancelable) return;
      if (next.destination.key === allowedKey) { allowedKey = null; return; }
      if (![...entries.current.values()].some((entry) => entry.dirty)) return;
      next.preventDefault();
      void confirmDiscard().then((discard) => {
        if (!discard) return;
        allowedKey = next.destination.key;
        void navigation?.traverseTo(allowedKey).finished.catch(() => { allowedKey = null; });
      });
    }
    function beforeUnload(event: BeforeUnloadEvent) {
      if (![...entries.current.values()].some((entry) => entry.dirty)) return;
      event.preventDefault();
      event.returnValue = "";
    }
    navigation?.addEventListener("navigate", navigate);
    window.addEventListener("beforeunload", beforeUnload);
    return () => {
      navigation?.removeEventListener("navigate", navigate);
      window.removeEventListener("beforeunload", beforeUnload);
      pending.current?.(false);
    };
  }, [confirmDiscard]);

  const guard = useMemo(() => ({ register, confirmDiscard }), [register, confirmDiscard]);
  return <UnsavedChangesContext.Provider value={guard}>
    {children}
    <Dialog open={open} onOpenChange={(next) => { if (!next) finish(false); }}>
      <DialogContent showCloseButton={false} className="border border-zinc-700 bg-zinc-950 text-zinc-200 shadow-none ring-0">
        <DialogTitle className="text-sm font-normal">未保存の変更があります</DialogTitle>
        <DialogDescription className="text-sm text-zinc-200">変更を破棄して続けますか？</DialogDescription>
        <div className="flex justify-end gap-2">
          <Button type="button" size="sm" onClick={() => finish(false)} className="bg-zinc-800 text-zinc-200 hover:bg-zinc-800">編集を続ける</Button>
          <Button type="button" size="sm" onClick={() => finish(true)} className="bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400">破棄する</Button>
        </div>
      </DialogContent>
    </Dialog>
  </UnsavedChangesContext.Provider>;
}

export function useDiscardChanges() {
  const guard = useContext(UnsavedChangesContext);
  if (!guard) throw new Error("UnsavedChangesProviderが必要です");
  return guard;
}

export function useUnsavedChanges<T>(scope: Scope, value: T, busy = false) {
  const id = useId();
  const { register } = useDiscardChanges();
  const snapshot = JSON.stringify(value);
  const [saved, setSaved] = useState(snapshot);
  const dirty = snapshot !== saved;
  const markSaved = useCallback((next: T) => setSaved(JSON.stringify(next)), []);
  useEffect(() => register(id, { scope, dirty, busy }), [register, id, scope, dirty, busy]);
  return { dirty, markSaved };
}
