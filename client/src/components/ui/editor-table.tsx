"use client";

import type { ReactNode } from "react";
import { ArrowDown, ArrowUp, Minus, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export type EditorTableColumn<Row> = {
  id: string;
  header: ReactNode;
  cell: (row: Row) => ReactNode;
};

type Props<Row> = {
  rows: readonly Row[];
  columns: readonly EditorTableColumn<Row>[];
  getRowId: (row: Row) => string;
  selectedId: string | null;
  onSelectionChange: (id: string | null) => void;
  onAdd: () => void;
  onRemove: (id: string) => void;
  onMove?: (id: string, direction: -1 | 1) => void;
  label: string;
  addLabel?: string;
  removeLabel?: string;
  emptyMessage?: ReactNode;
  disabled?: boolean;
  className?: string;
  children?: ReactNode;
  actions?: ReactNode;
};

const controlClassName = "rounded-none bg-transparent text-zinc-300 hover:bg-transparent hover:text-zinc-300 dark:hover:bg-transparent";

export function EditorTable<Row>({
  rows, columns, getRowId, selectedId, onSelectionChange, onAdd, onRemove, onMove, label,
  addLabel = "項目を追加", removeLabel = "選択した項目を外す", emptyMessage = "項目がありません", disabled = false, className, children, actions,
}: Props<Row>) {
  const activeIndex = rows.findIndex((row) => getRowId(row) === selectedId);
  const activeId = activeIndex >= 0 ? selectedId : null;

  return (
    <div className={cn("overflow-hidden rounded-xs border border-zinc-800", className)}>
      <div className="h-56 overflow-y-auto bg-zinc-950">
        <table aria-label={label} className="w-full table-fixed border-collapse text-sm text-zinc-200">
          <thead className="sticky top-0 bg-zinc-800 text-xs text-zinc-300">
            <tr>
              {columns.map((column) => <th key={column.id} scope="col" className="border-b border-zinc-700 px-3 py-2 text-left font-normal">{column.header}</th>)}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => {
              const id = getRowId(row);
              return (
                <tr key={id} className={activeId === id ? "bg-sky-950 text-white" : "odd:bg-zinc-950 even:bg-zinc-900"}>
                  {columns.map((column) => (
                    <td key={column.id} className="p-0">
                      <button
                        type="button"
                        aria-pressed={activeId === id}
                        disabled={disabled}
                        onClick={() => onSelectionChange(id)}
                        className="block h-8 w-full truncate px-3 text-left outline-none focus-visible:outline-1 focus-visible:-outline-offset-2 focus-visible:outline-zinc-200 disabled:cursor-not-allowed"
                      >
                        {column.cell(row)}
                      </button>
                    </td>
                  ))}
                </tr>
              );
            })}
            {children}
            {rows.length === 0 && !children ? <tr><td colSpan={columns.length} className="h-40 px-3 text-center text-xs text-zinc-500">{emptyMessage}</td></tr> : null}
          </tbody>
        </table>
      </div>
      <div className="flex h-8 items-center border-t border-zinc-800 bg-zinc-900 px-1">
        <Button type="button" size="icon-xs" variant="ghost" aria-label={addLabel} disabled={disabled} onClick={onAdd} className={controlClassName}>
          <Plus aria-hidden="true" />
        </Button>
        <span aria-hidden="true" className="mx-1 h-4 w-px bg-zinc-700" />
        <Button
          type="button" size="icon-xs" variant="ghost" aria-label={removeLabel} disabled={disabled || activeId === null}
          onClick={() => {
            if (activeId === null) return;
            onRemove(activeId);
            onSelectionChange(null);
          }}
          className={controlClassName}
        >
          <Minus aria-hidden="true" />
        </Button>
        {actions}
        {onMove ? (
          <div className="ml-auto flex items-center gap-1">
            <Button type="button" size="icon-xs" variant="ghost" aria-label="選択した項目を上へ" disabled={disabled || activeIndex <= 0} onClick={() => { if (activeId !== null) onMove(activeId, -1); }} className={controlClassName}>
              <ArrowUp aria-hidden="true" />
            </Button>
            <Button type="button" size="icon-xs" variant="ghost" aria-label="選択した項目を下へ" disabled={disabled || activeIndex < 0 || activeIndex === rows.length - 1} onClick={() => { if (activeId !== null) onMove(activeId, 1); }} className={controlClassName}>
              <ArrowDown aria-hidden="true" />
            </Button>
          </div>
        ) : null}
      </div>
    </div>
  );
}
