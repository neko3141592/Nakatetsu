# Boolインジケーター配列

Canvas配下のRectTransformに `TimsBoolIndicatorDisplay` を追加する。

- `Tims`：表示対象編成のTimsCommunicationController。
- `Indicator Prefab`：Indicators/Prefabs/BoolIndicator.prefab。
- `Indicators`：表示順の設定配列。各要素でBus Target、Car Number、Device Name、Item Name、Background On Color、Label On Color、Labelを指定する。
- `Car Number`：1始まりの号車番号。LocalBusだけで使用する。
- `Vertical Spacing` / `Horizontal Spacing`：セル中心間の配置間隔（Canvas単位）。
- `Indicators Per Column`：縦に並べる個数。指定数ごとに右の列へ進む。

親RectTransformの左上を最初のインジケーターの中心として配置する。生成はAwake時のみ。配列・配置設定は再生前に設定する。生成後は各TimsBoolIndicatorがLateUpdateでBusのbool値を読み取る。

trueで指定の背景色・文字色、false・タグ未受信・TIMS未接続ではPrefabの消灯色で表示する。データ未受信時もラベルは残る。消灯色と形状・文字サイズはBoolIndicator Prefab側で設定する。

`Prefabs/TimsBoolIndicatorDisplay.prefab` に画像の10項目（左列：非常運転、ATC電源、ATC、ATC常用、ATC非常／右列：ATC開放、構 内、非 設、停通防止、故 障）を設定済み。5行×2列で生成する。Device Name / Item Nameは空欄で、すべて消灯表示。

`Prefabs/TimsOperationIndicatorDisplay.prefab` は耐雪ブレーキ、定速運転、回生開放、勾配起動、高加速制御、ATO、TASC電源、TASCパターン、TASCブレーキ、TASC切の10項目を縦1列で表示する。データバインディング・点灯色は未設定で、すべて消灯表示。

`Prefabs/TimsPlatformDoorIndicatorDisplay.prefab` はBoolIndicatorWideを使い、定位置・ﾎｰﾑﾄﾞｱ連動・ﾎｰﾑﾄﾞｱ非連動を縦1列に生成する。中心間隔55、3個で折り返し。バインディング・点灯色は未設定で全項目消灯。
