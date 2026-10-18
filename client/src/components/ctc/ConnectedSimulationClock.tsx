"use client";

import { useEffect, useState } from "react";

import { SimulationClock } from "@/components/ctc/SimulationClock";
import type { WorldTimeInformation } from "@/gen/ctc_pb";
import { connectCtc } from "@/lib/ctcConnection";

export function ConnectedSimulationClock() {
  const [worldTime, setWorldTime] = useState<WorldTimeInformation>();

  useEffect(() => {
    let disconnect: (() => void) | undefined;
    let timer: number;

    function connect() {
      disconnect?.();
      disconnect = connectCtc((snapshot) => setWorldTime(snapshot.worldTime), {
        onStatusChange(status) {
          if (status === "disconnected" || status === "error") {
            setWorldTime(undefined);
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

  return <SimulationClock worldTime={worldTime} />;
}
