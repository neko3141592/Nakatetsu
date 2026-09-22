# Edge距離LUTのエディター生成

## 実行方法

1. Projectで`TrackGraphAsset`を選ぶ。
2. Inspectorの`Integration Step (m)`を設定する（既定0.05m）。
3. `Compile Distance Maps`を押す。

Graph内のGeometry・Node・Edgeを検証し、全Edgeの生成が成功したときだけ、Graph内の各`TrackEdgeDefinition.distanceMap`を置換してAssetを保存する。Play Mode中は実行できない。失敗理由はConsoleに表示し、既存LUTは保持する。変更はUndoに登録する。Undo後の状態を永続化する場合はAssetを保存する。

これは明示的な線路データのコンパイルであり、C#の再コンパイル・Awake・毎フレームの自動生成ではない。独立した`TrackEdgeAsset`には反映しない。現在のGraphはEdge/Geometryの定義を内包しているため、その内包データが対象となる。

## 方式

- `TrackEdgeCalculator.TryEvaluateAtGeometryDistance`で、LUTを使わず位置を評価する。実行時評価もこの計算を共有する。
- Node AからBへGeometry距離を刻み、隣接位置の`Vector3.Distance`を`double`に累積する。位置と各弦長はfloatであり、全計算がdoubleではない。
- 微分の大きさを積分する方式ではなく、旧プロジェクトと同じ折れ線近似。
- 旧版の実距離等間隔への再サンプリングは行わず、今回は各積算点を距離ペアとして保存する。水平・縦断・Offsetの区間境界も刻み点に含める。
- 最初はEdge実距離0、最後はNode B。逆向きEdgeでもEdge実距離は昇順、Geometry距離だけ降順になる。
- Geometry距離の刻みはdoubleの区間起点＋ステップ番号で計算し、floatの反復加算による刻みのずれを避ける。
- LUTの保存値はfloat。単調増加を表現できない刻み、非有限値、評価失敗、水平接線の縮退、同一点への積算はエラー。
- 1 Edgeあたり最大1,000,000点。超過時は刻みを大きくする必要がある。

## 定義の前提

水平区間はGeometry起点から連続した距離順、Offset区間はGeometry距離の昇順で、Edge全体を隙間・重複なく覆う。オフセットなしの場合も、offsetM=0の一定区間を明示する。隣接Offsetの値が0.0001mを超えて不連続なら生成に失敗する。

任意の曲線の連続性やサンプリング誤差を完全に保証する処理ではない。特に既存の緩和曲線の近似角による区間接続の問題は、このLUT生成では修正しない。細部を捉えるための刻み幅は利用側が設定する。

Geometry・Edge範囲・Offsetを変更したら再コンパイルする。古いLUTの自動失効判定はまだない。

## 責務

- `TrackEdgeCompiler`：1 EdgeのLUTを一時生成する。定義は変更しない。
- `TrackGraphCompiler.TryBuildDistanceMaps`：既存のグラフ検証後に全Edgeを生成し、成功時だけ結果一式を返す。
- `TrackGraphAssetEditor`：Inspectorボタン、Undo登録、Assetへの反映・保存を担当する。

既存`TrackGraphCompiler.TryCompile`の検索・参照検証APIは変更せず、LUT生成は新APIで明示する。
