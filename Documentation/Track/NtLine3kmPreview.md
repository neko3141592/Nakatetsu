# NtLine 3km試験線

`Assets/Nakatetsu/Track/NtLine/Data/NtLineGraph.asset` は、既存の `preview-straight`（0〜1000m）のIDと始点を維持し、同じGeometryへ4 Edgeを追加した。新しい区間は曲率0から半径1000mの円曲線へ入り、再び曲率0へ戻る。高さと横オフセットは0m。

| Edge ID | Geometry距離 | 線形 | Edge実距離の目安 |
| --- | --- | --- | ---: |
| `preview-straight` | 0〜1000m | 既存の直線・円曲線・直線 | 1000m |
| `preview-easement-in` | 1000〜1250m | 緩和曲線・半径1000mへ | 250.390m |
| `preview-curve-2` | 1250〜1650m | 半径1000mの円曲線 | 400.000m |
| `preview-easement-out` | 1650〜1900m | 緩和曲線・直線へ | 251.039m |
| `preview-tail-straight` | 1900〜3000m | 直線 | 1100m |

合計は距離表の積算で約3001.428m。4つの中間Nodeはそれぞれ `Always` の固定接続1組を持つ。`NtLine.unity` の `TrackGraph` に `TrackConnectionController` を配置してあり、車両はこれを通って隣のEdgeへ進む。始点・終点のNodeは未接続の線路端として扱う。

距離表はGeometry距離1m刻みで再計算した。Graphのコンパイルと接続解決に成功し、4境界で評価位置差と接線角度差が0であることを確認した。既存の初期位置は1本目の200mで、10両編成が線路内に収まる。

Scene・Gameのラインと50mラベルは `Debug/TrackEdges` の `TrackEdgeDebugDisplay` がGraphを参照する。Geometryやオフセットを編集したらGraph Inspectorの `Compile Distance Maps` を実行し、表示コンポーネントの `Rebuild Edge Debug Display` も実行する。
