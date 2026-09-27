# TIMS 時刻・速度テキスト

`TimsTextReadout`（Add Component → Nakatetsu → Tims → Text Readout）を表示用オブジェクトに追加する。

- `Tims`：未指定なら親の `TimsCommunicationController`、または所属 `TrainRoot` 内から自動取得する。実行時に生成される画面には `TimsMonitorOutput` から所有元のTIMSを接続する。別編成のTIMSは検索しない。
- `World Time Source`：未指定ならシーン内の有効な `IWorldTimeSource`（`ApplicationSimulationController`）を自動取得する。時計が後から生成された場合は1秒間隔で再検索する。
- `Time Text` / `Speed Text`：更新する `TMP_Text` を手動指定する。不要な欄は未指定でよい。

時刻はゲーム内の世界時刻を24時間表記・秒単位で `１４時０１分３３秒` のように表示する。時間を進める処理は持たず、ゲームの一時停止・再生倍率に従う。

速度はTIMS MasterBusの `Train.SpeedKmh` を整数に四捨五入し、単位なしの全角数字（例：`７７`）で表示する。無効値や参照未指定は全角ハイフンで表示する。

変更するのは指定されたテキストの `.text` のみ。フォント、色、サイズ、配置は変更しない。テキスト参照だけ手動指定すればよく、データ取得元は必要な場合に手動指定で上書きできる。全角数字と「時・分・秒」を収録したTMPフォントを使用する。

距離の参照・更新は今回実装しない。既存の距離テキストにも触れない。Scene・Prefabへの追加と参照設定は手動で行う。
