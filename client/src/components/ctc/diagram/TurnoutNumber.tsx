type Props = {
  label: string;
};

export function TurnoutNumber({ label }: Props) {
  return (
    <text
      x="0"
      y="0"
      dominantBaseline="central"
      textAnchor="middle"
      fontSize="8"
      fill="#38bdf8"
      data-turnout-number={label}
    >
      {label}
    </text>
  );
}
