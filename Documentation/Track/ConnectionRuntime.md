# Connectionの実行時状態

`TrackConnectionDefinition`は接続の定義、`TrackConnectionController`は全列車が共有する転轍機の状態と要求窓口を持つ。
Controllerは`TrackGraphController`と同じGameObjectに1つ配置する。GraphのAwake完了後、Startで初期化する。
この変更ではSceneへのコンポーネント追加は行っていない。

## 定義と初期化

- 1つのNodeに1つのConnectionを定義する。ConnectionがないNodeでは次Edgeを解決しない。
- 固定接続は`Always`の1ペア。転轍機は`Normal`と`Reverse`の2ペアで、共通Edgeを1本持つ。
- `pairId`はConnection内で一意にする。各ペアの両EdgeはそのNodeの端点であり、`connectedEdgeIds`にも含める。
- Graphのコンパイル時と設備Controller初期化時にペアの妥当性を検証する。
- 初期位置はControllerの`initialPosition`（既定は定位）。転轍機ごとの初期位置設定は未対応。
- 初期化時に定義をコピーし、以後はAssetを変更せずに実行時状態を更新する。
- `TryInitialize`は全転轍機を初期位置へ戻す。Graph定義を変更・再構築した場合は、走行を止めて設備Controllerも再初期化する。

## 連動装置からの操作

```csharp
if (!connections.TryRequestPosition(connectionId, TrackSwitchPosition.Reverse, out var error))
{
    // 要求が受理されなかった理由を扱う。
}

if (connections.TryGetState(connectionId, out var state))
{
    // RequestedPosition: 要求位置
    // ActualPosition: 確定した実際位置。転換中はUnknown。
    // IsMoving: 転換中かどうか
}
```

進路鎖錠や軌道占有に基づく操作可否の判定は、呼び出し側の連動装置が行う。
このControllerは連動装置そのものではなく、受理した要求と設備状態を管理する。
固定接続への要求、不明なID、Unknownへの要求、転換中の逆方向への要求は拒否する。同じ要求の再送は許容する。

`completeRequestsImmediately`の既定値はtrueで、受理した要求をその場で確定する。
falseの場合は要求受付後に未確定・転換中となり、転換動作側が`TryConfirmPosition(connectionId, position, out error)`を呼ぶまで維持する。
この段階では時間に応じた転換動作は実装していない。要求と異なる位置の完了通知は拒否する。

## 列車側からの参照

`TrainTrackPositionController.TryResolveNextEdge(nodeId, incomingEdgeId, out nextEdgeId, out error)`は、参照中のGraphと同じGameObjectの設備Controllerへ問い合わせる。
固定接続は常に有効。転轍機は要求位置ではなく確定した実際位置のペアを使う。
ペアは双方向に解決するが、選択されていない分岐側からの進入、転換中、未初期化では失敗する。
複数列車から問い合わせても状態は共通であり、参照で位置を変更することはない。

現在のTrackPositionのEdge跨ぎループへの呼び出し組み込みは、別途行う。
