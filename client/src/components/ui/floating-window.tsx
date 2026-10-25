"use client";

import { useId, useState, type ReactNode } from "react";
import { createPortal } from "react-dom";
import { Rnd } from "react-rnd";
import { X } from "lucide-react";
import { Button } from "@/components/ui/button";

type Props = { title: string; onClose: () => void; children: ReactNode };

export function FloatingWindow({ title, onClose, children }: Props) {
  const titleId = useId();
  const [initial] = useState(() => ({
    x: 16, y: 80,
    width: Math.min(720, window.innerWidth - 32),
    height: Math.min(600, window.innerHeight - 96),
  }));

  return createPortal(
    <div className="pointer-events-none fixed inset-0 z-40">
      <Rnd
        default={initial}
        bounds="parent"
        minWidth={Math.min(300, initial.width)}
        minHeight={Math.min(180, initial.height)}
        maxWidth="100%"
        maxHeight="100%"
        dragHandleClassName="floating-window-title"
        cancel="button, input, select, textarea"
        style={{ pointerEvents: "auto", display: "flex", flexDirection: "column" }}
        className="rounded-xs border border-zinc-500 bg-zinc-800 font-sans text-zinc-100 shadow-xl"
        role="dialog"
        aria-modal={false}
        aria-labelledby={titleId}
      >
        <header className="floating-window-title flex h-8 shrink-0 cursor-move touch-none items-center justify-between gap-2 border-b border-zinc-600 bg-zinc-950 px-2 select-none">
          <h2 id={titleId} className="truncate text-sm">{title}</h2>
          <Button type="button" size="icon-xs" variant="ghost" aria-label="列番編集を閉じる" onClick={onClose} className="text-zinc-200 hover:bg-transparent hover:text-zinc-200"><X aria-hidden="true" /></Button>
        </header>
        <div className="min-h-0 flex-1 overflow-auto p-3">{children}</div>
      </Rnd>
    </div>, document.body,
  );
}
