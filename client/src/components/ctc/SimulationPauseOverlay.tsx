"use client";

import { CirclePause } from "lucide-react";
import { useEffect, useRef } from "react";

export function SimulationPauseOverlay() {
  const dialogRef = useRef<HTMLDialogElement>(null);

  useEffect(() => {
    const dialog = dialogRef.current;
    function preventDismiss(event: Event) {
      event.preventDefault();
    }
    dialog?.addEventListener("cancel", preventDismiss);
    dialog?.showModal();
    return () => {
      dialog?.removeEventListener("cancel", preventDismiss);
      dialog?.close();
    };
  }, []);

  return (
    <dialog
      ref={dialogRef}
      aria-label="シミュレーション一時停止中"
      onKeyDown={(event) => {
        event.stopPropagation();
        if (event.key === "Escape") event.preventDefault();
      }}
      className="fixed inset-0 m-0 h-dvh max-h-none w-screen max-w-none items-center justify-center border-0 bg-black/60 p-0 text-zinc-200 outline-none backdrop:bg-transparent open:flex"
    >
      <CirclePause aria-hidden="true" className="size-24" strokeWidth={1.5} />
    </dialog>
  );
}
