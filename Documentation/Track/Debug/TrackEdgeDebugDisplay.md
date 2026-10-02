# エッジのデバッグ表示

NtLineシーンの `Debug/TrackEdges` に `TrackEdgeDebugDisplay` を配置している。
コード・マテリアル・テストは `Assets/Nakatetsu/Track/Debug` に置く。

- Sceneビュー：Gizmosを有効にすると、エッジの線と距離ラベルを表示する。
- Gameビュー：再生中はLineRendererと画面上のラベルで表示する。Gizmosの設定には依存しない。
- 距離ラベルは各エッジのNode A側を0mとして50m間隔。路線全体の累積キロ程ではない。
- ラベルは `エッジID  距離 m`。エッジ長が50mの倍数なら終点にもラベルを出し、それ以外は終点を超えない最後の50m地点まで表示する。
- 位置は `TrackEdgeCalculator` と実距離表から取得する。曲線・オフセット・勾配・逆向きに定義されたエッジにも対応する。配置したGameObjectのTransformは線路の座標に加算しない。

## Inspector

| 設定 | 用途 |
| --- | --- |
| Graph Asset | 表示する線路グラフ。NtLineGraphを設定済み |
| Game Camera | Gameのラベルの投影元。未指定ならMainCamera |
| Show In Scene / Game View | ビューごとの表示切り替え |
| Label Visible Distance M | ラベルを出すカメラからの距離。初期値500m、0で距離制限なし。遠方のラベルが重なる場合は短くする |
| Line Color / Width M / Height M | 線の色・太さ・地表からの高さ |
| Line Sample Interval M | 曲線を描く点の間隔。初期値5m |
| Label Color / Font Size / World Offset | 文字色・文字サイズ・ラベルの配置オフセット |

グラフの内容を変更した場合はコンポーネントのコンテキストメニュー `Rebuild Edge Debug Display` で表示を作り直す。
距離表や線形を評価できないエッジは警告を出して省略する。
生成した線・表示用マテリアルは実行時だけ保持し、Sceneアセットには保存しない。

## 検証

`TrackEdgeDebugSamplingTests` の6ケースで、短いエッジ・50m単位の終点・端数のある終点・曲線と勾配のある順方向／逆方向・距離表の欠損を確認する。
NtLineの1000mエッジで、201点の線と0〜1000mの21個のラベルの生成、Gameビューでの描画を確認済み。
