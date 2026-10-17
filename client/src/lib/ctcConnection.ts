import { fromBinary } from "@bufbuild/protobuf";
import { CtcSnapshotSchema, type CtcSnapshot } from "@/gen/ctc_pb";

export const CTC_WEBSOCKET_URL = "ws://127.0.0.1:8080/ws";

export type CtcConnectionStatus = "connecting" | "connected" | "disconnected" | "error";

type ConnectionOptions = {
  onStatusChange?: (status: CtcConnectionStatus) => void;
  onError?: (message: string) => void;
};

export function connectCtc(
  onSnapshot: (snapshot: CtcSnapshot) => void,
  options: ConnectionOptions = {},
): () => void {
  const socket = new WebSocket(CTC_WEBSOCKET_URL);
  socket.binaryType = "arraybuffer";
  let disposed = false;
  let failed = false;

  socket.onopen = () => {
    if (!disposed) options.onStatusChange?.("connected");
  };

  socket.onmessage = (event: MessageEvent<unknown>) => {
    if (disposed) return;

    let snapshot: CtcSnapshot;
    try {
      if (!(event.data instanceof ArrayBuffer)) {
        throw new Error("バイナリ以外のメッセージを受信しました。");
      }
      snapshot = fromBinary(CtcSnapshotSchema, new Uint8Array(event.data));
    } catch (error) {
      failed = true;
      options.onStatusChange?.("error");
      options.onError?.(
        error instanceof Error ? error.message : "CTC状態の復元に失敗しました。",
      );
      socket.close(1003, "Invalid CTC snapshot");
      return;
    }

    onSnapshot(snapshot);
  };

  socket.onerror = () => {
    if (disposed) return;
    failed = true;
    options.onStatusChange?.("error");
    options.onError?.("Goサーバーへ接続できません。サーバーの起動と接続許可を確認してください。");
  };

  socket.onclose = (event) => {
    if (disposed || failed) return;
    options.onStatusChange?.("disconnected");
    if (!event.wasClean) options.onError?.("Goサーバーとの接続が切断されました。");
  };

  return () => {
    disposed = true;
    socket.close();
  };
}
