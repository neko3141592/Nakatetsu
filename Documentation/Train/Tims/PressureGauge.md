# BC圧バー

`Assets/Nakatetsu/Train/Equipment/Tims/Display/PressureGauge/Prefabs/TimsPressureGauge.prefab` をCanvasの子に配置する。

- `Tims` に編成の `TimsCommunicationController` を設定する。
- `Car Index` に対象車両を指定する（先頭車は0）。
- `Maximum Pressure KPa` が最大長になる圧力。初期値は500 kPa。
- `Height` で圧力計の高さを指定する。上下の数字が収まる余白を引き、目盛りの高さとバーの最大長を計算する。
- `BC Bar Width` / `BC Bar Position X` でBCバーの幅・横位置を設定する。
- `MR Bar Width` / `MR Bar Position X` でMRバーの幅・横位置を設定する。BCと同じく下端からMR圧に比例して伸縮する。
- `Left Scale` の横位置と幅はRectTransformで設定する。
- `Use Manual Mr Pressure` をオンにして `Manual Mr Pressure KPa` で仮のMR圧を設定する。MR装置は未実装で、初期値700 kPaは表示確認用。
- 将来Bus接続する場合は仮入力をオフにし、`Mr Device Name` / `Mr Item Name` を実際の送信タグに合わせる。未受信時はMRバーを非表示にする。
- 上部のBCラベルはBCバーの色、MRラベルは正常範囲の色に追従する。`Label Gap` / `Label Height` で上部の間隔・高さを指定する。
- `Background` は背面に置いた黒いImage。位置・幅・高さはRectTransformで自由に設定でき、圧力計のレイアウト計算から独立している。
- バーの色は `BC Bar` のImageで変更する。

LocalBusの `Brake.MeasuredBCPressureKPa` を読み、下端から上へ圧力に比例して伸縮する。0 kPaで高さ0、250 kPaで半分、500 kPa以上で最大長（初期設定時）。値が取得できない場合はバーを非表示にする。

左側の `Left Scale` は速度計の `CircularGaugeScale` を参考にした `VerticalGaugeScale` で描画する。

- 小目盛り10 kPa、中目盛り50 kPa、大目盛りと数字100 kPa刻みが初期設定。
- 目盛りの間隔・長さ・太さ・色、数字の間隔・フォント・サイズはInspectorで設定できる。
- 下端が0 kPa、上端がバーの最大圧。実行中の上端は `Maximum Pressure KPa` に追従する。
- バーと目盛りの縦位置・高さのみ自動計算される。黒い背景は自動変更しない。

BC Pressure / MR Pressureに現在圧を最も近い5 kPa単位に四捨五入して右寄せ表示する（例：672→670、673→675）。バーの長さは元の圧力値を使用する。未受信時は「--」。MRの数字は正常範囲と同じ色。各PressureとUnitのRectTransformで位置・幅・高さ、TMPコンポーネントで文字サイズを変更できる。

## MR正常範囲

`Mr Normal Minimum KPa` / `Mr Normal Maximum KPa` で正常範囲の下限・上限を指定する（初期値600〜800 kPaは仮設定）。`Mr Normal Range Width` で帯の幅、`Mr Normal Range Color` で帯とMR文字の色を指定する。

帯はMR実圧バーの背面に配置し、横方向の中心を揃える。実圧とは独立した固定範囲として表示する。実圧バーの色は引き続きMR BarのImageで設定する。

`Mr Normal Range Offset` のX・Yで、正常範囲の計算位置から帯だけを移動できる。Xは右、Yは上が正。初期値(0, 0)では従来の位置を維持する。
