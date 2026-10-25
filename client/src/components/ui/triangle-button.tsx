"use client";

import type { ComponentProps } from "react";

import { buttonFrameClassName } from "@/components/ui/button";
import { cn } from "@/lib/utils";

const trianglePoints = {
  left: "3,8 12,2 12,14",
  right: "13,8 4,2 4,14",
  up: "8,3 2,12 14,12",
  down: "8,13 2,4 14,4",
};

type Props = Omit<ComponentProps<"button">, "children"> & {
  direction: keyof typeof trianglePoints;
  "aria-label": string;
};

export function TriangleButton({ direction, type = "button", className, ...props }: Props) {
  return (
    <button
      {...props}
      type={type}
      className={cn(buttonFrameClassName, "disabled:cursor-not-allowed disabled:opacity-30", className)}
    >
      <svg width="16" height="16" viewBox="0 0 16 16" aria-hidden="true">
        <polygon points={trianglePoints[direction]} fill="currentColor" />
      </svg>
    </button>
  );
}
