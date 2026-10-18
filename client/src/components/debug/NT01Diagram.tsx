import { RouteReservationIndicator } from "@/components/ctc/RouteReservationIndicator";
import { TrainLabel } from "@/components/ctc/TrainLabel";
import { TurnoutDirectionIndicator } from "@/components/ctc/TurnoutDirectionIndicator";
import { TurnoutPosition, type CtcSnapshot, type InterlockingStationStatus } from "@/gen/ctc_pb";
import diagram from "./NT01Diagram.data.json";

type Props = {
  circuitsById: CtcSnapshot["circuitsById"] | undefined;
  trainsById: CtcSnapshot["trainsById"] | undefined;
  turnoutsById: CtcSnapshot["turnoutsById"] | undefined;
  station: InterlockingStationStatus | undefined;
};

const turnoutIndicators = [
  { id: "NT01_23", label: "23", x: 535, y: 184 },
  { id: "NT01_21A", label: "21A", x: 671, y: 216 },
  { id: "NT01_22A", label: "22A", x: 671, y: 478 },
  { id: "NT01_21B", label: "21B", x: 935, y: 478 },
  { id: "NT01_22B", label: "22B", x: 933, y: 358 },
];

const trainAnchors: Record<string, { x: number; y: number }> = {
  NT01_1RT: { x: 200, y: 192 },
  NT01_2RT: { x: 200, y: 320 },
  NT01_3RT: { x: 200, y: 448 },
  NT01_23T_N: { x: 608, y: 320 },
  NT01_23T_R: { x: 608, y: 304 },
  NT01_21AT_N: { x: 784, y: 320 },
  NT01_21AT_R: { x: 784, y: 366 },
  NT01_22AT_N: { x: 784, y: 448 },
  NT01_22AT_R: { x: 784, y: 402 },
  NT01_21BT_N: { x: 880, y: 448 },
  NT01_21BT_R: { x: 880, y: 402 },
  NT01_22BT_N: { x: 944, y: 320 },
  NT01_22BT_R: { x: 944, y: 342 },
};

