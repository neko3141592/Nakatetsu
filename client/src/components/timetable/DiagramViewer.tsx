"use client";

import { TriangleButton } from "@/components/ui/triangle-button";
import { getTrainDiagramPoints } from "@/lib/trainDiagram";
import type { TimetableDiagramStation, TimetableDiagramTrain } from "@/types/timetableDiagram";
import { useEffect, useId, useRef, useState } from "react";

type DiagramViewerProps = {
  trains: readonly TimetableDiagramTrain[];
  emphasizedTrainIds?: ReadonlySet<string>;
  stations: readonly TimetableDiagramStation[];
  defaultPixelsPerMinute: number;
  startSeconds: number;
  endSeconds: number;
  onTrainClick?: (trainNumber: string) => void;
};

const scales = [0.3, 0.5, 1, 2, 4, 8] as const;
const diagramThemes = {
  light: {
    background: "#ffffff",
    text: "#18181b",
    timeLine: "#a1a1aa",
    stationLine: "#71717a",
    minorLine: "#e4e4e7",
    trainLine: "#2563eb",
  },
  dark: {
    background: "#09090b",
    text: "#d4d4d8",
    timeLine: "#52525b",
    stationLine: "#a1a1aa",
    minorLine: "#27272a",
    trainLine: "#60a5fa",
  },
};

const themeButtonClassName = "h-5 min-w-7 rounded-xs border border-zinc-400 bg-zinc-100 px-1.5 text-xs text-zinc-900 aria-pressed:bg-zinc-950 aria-pressed:text-white";

