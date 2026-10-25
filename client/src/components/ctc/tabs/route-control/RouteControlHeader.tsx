import { RouteControlStationSelect } from "@/components/ctc/tabs/route-control/RouteControlStationSelect";
import { TurnoutDirectionIndicator } from "@/components/ctc/tabs/route-control/TurnoutDirectionIndicator";
import type { CtcSnapshot } from "@/gen/ctc_pb";

type Props = {
  stations: readonly { stationId: string; displayName: string }[];
  stationId: string;
  onStationChange: (stationId: string) => void;
  turnouts: readonly { turnoutId: string; label: string }[];
  turnoutsById?: CtcSnapshot["turnoutsById"];
  lockedTurnoutIds: ReadonlySet<string>;
};

export function RouteControlHeader({
  stations,
  stationId,
  onStationChange,
  turnouts,
  turnoutsById,
  lockedTurnoutIds,
}: Props) {
  const sortedTurnouts = [...turnouts].sort((a, b) =>
    a.label.localeCompare(b.label, undefined, { numeric: true }),
  );

  return (
    <header
      aria-label="進路制御ヘッダー"
      className="shrink-0 border-b border-white border-zinc-800 bg-zinc-950 px-4 py-3"
    >
      <div className="flex items-center justify-center">
        <RouteControlStationSelect
          stations={stations}
          value={stationId}
          onValueChange={onStationChange}
        />
      </div>

      <div
        role="group"
        aria-label="転轍機状態"
        className="mt-3 flex flex-wrap justify-center gap-3"
      >
        {sortedTurnouts.map(({ turnoutId, label }) => (
          <svg
            key={turnoutId}
            data-header-turnout-id={turnoutId}
            viewBox="-8 -12 16 24"
            className="h-12 w-8 shrink-0"
          >
            <TurnoutDirectionIndicator
              turnout={turnoutsById?.[turnoutId]}
              label={label}
              isLocked={lockedTurnoutIds.has(turnoutId)}
            />
          </svg>
        ))}
      </div>
    </header>
  );
}
