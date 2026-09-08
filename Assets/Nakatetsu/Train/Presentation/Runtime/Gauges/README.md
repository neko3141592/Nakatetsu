# CircularGaugeScale

`CircularGaugeScale` は、速度計・圧力計などで共用する uGUI 用の丸形目盛りコンポーネントです。

## 使い方

1. Canvas 配下に空の UI オブジェクトを作ります。
2. **Add Component > Nakatetsu > Gauges > Circular Gauge Scale** を追加します。
3. RectTransform をメーターの中心と大きさに合わせます。
4. `Ticks` と `Number Labels` を Inspector で設定します。

角度は12時方向を `0` 度とし、時計回りが正です。たとえば左下から右下まで240度なら、`Start Angle = -120`、`Sweep Angle = 240` です。

## Ticks

- `Minimum` / `Maximum`: 値の範囲
- `Interval`: 最小目盛りの値間隔（速度計なら km/h、圧力計なら使用する圧力単位）
- `Medium Interval` / `Major Interval`: 中・大目盛りになる値間隔。`0` で無効
- `Radius`: 目盛り外端の半径
- `Start Angle` / `Sweep Angle`: 開始角と描画角度
- `Minor` / `Medium` / `Major`: 各目盛りの長さ、太さ、色

大目盛りと中目盛りが重なる値では、大目盛りを優先します。

## Number Labels

数字ラベルは目盛りとは独立した値範囲、間隔、半径、開始角、描画角度を持ちます。たとえば目盛りを2 km/hごと、数字を20 km/hごとに設定できます。

- `Number Format`: C# の数値書式（`0`、`0.0`、`000` など）
- `Rotate With Scale`: 数字の上方向をメーター外側へ向ける設定
- `Label Font`: 任意の TextMesh Pro フォント。未指定時はTMP既定フォント

実行中は、`Ticks` または `Labels` プロパティへ設定構造体を代入すると表示が更新されます。
