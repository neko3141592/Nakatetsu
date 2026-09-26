# 注目編成のHUD

`TrainHudController`が`TrainFocusController.FocusedTrain.Status`を毎フレーム読み、TextMeshProへ表示する。注目編成を切り替えると、HUDも同じ編成に切り替わる。

## 表示項目

| 項目 | 表示 | 取得元 |
| --- | --- | --- |
| 速度 | `45.2 km/h` | `TrainStatusController.TryGetSpeedMps`。Physicsの最新出力の絶対値をkm/hに変換 |
| ノッチ | `P1`〜、`N`、`B1`〜、`EB` | 有効運転台のマスコン操作位置 |
| レバーサー | `前`、`中`、`後` | 有効運転台のレバーサー操作位置 |
| 注目編成 | `1001F` | 注目編成の編成IDのみ |

速度はゲーム用の表示なので、TIMSの転送間隔を通さない。操作位置もマスコンから直接取得する。どの運転台を読むかは、現在のTIMSの有効運転台判定に従う。

`EB`はマスコンの非常位置を表し、保安装置などによる非常ブレーキ作動全般を表すものではない。取得できない速度・ノッチ・レバーサーは`--`と表示する。注目編成がない場合や編成IDを取得できない場合、`TrainInfo`は空欄にする。

ノッチの文字色は、Pが水色、Nが緑、Bがオレンジ、EBが赤。取得できない場合の`--`は白に戻す。各色は`TrainHudController`のInspectorの`Notch Colors`で変更できる。

## シーンでの接続

`Application/HUD/Canvas/TrainHUD`に`TrainHudController`を付ける。

| Inspector項目 | 接続先 |
| --- | --- |
| Focus Controller | `Application/Focus`の`TrainFocusController` |
| Speed Text | `TrainHUD/Speed` |
| Notch Text | `TrainHUD/Notch` |
| Reverser Text | `TrainHUD/Reverser` |
| Train Info Text | `TrainHUD/TrainInfo` |

文字の大きさ・配置・フォントは各TextMeshProで編集する。Controllerは文字列とノッチの文字色を更新する。日本語を含むため、日本語対応フォントを使用する。

NtLineには既存のReverser・Notchを使い、その下にSpeed・TrainInfoを追加する。左上基準のTrainHUDとCanvas Scalerはそのまま使う。接続を再設定する場合は、Playを停止してシーンを保存した後、`Nakatetsu > Setup > Connect NtLine HUD`を実行する。

## 文字のマテリアル

現在のHUDは`MPLUS2-Regular SDF.asset`内の`MPLUS2-Regular Atlas Material`を共有する。輪郭をくっきりさせ、濃紺の縁取りと右下への影を付ける。

| 設定 | 値 |
| --- | --- |
| Face Color | 白（ノッチの文字色をそのまま反映） |
| Face Softness / Dilate | `0` / `0.08` |
| Outline Color / Thickness | 濃紺 / `0.16` |
| Underlay Type | Normal |
| Underlay Color | 黒に近い紺、Alpha `0.65` |
| Underlay Offset X / Y | `0.35` / `-0.45` |
| Underlay Dilate / Softness | `0` / `0.18` |
| Lighting / Glow | 無効 |

このマテリアルを調整すると、同じマテリアルを参照する文字にまとめて反映される。
