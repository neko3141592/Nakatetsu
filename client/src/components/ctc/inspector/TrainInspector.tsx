import type { CtcTrainState } from "@/gen/ctc_pb";

type Props = {
  trainId: string;
  train: CtcTrainState | undefined;
};

export function TrainInspector({ trainId, train }: Props) {
  const trainNumber = train?.trainNumber.trim();

  return (
    <section aria-label="列車詳細" className="space-y-4 p-4 text-sm">
      <div>
        <h1 className="text-2xl">
        {train ? (trainNumber ? `${trainNumber}レ` : "列番未設定") : "未受信"}
        </h1>
        <p className="text-zinc-400">{train?.consistId}</p>
      </div>
      
      {!train ? <p className="text-xs text-zinc-400">列車情報を受信していません。</p> : null}
    </section>
  );
}
