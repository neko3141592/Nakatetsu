# 速度センサーからTIMSへの速度公開

装置全体の送受信先と接続状況は [Bus・タグ接続一覧](BusTagMap.md) を参照。

## 責務

| 部品 | 役割 |
| --- | --- |
| `SpeedSensor`（Train / Equipment） | Simulationから物理速度を受け取り、測定速度を出力する。TIMSやPhysicsのControllerを参照しない |
| `SpeedSensorTimsBusSource`（TIMS / Speed） | 同じGameObjectのセンサーを読み、所属車両のLocalBusへ公開する |
| `TimsSpeedController`（TIMS / Speed） | LocalBusから有効な測定速度を選び、MasterBusへ公開する |

最初の測定モデルは符号付き物理速度の絶対値。速度・時間の積分、誤差、遅延、空転の補正は持たない。純粋な `SpeedSensorLogic` が入力の有効性と有限値を確認し、m/sで出力する。

## 配置

`Equipment/SpeedSensor/Prefabs/SpeedSensor.prefab` にセンサー、`TrainEquipmentAssignment`、専用BusSourceを配置した。Tc1・Tc2・M・Tの `additionalEquipmentPrefabs` に登録してあり、Equipment Builderが車両indexを割り当てる。独自の車両定義でもこのPrefabを追加すればよい。初期構成では1車両につきセンサー1台を想定する。

`TimsCommunicationController` は同じGameObjectの `TimsSpeedController` を取得し、なければ実行時に追加する。既存のTIMS GameObjectにも適用できる。Scene全体の検索はせず、編成のTrainRootと自車の割り当てを使う。

## 更新順

```text
TrainSimulationController
  → 前ステップのTrainPhysics.Context.Outputの速度を全センサーへ入力
  → センサー群のCollectInput → Calculate → ApplyOutput
  → TIMSが全BusSourceを収集してLocalBusを更新
  → TimsSpeedControllerがLocalBusを読み、MasterBusを更新
  → 通常の車載装置群の入力収集・計算・反映
  → 物理モデルとTrainPhysicsを更新
```

速度センサーは通常のEquipment更新群から除き、通信前の測定群で1回だけ処理する。SimulationからTIMS型への逆参照は追加していない。センサーもBusSourceも独自のUpdateで時間を進めない。

物理入力は各測定ステップで必要であり、供給されなければ測定は無効になる。Physicsが存在しない／無効な場合も速度不明とする。初期のPhysics速度0は有効な停止速度として扱う。Physicsの確定済みOutputを使うため、新しい物理計算結果が見えるのは次ステップの測定時である。測定後のLocalBus→MasterBusにはさらに1ステップの待ちを加えない。

## タグ契約

| Bus | Device / Item | 型・単位 | 意味 |
| --- | --- | --- | --- |
| 各車LocalBus | `SpeedSensor / MeasuredSpeedMps` | Float、m/s | 非負の測定速度。無効ならタグなし |
| MasterBus | `Train / SpeedMps` | Float、m/s | 編成として採用した測定速度 |
| MasterBus | `Train / SpeedKmh` | Float、km/h | 同じ値を3.6倍。既存速度計との互換出力 |
| MasterBus | `Train / HasValidSpeed` | Bool | 速度を取得できるか。0 km/hとは別の状態 |

`TimsSpeedController` は車両indexの小さい順に調べ、最初の有効な値を採用する。そのセンサーが取得不能なら次の車両へ切り替える。有効運転台の位置には依存しない。複数センサーの平均・多数決・不一致診断は初期実装には含めない。

欠損、型不一致、負の測定速度、NaN、Infinity、km/h変換でオーバーフローする値は採用しない。全車が取得不能なら両方の速度タグを削除し、`HasValidSpeed = false` とする。以前の速度を残したり、速度不明を0に置き換えたりしない。

BusSource無効化時は、自身が送信したLocalBusのタグを削除する。MasterBusへの反映は次の通信収集時。通常の `CollectInputSources()` は全車の収集後に速度を集約する。低レベルの `CollectSources()` だけを呼ぶ場合はLocalBus収集までで、MasterBus集約を別途呼ぶ必要がある。

## 利用側と対象範囲

既存 `TimsSpeedMeter` は `Train/SpeedKmh` を読むため、表示用タグはこの経路で供給される。速度不明時には既存の欠損表示になる。m/sが必要な装置は `TryGetFloat(Train/SpeedMps)` の取得成否、または `HasValidSpeed` を併せて使う。

EBの入力Adapterは `Train/SpeedMps` を読み、既定では絶対速度5 km/h以上のときに無操作監視を行う。5 km/h未満や速度入力欠落時はタイマーとEB要求をリセットする。詳細は [EbStatus.md](EbStatus.md) を参照。ATC・力行制御の入力AdapterとVVVFへの既存の物理速度入力は別途接続する。

検証対象は絶対値、停車と不明の区別、同じ測定ステップでの公開、センサーの二重更新防止、物理入力消失、車両別送信、センサー切替、Prefab生成、複数編成の分離。実際の画面描画・車両走行の目視確認は別途行う。

2026-09-15、Unity 6000.4.0f1の独立した検証用プロジェクトで、速度関連および既存EBのEditModeテスト45件成功・失敗0件。作業中のEditorを閉じずに自作アセットをコピーし、描画不要の最小パッケージ構成で実行した。C#・Prefab・車両定義の一致、GUID参照、`git diff --check` を確認した。新しい速度タグの公開によって既存EBの有効運転台判定・状態公開が変わらないことも確認している。
