import { TurnoutPosition, type TurnoutState } from "@/gen/ctc_pb";

type Props = {
  turnout: TurnoutState | undefined;
  label: string;
  x?: number;
  y?: number;
};

export function TurnoutDirectionIndicator({ turnout, label, x = 0, y = 0 }: Props) {
  const state = turnout?.isMoving
    ? "moving"
    : turnout?.actualPosition === TurnoutPosition.NORMAL
      ? "normal"
      : turnout?.actualPosition === TurnoutPosition.REVERSE
        ? "reverse"
        : "unknown";
  const stateLabel = {
    normal: "定位",
    reverse: "反位",
    moving: "転換中",
    unknown: "位置不明",
  }[state];

  return (
    <g
      transform={`translate(${x} ${y})`}
      role="img"
      aria-label={`${label}：${stateLabel}`}
      data-turnout-label={label}
      data-turnout-state={state}
      fontFamily="Arial, sans-serif"
    >
      <text x="48" y="24" textAnchor="middle" fontSize="32" fill="#BABABA">
        R
      </text>
      <text x="152" y="24" textAnchor="middle" fontSize="32" fill="#BABABA">
        N
      </text>
      <svg x="16" y="32" width="64" height="64" viewBox="468 468 88 88" aria-hidden="true">
        <image
          href={`/ctc/pilotlamp/pilotlamp_Red${state === "reverse" ? "" : "_Off"}.svg`}
          width="1024"
          height="1024"
        />
      </svg>
      <svg x="120" y="32" width="64" height="64" viewBox="468 468 88 88" aria-hidden="true">
        <image
          href={`/ctc/pilotlamp/pilotlamp_Green${state === "normal" ? "" : "_Off"}.svg`}
          width="1024"
          height="1024"
        />
      </svg>
      <text
        x="100"
        y="140"
        dominantBaseline="central"
        textAnchor="middle"
        fontSize="50"
        fill="#FFFFFF"
      >
        {label}
      </text>
    </g>
  );
}
