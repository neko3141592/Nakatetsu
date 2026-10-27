"use client";

import { cn } from "cn";

import type { InterlockingRouteStatus } from "@/gen/ctc_pb";

type Props = {
  originName: string;
  destinationName: string;
  // 未受信・駅情報が未初期化の場合はundefinedを渡す。
  route: InterlockingRouteStatus | undefined;
  onCancel?: () => void;
  disabled?: boolean;
  className?: string;
};

export function RouteStatusIndicator({ originName, destinationName, route, onCancel, disabled = false, className }: Props) {
  const label = `${originName}～${destinationName}`;
  const canCancel = Boolean(route?.isRouteSet && !route.cancelPending && onCancel && !disabled);
  const lamp = route?.pathEstablished
    ? { state: "established", asset: "Green", label: "進路構成完了" }
    : route?.isRouteSet
      ? { state: "set", asset: "Orange", label: "進路設定済み" }
      : { state: "off", asset: "Black_Off", label: "消灯" };

  return (
    <button
      type="button"
      disabled={!canCancel}
      onClick={canCancel ? onCancel : undefined}
      aria-label={`${label}：${lamp.label}${route?.cancelPending ? "・取り消し待ち" : canCancel ? "。進路取り消しを要求" : ""}`}
      data-lamp-state={lamp.state}
      className={cn(
        "relative grid min-h-10 w-full min-w-0 grid-cols-[minmax(0,1fr)_1em_minmax(0,1fr)] items-center gap-x-0.5 rounded-xs border border-black bg-[#333333] px-2 py-1 text-left text-xl font-normal text-white enabled:cursor-pointer enabled:hover:bg-[#3f3f3f] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-sky-300 disabled:cursor-default",
        className,
      )}
    >
      <span className="min-w-0 truncate" title={originName}>{originName}</span>
      <span aria-hidden="true" className="text-center">～</span>
      <span className="min-w-0 truncate pr-7" title={destinationName}>{destinationName}</span>
      <svg width="24" height="24" viewBox="468 468 88 88" aria-hidden="true" className="absolute right-2 top-1/2 -translate-y-1/2">
        <image
          href={`/ctc/pilotlamp/pilotlamp_${lamp.asset}.svg`}
          width="1024"
          height="1024"
        />
      </svg>
    </button>
  );
}
