# 車両上の線路位置を取得する

`TrainTrackPositionController.TryGetTrackSample(carIndex, offsetFromCarCenterM, out sample)` を使う。

- 車両Indexは定義順の0始まり。
- オフセットは車両中心からの線路沿い距離[m]。編成の固定前方向が正で、逆走時も変わらない。
- 台車位置には `± bogieCenterDistanceM / 2` を渡す。
- 結果はワールド位置・姿勢・接線、Edge ID、Node AからのEdge内距離、編成前方向基準の勾配。
- 取得できない場合は `false`。エラー情報やログは追加しない。

## 初期化

TrainRootに編成定義、Controllerに初期化済みのGraphを設定してから呼ぶ。

```csharp
trackPosition.SetTrackPosition("edge-001", 50f, true);
if (trackPosition.TryGetTrackSample(0, 8f, out var sample))
{
    Vector3 position = sample.Position;
}
```

`SetTrackPosition` は0号車中心を配置し、現在の分岐接続から出力を再計算する。有効なEdge IDとEdge内距離を渡す。車両長と台車中心間距離は初回にTrainRootからコピーする。編成変更時は `TryConfigureConsist()` を呼び、再配置する。

## 計算

`Calculate` は0号車中心のEdge位置を進め、Graphと現在の分岐接続から台車位置・向きと占有Edge列を再計算する。`ApplyOutput` でのコピーは行わない。

車両中心間距離は隣接車の半車長を足して求める。別途の連結器間隔や、車体の剛体姿勢・受電器の高さは扱わない。

`Settings.CarOffsets` は各車両中心と前後台車の、0号車中心からの相対距離を保持する。`Output.TryGetBogies(carIndex, out front, out rear)` は各台車の位置と向きを返す。`Output.OccupiedEdges` は1号車前台車から最後尾後台車までの区間を、前から後ろへの順で `EdgeId` とGeometry上の開始・終了距離として保持する。Geometry距離が逆向きなら開始値は終了値より大きくなる。

占有区間を別の処理へ渡すときは `TryGetOccupiedEdges(destination)` でコピーできる。出力が無効なら `false` を返し、`destination` は変更しない。占有範囲は台車端基準なので、車体のオーバーハングは含まない。

`TrainTrackPositionController` はTrack側の `ITrackOccupancySource` を実装する。世界のtickで全列車を移動させた後、`TrackCircuitSimulationController.RefreshOccupancy()` が登録済みソースから区間を読み、Graphに定義された軌道回路との重なりを計算する。どれか1列車の位置を取得できないときは、登録された全軌道回路を占有とする。未登録の回路IDも `IsOccupied` は占有として返す。

接続先は照会・更新のたびに現在の分岐状態から解決する。走行経路の履歴は保持しないため、編成が分岐を占有している間の転換禁止が前提となる。接続先が未確定なら `TryGetBogies` と `TryGetOccupiedEdges` は失敗し、軌道回路は全回路を占有とする。接続不能時の先頭車中心の移動はEdge境界で止まる。

Graph再構築やテレポート時は `SetTrackPosition` で再計算する。

## NtLineの確認用線路

`NtLineGraph.asset` は転轍機21・22で直線本線と待避線を接続する、1面2線の試験線。本線経由は全長2,520mで、駅の両側に各1kmの自動閉そく区間を置く。配線と各定義は [NtLine 島式ホーム試験線](NtLineTestTrack.md) を参照。

`NtLine.unity` の `TrainTrackPositionDebugInitializer` は0号車中心を `nt-approach` の400mに置き、編成前方をNode A→Bへ向ける。生成した各車の `TrainBogiePresentation` は前後台車の線路サンプルから車体位置と向きを更新する。接続にはSceneの `Track/Graph` 上にある `TrackConnectionController` を使う。
