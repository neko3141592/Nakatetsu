import type { CtcTrainState } from "@/gen/ctc_pb";

type Props = {
  train: CtcTrainState;
  x: number;
  y: number;
  anchorX: number;
  anchorY: number;
};

function fittedTextLength(value: string, fontSize: number, maxWidth: number) {
  const estimatedWidth = Array.from(value).reduce((width, character) => {
    const widthRatio = /[MW@%]|[^\u0020-\u007E]/.test(character) ? 1 : 0.7;
    return width + fontSize * widthRatio;
  }, 0);

  return estimatedWidth > maxWidth ? maxWidth : undefined;
}

export function TrainLabel({ train, x, y, anchorX, anchorY }: Props) {
  const trainNumber = train.trainNumber.trim() || "—";
  const consistNumber = train.consistNumber.trim() || "—";
  const description = `列番 ${train.trainNumber.trim() ? train.trainNumber : "—"}、編成番号 ${train.consistNumber.trim() ? train.consistNumber : "—"}`;

  return (
    <g
      transform={`translate(${x} ${y})`}
      role="img"
      aria-label={description}
      data-train-number={train.trainNumber}
      data-consist-number={train.consistNumber}
    >
      <title>{description}</title>
      <line
        x1="28"
        y1="129"
        x2={anchorX - x}
        y2={anchorY - y}
        stroke="#EF4444"
        strokeWidth="1"
        vectorEffect="non-scaling-stroke"
      />
      <circle cx={anchorX - x} cy={anchorY - y} r="3" fill="#EF4444" />
      <svg
        x="0"
        y="28"
        width="96"
        height="100.8"
        viewBox="432 428 160 168"
        aria-hidden="true"
      >
        <image
          href="/ctc/train/NakatetsuSeries10000.svg"
          width="1024"
          height="1024"
        />
      </svg>
      <text
        x="48"
        y="20"
        textAnchor="middle"
        fontSize="24"
        fontFamily="Arial, sans-serif"
        fill="#FFFFFF"
        textLength={fittedTextLength(trainNumber, 24, 88)}
        lengthAdjust="spacingAndGlyphs"
      >
        {trainNumber}
      </text>
      <text
        x="88"
        y="62"
        textAnchor="end"
        fontSize="11"
        fontFamily="Arial, sans-serif"
        fill="#000000"
        textLength={fittedTextLength(consistNumber, 11, 80)}
        lengthAdjust="spacingAndGlyphs"
      >
        {consistNumber}
      </text>
    </g>
  );
}
