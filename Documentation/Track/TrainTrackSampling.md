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

`SetTrackPosition` は0号車中心の配置と経路リセットを行う。有効なEdge IDとEdge内距離を渡す。車両長は初回にTrainRootからコピーする。編成変更時は `TryConfigureConsist()` を呼び、再配置する。

## 計算

`Calculate` がStateと経路を更新し、照会は現在のStateを直接読む。`ApplyOutput` でのコピーは行わない。

車両中心間距離は隣接車の半車長を足して求める。別途の連結器間隔や、車体の剛体姿勢・受電器の高さは扱わない。

編成がまたがるEdge列を保持し、その間の接続は転轍機の現在状態から再解決しない。照会はこの保持経路を読むだけで、状態変更や遠方への経路探索は行わない。未初期化・保持経路外では取得できない。接続不能時の移動はEdge境界で止まる。

Graph再構築やテレポート時は `SetTrackPosition` で経路をリセットする。

## NtLineの確認用直線

`NtLineGraph.asset` には長さ400mの `preview-straight` を入れている。`TrainPresentationDebugSpawner` のPrefabが0号車中心をEdge内195mに配置し、編成前方をNode A方向（画面側）に向ける。生成した各車に `TrainBogiePresentation` を付け、毎フレーム前後台車の線路サンプルから車体の位置と向きを更新する。転轍機のない仮データなので端点で停止する。
