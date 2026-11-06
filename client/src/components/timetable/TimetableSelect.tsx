"use client";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import type { TimetableSummary } from "@/types/timetable";
export function TimetableSelect({
  timetables,
  value,
  onChange,
  disabled,
}: {
  timetables: readonly TimetableSummary[];
  value: string;
  onChange: (id: string | null) => void;
  disabled?: boolean;
}) {
  const items = [
    { value: "", label: "新規ダイヤ" },
    ...timetables.map((t) => ({ value: t.id, label: t.displayName })),
  ];
  return (
    <Select
      items={items}
      value={value}
      onValueChange={(id) => onChange(id || null)}
      disabled={disabled}
    >
      <SelectTrigger
        aria-label="ダイヤを選択"
        className="min-w-0 rounded-none bg-black px-3 text-sm font-normal text-white data-[size=default]:h-7 dark:bg-black"
      >
        <SelectValue />
      </SelectTrigger>
      <SelectContent align="start" alignItemWithTrigger={false}>
        {items.map((item) => (
          <SelectItem key={item.value} value={item.value}>
            {item.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