export function NT01Diagram({ circuitsById, trainsById, turnoutsById, station }: Props) {
  const routeIds = Array.from(new Set([...diagram.routeIds, ...Object.keys(station?.routesById ?? {})]));
  const circuitViews = diagram.circuits.map((shape) => {
    // _N は定位、_R は反位。軌道回路末尾の T を除くと転轍機IDになる。
    const branch = shape.id.match(/_([NR])$/)?.[1];
    const circuitId = branch ? shape.id.slice(0, -2) : shape.id;
    const turnoutId = branch ? circuitId.slice(0, -1) : undefined;
    const circuit = circuitsById?.[circuitId];
    const turnout = turnoutId ? turnoutsById?.[turnoutId] : undefined;
    const positionKnown = turnout?.actualPosition === TurnoutPosition.NORMAL ||
      turnout?.actualPosition === TurnoutPosition.REVERSE;
    const requiredPosition = branch === "N" ? TurnoutPosition.NORMAL : TurnoutPosition.REVERSE;
    const branchState = !branch ? undefined : turnout?.isMoving ? "moving" :
      !positionKnown ? "unknown" : turnout?.actualPosition === requiredPosition ? "active" : "inactive";
    const uncertain = branchState === "moving" || branchState === "unknown";
    const occupancy = circuit ? (circuit.isOccupied ? "在線" : "非在線") : "情報なし";
    const state = branchState === "moving" ? "転換中" : branchState === "unknown" ? "位置不明" :
      branchState === "inactive" ? "非開通" : occupancy;
    const color = circuit?.isOccupied && !uncertain && branchState !== "inactive"
      ? "#ef4444"
      : "#3f3f46";
    return { ...shape, circuitId, turnoutId, circuit, branchState, uncertain, state, color };
  });
  const displayedTrainIds = new Set<string>();
  const trainLabels = circuitViews.flatMap(({ id, circuitId, circuit, branchState, uncertain }) => {
    if (!circuit?.isOccupied || !circuit.trainId || uncertain || branchState === "inactive") return [];
    const train = trainsById?.[circuit.trainId];
    const anchor = trainAnchors[id];
    if (!train || !anchor || displayedTrainIds.has(circuit.trainId)) return [];
    displayedTrainIds.add(circuit.trainId);
    return [{ trainId: circuit.trainId, circuitId, train, anchor }];
  });

  return (
    <section aria-label="NT01配線図" className="space-y-3">
      <h2 className="text-lg font-semibold">NT01 配線図</h2>
      <p className="text-zinc-600 dark:text-zinc-400">
        赤：在線 ／ 暗いグレー：非在線・非開通・情報なし ／ 破線：転換中・位置不明
      </p>
      <p className="text-zinc-600 dark:text-zinc-400">
        表示灯：緑＝進路構成完了 ／ オレンジ＝進路設定済み ／ 黒＝消灯
      </p>
      <p className="text-zinc-600 dark:text-zinc-400">
        転轍機表示灯：N＝定位（緑） ／ R＝反位（赤） ／ 両消灯＝転換中・位置不明
      </p>
      <p className="text-zinc-600 dark:text-zinc-400">
        列車表示：列番・編成番号 ／ 赤丸は最後尾側の基準回路内の代表位置
      </p>
      <div className="overflow-hidden rounded border border-zinc-700 bg-black">
        <svg
          xmlns="http://www.w3.org/2000/svg"
          viewBox={`0 0 ${diagram.width} ${diagram.height}`}
          width={diagram.width}
          height={diagram.height}
          role="img"
          aria-label="NT01の配線・在線・進路予約・転轍機・列車情報"
          className="mx-auto block h-auto w-full max-w-5xl"
        >
          <g fill="none" strokeWidth="6">
            {circuitViews.map(({ id, paths, circuitId, turnoutId, branchState, uncertain, state, color }) => (
              <g
                key={id}
                data-cell-id={id}
                data-circuit-id={circuitId}
                data-turnout-id={turnoutId}
                data-branch-state={branchState}
                stroke={color}
                strokeDasharray={uncertain ? "8 6" : undefined}
              >
                <title>{`${id}：${state}`}</title>
                {paths.map((d) => <path key={d} d={d} vectorEffect="non-scaling-stroke" />)}
              </g>
            ))}
            {diagram.platforms.map(({ id, ...rect }) => (
              <g key={id} data-cell-id={id}>
                <rect {...rect} stroke="#a1a1aa" strokeWidth="1" />
              </g>
            ))}
          </g>
          <g aria-label="NT01の列車情報">
            {trainLabels.map(({ trainId, circuitId, train, anchor }) => (
              <g
                key={trainId}
                data-train-id={trainId}
                data-train-circuit-id={circuitId}
                transform={`translate(${anchor.x} ${anchor.y}) scale(0.75)`}
              >
                <TrainLabel train={train} x={-28} y={-148} anchorX={0} anchorY={0} />
              </g>
            ))}
          </g>
          <g aria-label="NT01の転轍機方向表示灯">
            {turnoutIndicators.map(({ id, label, x, y }) => (
              <g key={id} data-turnout-indicator-id={id} transform={`translate(${x} ${y}) scale(0.25)`}>
                <TurnoutDirectionIndicator label={label} turnout={turnoutsById?.[id]} />
              </g>
            ))}
          </g>
          <g
            aria-label="NT01の全進路予約表示灯"
            transform={`translate(${diagram.width - 120} 24) scale(0.4)`}
          >
            {routeIds.map((routeId, index) => (
              <RouteReservationIndicator
                key={routeId}
                station={station}
                routeId={routeId}
                y={index * 64}
              />
            ))}
          </g>
        </svg>
      </div>
    </section>
  );
}