export function DiagramViewer({
  trains,
  emphasizedTrainIds,
  stations,
  defaultPixelsPerMinute,
  startSeconds,
  endSeconds,
  onTrainClick,
}: DiagramViewerProps) {
  const containerRef = useRef<HTMLDivElement>(null);
  const plotClipId = useId();
  const [size, setSize] = useState({ width: 0, height: 0 });
  const [scaleIndex, setScaleIndex] = useState(2);
  const [verticalScaleIndex, setVerticalScaleIndex] = useState(2);
  const [theme, setTheme] = useState<"light" | "dark">("light");
  const colors = diagramThemes[theme];
  const horizontalScale = scales[scaleIndex];
  const verticalScale = scales[verticalScaleIndex];
  const stationLabelWidth = 96;
  const left = 16;
  const top = 16;
  const bottom = 16;
  const timeAxisHeight = 36;
  const yDivisions = stations.slice(0, -1)
    .reduce((sum, station) => sum + (station.divisionsToNext ?? 0), 0);
  const plotHeight = Math.max(0, size.height - top - bottom - timeAxisHeight) * verticalScale;
  const plotBottom = top + plotHeight;
  const svgHeight = Math.max(size.height - timeAxisHeight, plotBottom + bottom);
  const unitHeight = yDivisions > 0 ? plotHeight / yDivisions : 0;
  const stationPositions = getStationPositions(stations, unitHeight, top);

  const plotRight = getTimePosition(endSeconds, defaultPixelsPerMinute * horizontalScale, startSeconds, left);
  const svgWidth = Math.max(size.width - stationLabelWidth, plotRight + 24);
  const isDimmed = (train: TimetableDiagramTrain) =>
    emphasizedTrainIds !== undefined && !emphasizedTrainIds.has(train.id ?? train.trainNumber);
  const trainsInPaintOrder = emphasizedTrainIds?.size
    ? [...trains].sort((a, b) => Number(isDimmed(b)) - Number(isDimmed(a)))
    : trains;

  const timeSeconds: number[] = [];
  for (let seconds = Math.ceil(startSeconds / 60) * 60; seconds <= endSeconds; seconds += 60) {
    timeSeconds.push(seconds);
  }

  useEffect(() => {
    const element = containerRef.current;
    if (!element) return;

    const observer = new ResizeObserver(([entry]) => {
      setSize({ width: entry.contentRect.width, height: entry.contentRect.height });
    });
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    const firstSeconds = Math.min(...trains.flatMap((train) => (train.points ?? [])
      .filter((point) => (point.section ?? "service") === "service")
      .flatMap((point) => [point.arrivalSeconds, point.departureSeconds])
      .filter((seconds): seconds is number => seconds !== null && Number.isFinite(seconds))));
    if (containerRef.current && Number.isFinite(firstSeconds)) {
      containerRef.current.scrollLeft = Math.max(0, getTimePosition(firstSeconds, defaultPixelsPerMinute * horizontalScale, startSeconds, left) - 48);
    }
  }, [trains, defaultPixelsPerMinute, horizontalScale, startSeconds]);

  return (
    <div className="flex h-full min-h-0 w-full flex-col overflow-hidden">
      <div className="flex h-7 shrink-0 items-center justify-end gap-2 border-b border-zinc-700 bg-zinc-300 px-3">
        <div className="mr-auto flex gap-1" role="group" aria-label="ダイアグラムの配色">
          <button type="button" aria-pressed={theme === "light"} onClick={() => setTheme("light")} className={themeButtonClassName}>
            明
          </button>
          <button type="button" aria-pressed={theme === "dark"} onClick={() => setTheme("dark")} className={themeButtonClassName}>
            暗
          </button>
        </div>
        <div className="flex items-center gap-2" role="group" aria-label="横倍率">
          <TriangleButton
            direction="left"
            aria-label="横方向縮小"
            disabled={scaleIndex === 0}
            onClick={() => setScaleIndex((index) => Math.max(0, index - 1))}
          />
          <span aria-live="polite" className="w-3.5 text-center">{String(horizontalScale).replace(/^0\./, ".")}x</span>
          <TriangleButton
            direction="right"
            aria-label="横方向拡大"
            disabled={scaleIndex === scales.length - 1}
            onClick={() => setScaleIndex((index) => Math.min(scales.length - 1, index + 1))}
          />
        </div>
        <div className="ml-3 flex items-center gap-2" role="group" aria-label="縦倍率">
          <TriangleButton
            direction="down"
            aria-label="縦方向縮小"
            disabled={verticalScaleIndex === 0}
            onClick={() => setVerticalScaleIndex((index) => Math.max(0, index - 1))}
          />
          <span aria-live="polite" className="w-3.5 text-center">{String(verticalScale).replace(/^0\./, ".")}x</span>
          <TriangleButton
            direction="up"
            aria-label="縦方向拡大"
            disabled={verticalScaleIndex === scales.length - 1}
            onClick={() => setVerticalScaleIndex((index) => Math.min(scales.length - 1, index + 1))}
          />
        </div>
      </div>
      <div className="relative min-h-0 flex-1" style={{ backgroundColor: colors.background, colorScheme: theme }}>
        <div ref={containerRef} className="absolute inset-0 overflow-auto">
          <div className="flex" style={{ width: stationLabelWidth + svgWidth, height: svgHeight }}>
            <div className="sticky left-0 z-10 shrink-0" style={{ width: stationLabelWidth, backgroundColor: colors.background }}>
              <svg width={stationLabelWidth} height={svgHeight} className="block" role="img" aria-label="駅名">
                {stations.map((station) => {
                  const y = stationPositions.get(station.stationId);
                  if (y === undefined) return null;

                  return (
                    <text key={station.stationId} x={stationLabelWidth - 8} y={y} textAnchor="end" dominantBaseline="middle" fill={colors.text} fontSize={12}>
                      {station.displayName}
                    </text>
                  );
                })}
              </svg>
            </div>
            <svg width={svgWidth} height={svgHeight} className="block shrink-0" style={{ backgroundColor: colors.background }} role={onTrainClick ? "group" : "img"} aria-label="列車ダイアグラム">
              <defs><clipPath id={plotClipId}><rect x={left} y={top} width={Math.max(0, plotRight - left)} height={plotHeight} /></clipPath></defs>
              {timeSeconds.map((seconds) => {
                const x = getTimePosition(seconds, defaultPixelsPerMinute * horizontalScale, startSeconds, left);
                const isMajor = seconds % (10 * 60) === 0;

                return (
                  <g key={seconds}>
                    <line
                      x1={x}
                      x2={x}
                      y1={top}
                      y2={plotBottom}
                      stroke={isMajor ? colors.timeLine : colors.minorLine}
                      strokeWidth={isMajor ? 1 : 0.5}
                    />
                  </g>
                );
              })}
              {stations.map((station) => {
                const y = stationPositions.get(station.stationId);
                if (y === undefined) return null;

                const subdivisionYs: number[] = [];
                for (let division = 1; division < (station.divisionsToNext ?? 0); division++) {
                  subdivisionYs.push(y + division * unitHeight);
                }

                return (
                  <g key={station.stationId}>
                    {subdivisionYs.map((subdivisionY, index) => (
                      <line key={index} x1={left} x2={plotRight} y1={subdivisionY} y2={subdivisionY} stroke={colors.minorLine} strokeWidth={0.5} />
                    ))}
                    <line x1={left} x2={plotRight} y1={y} y2={y} stroke={colors.stationLine} strokeWidth={1} />
                  </g>
                );
              })}
              <g clipPath={`url(#${plotClipId})`}>
                {trainsInPaintOrder.map((train) => {
                  const points = getTrainDiagramPoints(train.points ?? [], stationPositions, defaultPixelsPerMinute * horizontalScale, startSeconds, left);
                  if (points.length < 2) return null;
                  const color = train.trainType?.color || colors.trainLine;
                  const path = points.map(({ x, y }) => `${x},${y}`).join(" ");
                  return <g key={train.id ?? train.trainNumber} opacity={isDimmed(train) ? 0.25 : undefined} role={onTrainClick ? "button" : "group"}
                    aria-label={onTrainClick ? `${train.trainNumber}を編集` : `${train.trainNumber}のスジ`}
                    aria-haspopup={onTrainClick ? "dialog" : undefined} tabIndex={onTrainClick ? 0 : undefined}
                    className={onTrainClick ? "group/train cursor-pointer outline-none" : undefined}
                    onClick={() => onTrainClick?.(train.id ?? train.trainNumber)}
                    onKeyDown={(event) => {
                      if (onTrainClick && (event.key === "Enter" || event.key === " ")) {
                        event.preventDefault();
                        onTrainClick(train.id ?? train.trainNumber);
                      }
                    }}>
                    <title>{train.trainNumber}</title>
                    {onTrainClick ? <polyline points={path} fill="none" stroke="transparent" strokeWidth={12} pointerEvents="stroke" aria-hidden="true" /> : null}
                    <polyline points={path} fill="none" stroke={color} strokeWidth={1.5} className="group-focus-visible/train:stroke-[3]" />
                    <text x={points[0].x + 4} y={Math.min(points[0].y + 12, plotBottom - 4)} fill={color} fontSize={11}>{train.trainNumber}</text>
                  </g>;
                })}
              </g>
            </svg>
          </div>
          <div className="sticky bottom-0 z-20 flex" style={{ width: stationLabelWidth + svgWidth, height: timeAxisHeight, backgroundColor: colors.background }}>
            <div className="sticky left-0 z-10 shrink-0" style={{ width: stationLabelWidth, backgroundColor: colors.background }} />
            <svg width={svgWidth} height={timeAxisHeight} className="block shrink-0" role="img" aria-label="時刻目盛り">
              {timeSeconds.filter((seconds) => seconds % (10 * 60) === 0).map((seconds) => (
                <text
                  key={seconds}
                  x={getTimePosition(seconds, defaultPixelsPerMinute * horizontalScale, startSeconds, left)}
                  y={20}
                  textAnchor="middle"
                  dominantBaseline="middle"
                  fill={colors.text}
                  fontSize={12}
                >
                  {formatTime(seconds)}
                </text>
              ))}
            </svg>
          </div>
        </div>
      </div>
    </div>
  );
}

function formatTime(seconds: number) {
  const hours = Math.floor(seconds / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
}

function getTimePosition(
  currentSeconds: number,
  pixelsPerMinute: number,
  startSeconds: number,
  left = 0,
) {
  return left + ((currentSeconds - startSeconds) / 60) * pixelsPerMinute;
}

function getStationPositions(
  stations: readonly TimetableDiagramStation[],
  unitHeight: number,
  top = 0,
): Map<string, number> {
  const positions = new Map<string, number>();
  let divisions = 0;

  for (const station of stations) {
    positions.set(station.stationId, top + divisions * unitHeight);
    divisions += station.divisionsToNext ?? 0;
  }

  return positions;
}
