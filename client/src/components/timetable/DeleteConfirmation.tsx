"use client";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
export function DeleteConfirmation({
  label,
  description,
  disabled,
  onDelete,
}: {
  label: string;
  description: string;
  disabled?: boolean;
  onDelete: () => Promise<boolean>;
}) {
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        if (!busy) setOpen(value);
      }}
    >
      <DialogTrigger
        disabled={disabled}
        render={
          <Button
            size="sm"
            className="w-full bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400"
          />
        }
      >
        {label}
      </DialogTrigger>
      <DialogContent
        showCloseButton={false}
        className="border-zinc-700 bg-zinc-950 text-zinc-200 shadow-none ring-0"
      >
        <DialogTitle className="text-sm font-normal">{label}</DialogTitle>
        <DialogDescription className="text-zinc-300">
          {description}
        </DialogDescription>
        <div className="flex justify-end gap-2">
          <DialogClose
            disabled={busy}
            render={
              <Button
                size="sm"
                className="bg-zinc-800 text-zinc-200 hover:bg-zinc-800"
              />
            }
          >
            キャンセル
          </DialogClose>
          <Button
            size="sm"
            disabled={busy}
            onClick={async () => {
              setBusy(true);
              try {
                if (await onDelete()) setOpen(false);
              } finally {
                setBusy(false);
              }
            }}
            className="bg-red-400 text-white hover:bg-red-400 dark:bg-red-400 dark:hover:bg-red-400"
          >
            削除する
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
