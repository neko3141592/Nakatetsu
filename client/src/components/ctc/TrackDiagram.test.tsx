import { create } from "@bufbuild/protobuf";
import { act, type ComponentProps } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";

import { CtcSnapshotSchema, InterlockingRouteStatusSchema, InterlockingStationStatusSchema, TurnoutPosition, TurnoutStateSchema } from "@/gen/ctc_pb";
import { TrackDiagram } from "./TrackDiagram";
import { TurnoutDirectionIndicator } from "./TurnoutDirectionIndicator";

type Props = ComponentProps<typeof TrackDiagram>;
// CSSOMが正規化した色で、SVGに実際に設定されたstrokeを比較する。
const RED = "rgb(239, 68, 68)";
const YELLOW = "rgb(255, 213, 23)";
const WHITE = "rgb(255, 255, 255)";
const N = TurnoutPosition.NORMAL;
const R = TurnoutPosition.REVERSE;
const svg = '<path id="plain" d="M0 0H100"/><path id="normal" d="M100 0H200"/><path id="reverse" d="M100 0L200 100"/>';
const circuitSvgMap: Props["circuitSvgMap"] = {
  plain: { svgId: "plain" },
  branch: { turnoutId: "point", normalSvgId: "normal", reverseSvgId: "reverse" },
};
const routeDefinitions: Props["routeDefinitions"] = [N, R].map((position) => ({
  routeId: String(position),
  trackCircuitIds: ["plain", "branch"],
  requiredTurnouts: [{ connectionId: "point", requiredPosition: position }],
}));

function circuits(isOccupied: boolean) {
  return create(CtcSnapshotSchema, {
    circuitsById: { plain: { isOccupied }, branch: { isOccupied } },
  }).circuitsById;
}

function station(position?: TurnoutPosition) {
  return create(InterlockingStationStatusSchema, {
    isInitialized: true,
    hasOutput: true,
    routesById: position === undefined ? {} : { [position]: { isRouteSet: true } },
  });
}

const states: {
  name: string;
  turnoutsById: Props["turnoutsById"];
  occupiedColors: readonly [string, string];
  indicator: string;
}[] = [
  { name: "定位確定", turnoutsById: { point: create(TurnoutStateSchema, { actualPosition: N }) }, occupiedColors: [RED, WHITE], indicator: "normal" },
  { name: "反位確定", turnoutsById: { point: create(TurnoutStateSchema, { actualPosition: R }) }, occupiedColors: [WHITE, RED], indicator: "reverse" },
  { name: "定位情報を保持した転換中", turnoutsById: { point: create(TurnoutStateSchema, { actualPosition: N, isMoving: true }) }, occupiedColors: [RED, RED], indicator: "moving" },
  { name: "反位情報を保持した転換中", turnoutsById: { point: create(TurnoutStateSchema, { actualPosition: R, isMoving: true }) }, occupiedColors: [RED, RED], indicator: "moving" },
  { name: "位置不明の転換中", turnoutsById: { point: create(TurnoutStateSchema, { isMoving: true }) }, occupiedColors: [RED, RED], indicator: "moving" },
  { name: "位置不明", turnoutsById: { point: create(TurnoutStateSchema, { actualPosition: TurnoutPosition.UNKNOWN }) }, occupiedColors: [RED, RED], indicator: "unknown" },
  { name: "位置フィールド省略（Protobuf既定値）", turnoutsById: { point: create(TurnoutStateSchema) }, occupiedColors: [RED, RED], indicator: "unknown" },
  { name: "未対応の位置値", turnoutsById: { point: create(TurnoutStateSchema, { actualPosition: 99 as TurnoutPosition }) }, occupiedColors: [RED, RED], indicator: "unknown" },
  { name: "転轍機ID欠落", turnoutsById: { other: create(TurnoutStateSchema, { actualPosition: N }) }, occupiedColors: [RED, RED], indicator: "unknown" },
  { name: "転轍機マップ空", turnoutsById: {}, occupiedColors: [RED, RED], indicator: "unknown" },
  { name: "転轍機マップ未取得", turnoutsById: undefined, occupiedColors: [RED, RED], indicator: "unknown" },
];

let container: HTMLDivElement;
let root: Root;

