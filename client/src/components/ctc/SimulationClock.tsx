import type { WorldTimeInformation } from "@/gen/ctc_pb";

type Props = {
  worldTime: WorldTimeInformation | undefined;
};

function formatTime(seconds: number | undefined) {
  if (seconds === undefined || !Number.isFinite(seconds) || seconds < 0) {
    return "--:--:--";
  }

  const totalSeconds = Math.floor(seconds) % 86400;
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const remainingSeconds = totalSeconds % 60;

  return [hours, minutes, remainingSeconds]
    .map((value) => String(value).padStart(2, "0"))
    .join(":");
}

function formatSpeed(speed: number | undefined) {
  if (speed === undefined || !Number.isFinite(speed) || speed <= 0) {
    return "--x";
  }

  return `${new Intl.NumberFormat("en-US", {
    maximumFractionDigits: 3,
    useGrouping: false,
  }).format(speed)}x`;
}

export function SimulationClock({ worldTime }: Props) {
  const time = formatTime(worldTime?.worldTimeSeconds);
  const speed = formatSpeed(worldTime?.playbackSpeed);
  const pausedLabel = worldTime?.isPaused ? "、一時停止中" : "";

  return (
    <div
      className="flex shrink-0 items-baseline gap-2 self-center pr-3 text-zinc-800 sm:pr-8"
      title={worldTime?.isPaused ? "シミュレーション一時停止中" : undefined}
    >
      <span
        aria-label={`シミュレーション時刻 ${time === "--:--:--" ? "未受信" : time}`}
        className="text-base tabular-nums sm:text-2xl"
      >
        {time}
      </span>
      <span
        aria-label={`再生倍率 ${speed === "--x" ? "未受信" : speed}${pausedLabel}`}
        className="text-xs text-zinc-600 sm:text-sm"
      >
        {speed}
      </span>
    </div>
  );
}
