import { RouteReservationIndicator } from "@/components/ctc/RouteReservationIndicator";
import type { InterlockingStationStatus } from "@/gen/ctc_pb";

type Props = {
  station: InterlockingStationStatus | undefined;
  routeIds?: readonly string[];
  /** 配線図の座標での、ランプ中心間の縦間隔。 */
  spacing?: number;
  /** 1列に並べる表示灯の個数。 */
  rowsPerColumn?: number;
  /** 配線図の座標での、列の横間隔。 */
  columnSpacing?: number;
};

export function StationRouteIndicators({
  station,
  routeIds,
  spacing = 20,
  rowsPerColumn = 4,
  columnSpacing = 56,
}: Props) {
  const ids = routeIds ?? Object.keys(station?.routesById ?? {});

  return (
    <g>
      {ids.map((routeId, index) => (
        <g
          key={routeId}
          transform={`translate(${Math.floor(index / rowsPerColumn) * columnSpacing} ${(index % rowsPerColumn) * spacing}) scale(0.25) translate(-32 -32)`}
        >
          <RouteReservationIndicator station={station} routeId={routeId} />
        </g>
      ))}
    </g>
  );
}
