"use client";

import { Toast } from "@base-ui/react/toast";
import { CheckCircle2, CircleAlert, Info, LoaderCircle, X } from "lucide-react";
import { Button } from "@/components/ui/button";

export const toast = Toast.createToastManager();

function ToastList() {
  const { toasts } = Toast.useToastManager();
  return <Toast.Portal>
    <Toast.Viewport aria-label="通知" className="pointer-events-none fixed right-4 bottom-4 z-[100] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2 font-sans">
      {toasts.map((item) => <Toast.Root key={item.id} toast={item} className="pointer-events-auto flex items-start gap-2 rounded-xs border border-zinc-700 bg-zinc-900 p-3 text-sm text-zinc-100 shadow-lg data-limited:hidden data-ending:opacity-0">
        {item.type === "success" ? <CheckCircle2 className="size-4 shrink-0 text-green-400" /> : item.type === "error" ? <CircleAlert className="size-4 shrink-0 text-red-400" /> : item.type === "loading" ? <LoaderCircle className="size-4 shrink-0 animate-spin text-zinc-300" /> : <Info className="size-4 shrink-0 text-zinc-300" />}
        <Toast.Content className="min-w-0 flex-1"><Toast.Title className="break-words" /></Toast.Content>
        <Toast.Close aria-label="通知を閉じる" render={<Button type="button" size="icon-xs" variant="ghost" className="-my-1 -mr-1 text-zinc-300 hover:bg-transparent hover:text-white" />}><X aria-hidden="true" /></Toast.Close>
      </Toast.Root>)}
    </Toast.Viewport>
  </Toast.Portal>;
}

export function Toaster() {
  return <Toast.Provider toastManager={toast} timeout={5000} limit={3}><ToastList /></Toast.Provider>;
}
