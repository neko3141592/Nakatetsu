import { RouteReservationIndicator } from "@/components/ctc/RouteReservationIndicator";
import { TrainLabel } from "@/components/ctc/TrainLabel";
import { TurnoutDirectionIndicator } from "@/components/ctc/TurnoutDirectionIndicator";
import diagramLayout from "@/data/diagrams/NT01.layout.json";
import { TurnoutPosition, type CtcSnapshot, type InterlockingStationStatus } from "@/gen/ctc_pb";
import diagram from "./NT01Diagram.data.json";

type Props = {
  circuitsById: CtcSnapshot["circuitsById"] | undefined;
  trainsById: CtcSnapshot["trainsById"] | undefined;
  turnoutsById: CtcSnapshot["turnoutsById"] | undefined;
  station: InterlockingStationStatus | undefined;
};

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
        転轍機番号：定位＝水色文字 ／ 反位＝水色背景・黒文字 ／ 轍査なし＝灰色
      </p>
      <p className="text-zinc-600 dark:text-zinc-400">
        列車表示：列番＋レ ／ 未設定の場合は「列番未設定」
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
                transform={`translate(${anchor.x} ${anchor.y})`}
              >
                <TrainLabel train={train} />
              </g>
            ))}
          </g>
          <g aria-label="NT01の転轍機位置">
            {diagramLayout.turnoutIndicators.map(({ id, turnoutId, label, x, y }) => (
              <g key={id} data-turnout-indicator-id={id} data-turnout-id={turnoutId} transform={`translate(${x} ${y})`}>
                <TurnoutDirectionIndicator label={label} turnout={turnoutsById?.[turnoutId]} />
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
