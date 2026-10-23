"use client";

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

type Props = {
  stations: readonly { stationId: string; displayName: string }[];
  value: string;
  onValueChange: (stationId: string) => void;
};

export function RouteControlStationSelect({
  stations,
  value,
  onValueChange,
}: Props) {
  const items = stations.map((station) => ({
    value: station.stationId,
    label: station.displayName,
  }));

  return (
    <Select
      items={items}
      value={value}
      onValueChange={(stationId) => {
        if (stationId !== null) onValueChange(stationId);
      }}
    >
      <SelectTrigger
        aria-label="駅を選択"
        className="w-36 text-lg border-zinc-700 bg-zinc-900 text-zinc-200 hover:bg-zinc-800 focus-visible:border--400 focus-visible:ring-zinc-400/50 dark:bg-zinc-900 dark:hover:bg-zinc-800"
      >
        <SelectValue placeholder="駅を選択" />
      </SelectTrigger>
      <SelectContent
        align="start"
        alignItemWithTrigger={false}
        className="bg-zinc-900 text-zinc-200 ring-zinc-700"
      >
        {items.map((item) => (
          <SelectItem
            key={item.value}
            value={item.value}
            className="focus-visible:outline-zinc-400 data-highlighted:bg-orange-300 data-highlighted:text-zinc-950"
          >
            {item.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
