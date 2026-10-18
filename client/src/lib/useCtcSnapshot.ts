"use client";

import { useEffect, useState } from "react";

import type { CtcSnapshot } from "@/gen/ctc_pb";
import { connectCtc } from "@/lib/ctcConnection";

export function useCtcSnapshot(): CtcSnapshot | undefined {
  const [snapshot, setSnapshot] = useState<CtcSnapshot>();

  useEffect(() => {
    let disconnect: (() => void) | undefined;
    let timer: number;

    function connect() {
      disconnect?.();
      disconnect = connectCtc(setSnapshot, {
        onStatusChange(status) {
          if (status === "disconnected" || status === "error") {
            setSnapshot(undefined);
            window.clearTimeout(timer);
            timer = window.setTimeout(connect, 2000);
          }
        },
      });
    }

    // Strict Modeで破棄されるEffectからは接続を開始しない。
    timer = window.setTimeout(connect, 0);
    return () => {
      window.clearTimeout(timer);
      disconnect?.();
    };
  }, []);

  return snapshot;
}
