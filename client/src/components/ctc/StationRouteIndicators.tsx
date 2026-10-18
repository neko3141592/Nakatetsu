import { RouteReservationIndicator } from "@/components/ctc/RouteReservationIndicator";
import type { InterlockingStationStatus } from "@/gen/ctc_pb";

type Props = {
  station: InterlockingStationStatus | undefined;
  routeIds?: readonly string[];
  /** 配線図の座標での、ランプ中心間の縦間隔。 */
  spacing?: number;
};

export function StationRouteIndicators({ station, routeIds, spacing = 20 }: Props) {
  const ids = routeIds ?? Object.keys(station?.routesById ?? {});

  return (
    <g>
      {ids.map((routeId, index) => (
        <g key={routeId} transform={`translate(0 ${index * spacing}) scale(0.25) translate(-32 -32)`}>
          <RouteReservationIndicator station={station} routeId={routeId} />
        </g>
      ))}
    </g>
  );
}
