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
        className="w-60 max-w-full border-orange-400 bg-orange-300 px-4 text-2xl text-black hover:bg-orange-300 focus-visible:border-orange-400 focus-visible:ring-orange-400/50 data-[size=default]:h-12 dark:bg-orange-300 dark:hover:bg-orange-300 [&_svg]:text-black"
      >
        <SelectValue placeholder="駅を選択" className="justify-center text-center" />
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
