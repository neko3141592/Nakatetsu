import type { CtcTrainState } from "@/gen/ctc_pb";

type Props = {
  train: CtcTrainState;
};

export function TrainLabel({ train }: Props) {
  const trainNumber = train.trainNumber.trim();
  const label = trainNumber ? `${trainNumber}レ` : "列番未設定";
  const width = Array.from(label).reduce(
    (total, character) => total + (character.charCodeAt(0) < 128 ? 7.2 : 12), 8,
  );

  return (
    <g
      role="img"
      aria-label={label}
      data-train-number={train.trainNumber}
    >
      <rect
        x={-width / 2}
        y="-14"
        width={width}
        height="28"
        fill="transparent"
        pointerEvents="all"
      />
      <text
        x="0"
        y="0"
        textAnchor="middle"
        dominantBaseline="central"
        fontSize="12"
        fontFamily="monospace"
        fill="#39FF14"
      >
        {label}
      </text>
    </g>
  );
}
