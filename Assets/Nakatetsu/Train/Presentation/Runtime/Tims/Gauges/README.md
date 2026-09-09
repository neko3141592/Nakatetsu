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
- `Label Font`: 任意の TextMesh Pro フォント。未指定時はTMP既定フォント

数字ラベルはゲージ本体の回転にかかわらず、常に画面の鉛直上向きで表示されます。

実行中は、`Ticks` または `Labels` プロパティへ設定構造体を代入すると表示が更新されます。

## TimsSpeedMeter

`TimsSpeedMeter` は `SpeedMeter` の親オブジェクトへ追加し、TIMS MasterBus の値から速度計を更新します。

- `Needle` と `Speed` は同名の子オブジェクトから自動取得できます。
- `Speed Binding` は針と速度数字に使う数値タグです。`float` と `int` の両方に対応します。
- `ATC Speed Binding` は点灯させるATC現示速度、`ATC Validity Binding` は現示の有効状態です。
- ATC三角は0 km/hから `Maximum Atc Marker Speed Kmh` まで、`Atc Marker Interval Kmh`（既定5 km/h）ごとに自動生成されます。
- ATC速度または有効状態を取得できない場合、すべての三角を消灯スプライトにします。
- 半径を含めて作成した `Atc Marker On/Off Sprite` を指定し、その画像サイズを `Atc Marker Size` に設定します。

旧プロジェクトと同じ既定タグは `ATC.PatternAllowSpeedKmh` と `ATC.HasValidPattern` です。走行速度タグはプロジェクト側の送信名に合わせて `Speed Binding` を変更してください。
