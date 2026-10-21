import { TurnoutPosition, type TurnoutState } from "@/gen/ctc_pb";

type Props = {
  turnout: TurnoutState | undefined;
  label: string;
};

export function TurnoutDirectionIndicator({ turnout, label }: Props) {
  const position = turnout?.isMoving ? TurnoutPosition.UNKNOWN : turnout?.actualPosition;
  const state = position === TurnoutPosition.NORMAL
    ? "normal"
    : position === TurnoutPosition.REVERSE ? "reverse" : "unknown";
  const stateLabel = {
    normal: "定位",
    reverse: "反位",
    unknown: "轍査なし",
  }[state];
  const width = label.length * 5 + 4;

  return (
    <g
      role="img"
      aria-label={`${label}：${stateLabel}`}
      data-turnout-label={label}
      data-turnout-state={state}
    >
      {state === "reverse" ? (
        <rect x={-width / 2} y="-6" width={width} height="12" fill="#38bdf8" />
      ) : null}
      <text
        x="0"
        y="0"
        dominantBaseline="central"
        textAnchor="middle"
        fontSize="8"
        fill={state === "reverse" ? "#000000" : state === "normal" ? "#38bdf8" : "#a1a1aa"}
      >
        {label}
      </text>
    </g>
  );
}
