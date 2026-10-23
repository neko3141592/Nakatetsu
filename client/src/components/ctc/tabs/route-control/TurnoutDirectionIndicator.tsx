import { TurnoutPosition, type TurnoutState } from "@/gen/ctc_pb";

type Props = {
  turnout: TurnoutState | undefined;
  label: string;
  isLocked?: boolean;
};

export function TurnoutDirectionIndicator({ turnout, label, isLocked = false }: Props) {
  const position = turnout?.actualPosition;
  const state = turnout?.isMoving ? "moving" : position === TurnoutPosition.NORMAL
    ? "normal"
    : position === TurnoutPosition.REVERSE ? "reverse" : "unknown";
  const stateLabel = {
    normal: "定位",
    reverse: "反位",
    moving: "転換中",
    unknown: "轍査なし",
  }[state];
  const normalLamp = "-7,-8 0,-10.5 7,-8";
  const reverseLamp = "-7,8 0,10.5 7,8";

  return (
    <g
      role="img"
      aria-label={`${label}：${stateLabel}${isLocked ? "・鎖錠中" : ""}`}
      data-turnout-label={label}
      data-turnout-state={state}
      data-turnout-locked={isLocked}
    >
      <rect x="-7" y="-7" width="14" height="14" fill={isLocked ? "#ffd517" : "#333333"} />
      <polygon points={normalLamp} fill="#333333" />
      <polygon points={reverseLamp} fill="#333333" />
      <g className={state === "moving" ? "ctc-turnout-flash" : undefined}>
        {state === "normal" || state === "moving" ? (
          <polygon points={normalLamp} fill="#00ff00" />
        ) : null}
        {state === "reverse" || state === "moving" ? (
          <polygon points={reverseLamp} fill="#ff2424" />
        ) : null}
      </g>
      <text
        x="0"
        y="0"
        dominantBaseline="central"
        textAnchor="middle"
        fontSize="8"
        fill={isLocked ? "#000000" : "#ffffff"}
      >
        {label}
      </text>
    </g>
  );
}
