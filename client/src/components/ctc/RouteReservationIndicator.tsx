import type { InterlockingStationStatus } from "@/gen/ctc_pb";

type Props = {
  station: InterlockingStationStatus | undefined;
  routeId: string;
  x?: number;
  y?: number;
};

export function RouteReservationIndicator({ station, routeId, x = 0, y = 0 }: Props) {
  const route = station?.isInitialized && station.hasOutput
    ? station.routesById[routeId]
    : undefined;
  const lamp = route?.pathEstablished
    ? { state: "established", asset: "Green", label: "進路構成完了" }
    : route?.isRouteSet
      ? { state: "set", asset: "Orange", label: "進路設定済み" }
      : { state: "off", asset: "Black_Off", label: "消灯" };

  return (
    <g
      transform={`translate(${x} ${y})`}
      role="img"
      aria-label={`${routeId}：${lamp.label}`}
      data-route-id={routeId}
      data-lamp-state={lamp.state}
    >
      <svg width="64" height="64" viewBox="468 468 88 88" aria-hidden="true">
        <image
          href={`/ctc/pilotlamp/pilotlamp_${lamp.asset}.svg`}
          width="1024"
          height="1024"
        />
      </svg>
      <text
        x="64"
        y="32"
        dominantBaseline="central"
        fontSize="40"
        fontFamily="Arial, sans-serif"
        fill="#BABABA"
      >
        {routeId}
      </text>
    </g>
  );
}