beforeAll(() => {
  Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true });
});

beforeEach(() => {
  container = document.createElement("div");
  document.body.append(container);
  root = createRoot(container);
});

afterEach(async () => {
  await act(() => root.unmount());
  container.remove();
});

async function render(overrides: Partial<Props> = {}) {
  const props = { svg, circuitSvgMap, routeDefinitions, ...overrides };
  await act(() => root.render(
    <svg>
      <TrackDiagram {...props} />
      <TurnoutDirectionIndicator turnout={props.turnoutsById?.point} label="point" />
    </svg>,
  ));
}

function stroke(id: string) {
  const value = container.querySelector<SVGElement>(`#${id}`)?.style.getPropertyValue("stroke");
  // strokeのシリアライズ形式（hex/rgb）の環境差を除いて色を比較する。
  const style = document.createElement("span").style;
  style.color = value ?? "";
  return style.color;
}

describe.each(states)("$name", ({ turnoutsById, occupiedColors, indicator }) => {
  it.each([undefined, N, R])("在線は予約位置%sの黄色より優先する", async (reservedPosition) => {
    await render({ circuitsById: circuits(true), turnoutsById, station: station(reservedPosition) });

    const expected = occupiedColors.map((color, index) =>
      color === RED ? RED : reservedPosition === [N, R][index] ? YELLOW : WHITE,
    );
    expect([stroke("normal"), stroke("reverse")]).toEqual(expected);
    expect(stroke("plain")).toBe(RED);
    expect(container.querySelector("[data-turnout-state]")?.getAttribute("data-turnout-state")).toBe(indicator);
  });

  it.each([undefined, N, R])("非在線では予約位置%sだけ黄色、他は白", async (reservedPosition) => {
    await render({ circuitsById: circuits(false), turnoutsById, station: station(reservedPosition) });

    expect([stroke("normal"), stroke("reverse")]).toEqual([
      reservedPosition === N ? YELLOW : WHITE,
      reservedPosition === R ? YELLOW : WHITE,
    ]);
    expect(stroke("plain")).toBe(reservedPosition === undefined ? WHITE : YELLOW);
  });
});

it("定位在線→転換中→情報欠落→反位確定→非在線の更新で赤を維持・解除する", async () => {
  const props = { circuitsById: circuits(true), station: station(R) };
  for (const [turnoutsById, expected] of [
    [{ point: create(TurnoutStateSchema, { actualPosition: N }) }, [RED, YELLOW]],
    [{ point: create(TurnoutStateSchema, { actualPosition: N, isMoving: true }) }, [RED, RED]],
    [undefined, [RED, RED]],
    [{ point: create(TurnoutStateSchema, { actualPosition: R }) }, [WHITE, RED]],
  ] as const) {
    await render({ ...props, turnoutsById });
    expect([stroke("normal"), stroke("reverse")]).toEqual(expected);
  }

  await render({ ...props, circuitsById: circuits(false) });
  expect([stroke("plain"), stroke("normal"), stroke("reverse")]).toEqual([YELLOW, WHITE, YELLOW]);
  await render({ circuitsById: circuits(false) });
  expect([stroke("plain"), stroke("normal"), stroke("reverse")]).toEqual([WHITE, WHITE, WHITE]);
});

it.each([
  { name: "連動情報なし", routeStation: undefined },
  { name: "連動未初期化", routeStation: create(InterlockingStationStatusSchema, { ...station(N), isInitialized: false }) },
  { name: "連動出力なし", routeStation: create(InterlockingStationStatusSchema, { ...station(N), hasOutput: false }) },
  { name: "進路未設定", routeStation: create(InterlockingStationStatusSchema, { ...station(N), routesById: { [N]: create(InterlockingRouteStatusSchema) } }) },
])("未確立の進路を黄色にしない：$name", async ({ routeStation }) => {
  await render({ circuitsById: circuits(false), station: routeStation });
  expect([stroke("plain"), stroke("normal"), stroke("reverse")]).toEqual([WHITE, WHITE, WHITE]);
  await render({ circuitsById: circuits(true), station: routeStation });
  expect([stroke("plain"), stroke("normal"), stroke("reverse")]).toEqual([RED, RED, RED]);
});
