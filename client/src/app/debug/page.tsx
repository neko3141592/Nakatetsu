"use client";

import { toJsonString } from "@bufbuild/protobuf";
import { useEffect, useState } from "react";
import { NT01Diagram } from "@/components/debug/NT01Diagram";
import { CtcSnapshotSchema, TurnoutPosition, type CtcSnapshot } from "@/gen/ctc_pb";
import {
  connectCtc,
  getCtcWebSocketUrl,
  type CtcConnectionStatus,
} from "@/lib/ctcConnection";

const statusLabels: Record<CtcConnectionStatus, string> = {
  connecting: "接続中",
  connected: "接続済み",
  disconnected: "切断",
  error: "接続・受信エラー",
};

export default function DebugPage() {
  const [status, setStatus] = useState<CtcConnectionStatus>("connecting");
  const [snapshot, setSnapshot] = useState<CtcSnapshot | null>(null);
  const [receivedCount, setReceivedCount] = useState(0);
  const [lastReceivedAt, setLastReceivedAt] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [attempt, setAttempt] = useState(0);
  const [webSocketUrl, setWebSocketUrl] = useState("");

  useEffect(() => {
    // 開発時のStrict Modeでも、破棄されたEffectから接続しない。
    let disconnect: (() => void) | undefined;
    const timer = window.setTimeout(() => {
      setWebSocketUrl(getCtcWebSocketUrl());
      disconnect = connectCtc(
        (next) => {
          setSnapshot(next);
          setReceivedCount((count) => count + 1);
          setLastReceivedAt(new Date().toLocaleTimeString("ja-JP"));
        },
        { onStatusChange: setStatus, onError: setError },
      );
    }, 0);

    return () => {
      window.clearTimeout(timer);
      disconnect?.();
    };
  }, [attempt]);

  function reconnect() {
    setStatus("connecting");
    setSnapshot(null);
    setReceivedCount(0);
    setLastReceivedAt(null);
    setError(null);
    setAttempt((value) => value + 1);
  }

  const circuits = Object.entries(snapshot?.circuitsById ?? {}).sort(([a], [b]) => a.localeCompare(b));
  const trains = Object.entries(snapshot?.trainsById ?? {}).sort(([a], [b]) => a.localeCompare(b));
  const turnouts = Object.entries(snapshot?.turnoutsById ?? {}).sort(([a], [b]) => a.localeCompare(b));
  const occupiedCount = circuits.filter(([, circuit]) => circuit.isOccupied).length;
  const circuitIDsByTrainID = new Map<string, string[]>();
  for (const [circuitID, circuit] of circuits) {
    if (!circuit.trainId) continue;
    const ids = circuitIDsByTrainID.get(circuit.trainId) ?? [];
    ids.push(circuitID);
    circuitIDsByTrainID.set(circuit.trainId, ids);
  }

  return (
    <main className="mx-auto w-full max-w-6xl space-y-6 p-6 text-sm sm:p-10">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-2xl font-semibold">CTC 接続確認</h1>
          <p className="mt-2 text-zinc-600 dark:text-zinc-400">Goから受信した最新の追跡状態を表示します。</p>
        </div>
        <button
          type="button"
          onClick={reconnect}
          className="rounded-xs border border-zinc-400 px-4 py-2 hover:bg-zinc-100 focus-visible:outline-2 focus-visible:outline-offset-4 dark:hover:bg-zinc-800"
        >
          再接続
        </button>
      </header>

      <section aria-label="接続情報" className="rounded-xs border border-zinc-300 p-5 dark:border-zinc-700">
        <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <div><dt className="text-zinc-600 dark:text-zinc-400">接続状態</dt><dd role="status" className="mt-1 font-semibold">{statusLabels[status]}</dd></div>
          <div><dt className="text-zinc-600 dark:text-zinc-400">受信回数</dt><dd className="mt-1 tabular-nums">{receivedCount}</dd></div>
          <div><dt className="text-zinc-600 dark:text-zinc-400">最終受信</dt><dd className="mt-1 tabular-nums">{lastReceivedAt ?? "未受信"}</dd></div>
          <div><dt className="text-zinc-600 dark:text-zinc-400">接続先</dt><dd className="mt-1 break-all font-mono text-xs">{webSocketUrl}</dd></div>
        </dl>
      </section>

      {error && <p role="alert" className="rounded-xs border border-red-400 p-4 text-red-700 dark:text-red-300">{error}</p>}
      {status !== "connected" && snapshot && <p>以下は最終受信時の状態です。現在は更新されていません。</p>}

      <NT01Diagram
        circuitsById={snapshot?.circuitsById}
        trainsById={snapshot?.trainsById}
        turnoutsById={snapshot?.turnoutsById}
      />

      {!snapshot ? (
        <p className="py-8 text-zinc-600 dark:text-zinc-400">{status === "connecting" || status === "connected" ? "CTCの状態を受信待ち…" : "未受信です。Goサーバーを起動して再接続してください。"}</p>
      ) : (
        <>
          <section aria-label="CTC状態" className="rounded-xs border border-zinc-300 p-5 dark:border-zinc-700">
            <dl className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div><dt className="text-zinc-600 dark:text-zinc-400">CTC初期化</dt><dd className="mt-1">{snapshot.isInitialized ? "完了" : "未完了（Unityからの初期登録待ち）"}</dd></div>
              <div><dt className="text-zinc-600 dark:text-zinc-400">追跡の再設定</dt><dd className={`mt-1 ${snapshot.needsReset ? "font-semibold text-red-700 dark:text-red-300" : ""}`}>{snapshot.needsReset ? "必要" : "不要"}</dd></div>
              <div><dt className="text-zinc-600 dark:text-zinc-400">列車数</dt><dd className="mt-1">{trains.length}</dd></div>
              <div><dt className="text-zinc-600 dark:text-zinc-400">軌道回路数 / 在線数</dt><dd className="mt-1">{circuits.length} / {occupiedCount}</dd></div>
            </dl>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold">列車</h2>
            <p className="text-zinc-600 dark:text-zinc-400">軌道回路位置は、TrainIDを保持する最後尾側の基準回路です。</p>
            <div className="overflow-x-auto rounded-xs border border-zinc-300 dark:border-zinc-700">
              <table className="w-full text-left">
                <thead className="bg-zinc-100 dark:bg-zinc-900"><tr>{["TrainID", "編成ID", "編成番号", "列番", "運用番号", "軌道回路位置"].map((label) => <th key={label} scope="col" className="whitespace-nowrap p-3 font-medium">{label}</th>)}</tr></thead>
                <tbody>
                  {trains.map(([id, train]) => (
                    <tr key={id} className="border-t border-zinc-200 dark:border-zinc-800">
                      <td className="p-3 font-mono">{id}</td><td className="p-3">{train.consistId ?? "不明"}</td><td className="p-3">{train.consistNumber || "未設定"}</td><td className="p-3">{train.trainNumber || "未設定"}</td><td className="p-3">{train.operationNumber || "未設定"}</td>
                      <td className="whitespace-nowrap p-3 font-mono">{circuitIDsByTrainID.get(id)?.join(", ") || "不明"}</td>
                    </tr>
                  ))}
                  {trains.length === 0 && <tr><td colSpan={6} className="p-4 text-zinc-600 dark:text-zinc-400">登録された列車はありません。</td></tr>}
                </tbody>
              </table>
            </div>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold">軌道回路</h2>
            <p className="text-zinc-600 dark:text-zinc-400">TrainIDは最後尾側の基準回路にだけ保持されます。</p>
            <div className="max-h-96 overflow-auto rounded-xs border border-zinc-300 dark:border-zinc-700">
              <table className="w-full text-left">
                <thead className="sticky top-0 bg-zinc-100 dark:bg-zinc-900"><tr>{["軌道回路ID", "名称", "在線", "TrainID"].map((label) => <th key={label} scope="col" className="p-3 font-medium">{label}</th>)}</tr></thead>
                <tbody>
                  {circuits.map(([id, circuit]) => (
                    <tr key={id} className="border-t border-zinc-200 dark:border-zinc-800">
                      <td className="p-3 font-mono">{id}</td><td className="p-3">{circuit.displayName || id}</td><td className={`p-3 ${circuit.isOccupied ? "font-semibold text-amber-700 dark:text-amber-300" : ""}`}>{circuit.isOccupied ? "在線" : "非在線"}</td><td className="p-3 font-mono">{circuit.trainId || "—"}</td>
                    </tr>
                  ))}
                  {circuits.length === 0 && <tr><td colSpan={4} className="p-4 text-zinc-600 dark:text-zinc-400">CTCの初期登録後に表示されます。</td></tr>}
                </tbody>
              </table>
            </div>
          </section>

          <section className="space-y-3">
            <h2 className="text-lg font-semibold">転轍機</h2>
            <div className="max-h-96 overflow-auto rounded-xs border border-zinc-300 dark:border-zinc-700">
              <table className="w-full text-left">
                <thead className="sticky top-0 bg-zinc-100 dark:bg-zinc-900"><tr>{["転轍機ID", "実位置"].map((label) => <th key={label} scope="col" className="p-3 font-medium">{label}</th>)}</tr></thead>
                <tbody>
                  {turnouts.map(([id, turnout]) => (
                    <tr key={id} className="border-t border-zinc-200 dark:border-zinc-800">
                      <td className="p-3 font-mono">{id}</td>
                      <td className="p-3">{turnout.isMoving ? "転換中" : turnout.actualPosition === TurnoutPosition.NORMAL ? "定位" : turnout.actualPosition === TurnoutPosition.REVERSE ? "反位" : "位置不明"}</td>
                    </tr>
                  ))}
                  {turnouts.length === 0 && <tr><td colSpan={2} className="p-4 text-zinc-600 dark:text-zinc-400">転轍機の状態は未受信です。</td></tr>}
                </tbody>
              </table>
            </div>
          </section>

          <details className="rounded-xs border border-zinc-300 p-4 dark:border-zinc-700">
            <summary className="cursor-pointer font-medium">受信したSnapshot（JSON）</summary>
            <pre className="mt-4 max-h-96 overflow-auto text-xs">{toJsonString(CtcSnapshotSchema, snapshot, { prettySpaces: 2, alwaysEmitImplicit: true })}</pre>
          </details>
        </>
      )}
    </main>
  );
}
