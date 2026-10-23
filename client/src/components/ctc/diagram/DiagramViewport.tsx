"use client";

import { Equal, Minus, Plus } from "lucide-react";
import type { ReactNode } from "react";
import { TransformComponent, TransformWrapper } from "react-zoom-pan-pinch";

import { Button } from "@/components/ui/button";

type Props = {
  width: number;
  height: number;
  children: ReactNode;
  label?: string;
  fitLabel?: string;
};

const PADDING = 32;
const controlClassName =
  "size-6 rounded-full border-0 bg-zinc-200 p-0 text-zinc-800 shadow-none transition-none hover:bg-zinc-200 hover:text-zinc-800 [&_svg]:size-4";

export function DiagramViewport({ width, height, children, label = "全線の配線図", fitLabel = "全線表示に戻す" }: Props) {
  const canvasWidth = width + PADDING * 2;
  const canvasHeight = height + PADDING * 2;

  return (
    <div className="relative h-full w-full overflow-hidden">
      <TransformWrapper
        fitOnInit
        minScale={0.05}
        maxScale={4}
        limitToBounds={false}
        disablePadding
        panning={{ velocityDisabled: true, allowRightClickPan: false }}
        doubleClick={{ disabled: true }}
        keyboard={{ disabled: false }}
      >
        {({ zoomIn, zoomOut, fitToView }) => (
          <>
            <TransformComponent
              wrapperStyle={{ width: "100%", height: "100%" }}
              contentStyle={{ width: canvasWidth, height: canvasHeight }}
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width={canvasWidth}
                height={canvasHeight}
                viewBox={`${-PADDING} ${-PADDING} ${canvasWidth} ${canvasHeight}`}
                aria-label={label}
                className="block max-w-none select-none"
              >
                {children}
              </svg>
            </TransformComponent>

            <div
              role="group"
              aria-label="配線図の表示操作"
              className="absolute bottom-6 left-6 flex flex-col gap-2"
            >
              <Button
                type="button"
                size="icon"
                aria-label="拡大"
                title="拡大"
                className={controlClassName}
                onClick={() => void zoomIn(0.25, 150)}
              >
                <Plus aria-hidden="true" />
              </Button>
              <Button
                type="button"
                size="icon"
                aria-label="縮小"
                title="縮小"
                className={controlClassName}
                onClick={() => void zoomOut(0.25, 150)}
              >
                <Minus aria-hidden="true" />
              </Button>
              <Button
                type="button"
                size="icon"
                aria-label={fitLabel}
                title={fitLabel}
                className={controlClassName}
                onClick={() => void fitToView({ animationTime: 150 })}
              >
                <Equal aria-hidden="true" />
              </Button>
            </div>
          </>
        )}
      </TransformWrapper>
    </div>
  );
}
