# 電流計

Canvas配下に `Display/CurrentGauge/Prefabs/TimsCurrentGauge.prefab` を配置する。

- 左は回生（黄色）、右は力行（水色）。両方とも0から上へ伸びる縦バー。
- 初期目盛りは0〜1500 A。`Maximum Current A` で上限、`Height` で描画高さを設定する。
- `Regen Width` / `Power Width` と各 `Position X` でバー幅・横位置を個別に指定する。色は各バーのImageで変更する。
- `Use Manual Current` がオンの間は `Manual Current A` を表示。正で力行、負で回生。初期値0 Aは仮入力。
- 下部に符号付きの整数値と単位Aを表示する。数値と背景の位置・サイズは各RectTransformで調整する。
- 仮入力をオフにすると、指定車両（Car Indexは0始まり）のLocalBusにあるDevice Name / Item Nameのfloat値を読む。正が力行・負が回生のA値を送る必要がある。未接続・未受信時はバー非表示、数値は「--」。

現時点では実装置の電流タグには接続していない。モーター電流の実効値と架線電流は異なるため、表示対象に合わせた送信元を接続する。

目盛りには圧力計と共通のVerticalGaugeScaleを使用し、左側だけに表示する。右側の目盛りは生成しない。

Prototype/Timsシーン上の圧力計に合わせ、描画高さ514、バー幅60、数値文字サイズ40、目盛り文字サイズ24に設定。背景色・数値フォント・目盛りの太さと長さも同じ設定を使用する。電流上限1500 Aに合わせて目盛り間隔を比例換算している。
