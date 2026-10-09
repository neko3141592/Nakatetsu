# 車上ATCの入力・状態・出力

本書は、`Assets/Nakatetsu/Train/Equipment/Atc/`にある正式な車上ATCの受け渡しを記載する。名前空間は`Nakatetsu.Train.Equipment.Atc`とする。旧実装の入力説明は[Archiveの旧版](Archive/TrainAtcInput.md)に保存し、現在の型・関数の案内には使用しない。

全体の処理順と各Logicの更新担当は[車上ATC 全体処理仕様](TrainAtcProcessingFlow.md)、制動計算とブレーキ制御は[ブレーキパターン・ブレーキ出力仕様](TrainAtcBrakePatternSpecification.md)、関数の呼び出しは[状態遷移図](TrainAtcTransitions.drawio)を参照する。

## Contextの構成

`TrainAtcController.Context`から`TrainAtcContext`を参照する。Contextは次の情報を編成ごとに保持する。

| 項目 | 型・用途 |
| --- | --- |
| `Settings` | `TrainAtcSettings`。計算・制御用の設定値 |
| `Graph` | `TrackAtcGraphDefinition`。距離・接続・常設制限・勾配などの静的な路線定義 |
| `atcEdgesById` | Edge IDから`TrackAtcGraphEdge`を取得する辞書 |
| `Input` | `TrainAtcInput`。今回の入力スナップショット |
| `State` | `TrainAtcState`。工程ごとの状態と全体の正常性 |
| `Output` | `TrainAtcOutput`。TIMSへ渡す表示情報とブレーキ指令 |

占有・鎖錠などの地上装置の動的な状態は車上から直接参照しない。地上の進行可能範囲と防護方式は、受電器が取得した電文から読む。

## Controllerの参照と更新

`TrainAtcController`は編成のCommonに一つ配置し、次の参照を持つ。

| Inspectorの項目 | 参照先 |
| --- | --- |
| `Settings Asset` | `TrainAtcSettingsAsset` |
| `Front Receiver`・`Rear Receiver` | 固定前側・固定後側の`TrainAtcReceiverController` |
| `Cab Input Source` | `ITrainAtcCabInputSource`を実装するコンポーネント |
| `Door Input Source` | `ITrainAtcDoorInputSource`を実装するコンポーネント |
| `Mass Input Source` | `ITrainAtcMassInputSource`を実装するコンポーネント |
| `Brake Settings Input Source` | `ITrainAtcBrakeSettingsInputSource`を実装するコンポーネント |
| `Speed Sensor` | 符号付き測定速度を読む`SpeedSensor` |

InputSourceが未指定の場合は、同じGameObjectのコンポーネントから対応するインターフェースを探す。旧型からの変換処理は設けない。

| 関数 | 担当 |
| --- | --- |
| `CollectInput()` | 前回入力を消去し、操作・ドア状態・ブレーキ設定・車両質量・受電器の取り付け距離・測定速度・両端電文を取り込む |
| `Calculate(deltaTimeSeconds)` | 今回の経過時間をInputへ設定し、`TrainAtcLogic.Calculate(context)`を呼ぶ |
| `ApplyOutput(deltaTimeSeconds)` | ATCから外部へ直接書き込まない。TIMS側が計算済みOutputを読み取る |
| `SetReceivers(front, rear)` | 両端受電器を接続し、古いInputを消去する |
| `SetSpeedSensor(sensor)` | 測定速度の参照元を接続する |
| `TryInitializePosition(graph, frontPosition, rearPosition)` | 開始時の両端位置とGraphを設定する |
| `TryCorrectPosition(frontPosition, rearPosition)` | 位置不明時などに両端位置を明示的に補正する |

両端電文は`Clone()`した内容をInputに保持する。入力収集後に地上側の電文が更新されても、今回の計算中の入力内容は変わらない。未受信側はnullとし、前回の電文で置き換えない。

Controller無効時と`OnDisable()`ではInputを消去する。取得できなかった値を、初期値による正常なキー切・中立・停車として扱わない。

## 入力スナップショット

`TrainAtcInput`は次の情報を保持する。LogicとHelperはInputを書き換えない。

| 項目 | 内容 |
| --- | --- |
| `deltaTimeSeconds` | 今回の更新で使う経過時間[s] |
| `hasSpeedMeasurement`・`signedSpeedMps` | 測定速度の取得可否と、編成の固定前方向を正とする速度[m/s] |
| `hasCabState`・`cab` | 有効運転台の操作状態の取得可否と値 |
| `hasDoorState`・`areAllDoorsClosed` | 編成全体のドア状態の取得可否と全閉状態。転動防止に使用する |
| `frontTelegram`・`rearTelegram` | 両端の受信電文。未受信はnull |
| `hasCarMasses`・`cars` | 各車両の総質量と中心位置の取得可否、および固定前側から順に並ぶ車両入力 |
| `frontReceiverDistanceFromFrontM`・`rearReceiverDistanceFromFrontM` | 編成の固定前端から受電器までの取り付け距離[m] |
| `hasBrakeSettings`・`brakeSettings` | TIMSの常用ブレーキ設定の取得可否と値 |

### 有効運転台の操作状態

`ITrainAtcCabInputSource.TryReadCabInput(out TrainAtcCabInput input)`で取得する。既存の`TrainAtcCabInputAdapter`は`TrainCabResolver`で有効運転台を選び、対応する`MasterController`を直接読む。

`TrainAtcCabInput`が持つ項目は次の五つとする。力行ノッチや常用ノッチの写しはATC入力に保存しない。

| 項目 | 内容 |
| --- | --- |
| `carIndex` | 有効運転台の号車index |
| `isFrontCab` | 有効運転台が編成の固定前側か |
| `isKeyInserted` | マスコンキーが投入されているか |
| `reverserPosition` | 有効運転台を基準とした前進・中立・後退 |
| `isEmergencyBrake` | マスコンが非常位置か。TIMSが合成した非常要求とは区別する |

キー切でも操作状態を取得する。取得失敗時は`hasCabState`をfalseとし、初期値falseのキー状態を確認済みのキー切として扱わない。

### 車両質量と中心位置

`ITrainAtcMassInputSource.TryReadCarInputs(List<TrainAtcCarInput> cars)`で取得する。`TrainAtcMassInputAdapter`は、同じTrainRootのConsistDefinitionとTIMSの各号車LocalBusを読む。

| `TrainAtcCarInput`の項目 | 内容 |
| --- | --- |
| `massKg` | 空車質量と積載質量を合わせた総質量[kg] |
| `centerDistanceFromFrontM` | 編成の固定前端から車両中心までの距離[m] |

TIMSの`BrakeControlDeviceTimsBusSource.MassKgKey`は空車質量を含む測定値なので、次の式で扱う。

```text
積載質量 = max(0, TIMSの測定質量 − ConsistDefinitionの空車質量)
車両質量 = ConsistDefinitionの空車質量 + 積載質量
```

車両中心は、それより前の車両長の合計に当該車両長の半分を加える。受電器の固定前端からの距離は、該当車両中心から受電器の`OffsetFromCarCenterM`を引いて求める。

全車両の質量と両端受電器の取り付け距離を取得できた場合だけ`hasCarMasses`をtrueにする。失敗時は車両入力と取り付け距離を消去する。毎tickの車両中心や受電器位置をTrackSampleから車上計算へ渡す処理は設けない。

### ドア全閉状態

`ITrainAtcDoorInputSource.TryReadDoorState(out bool areAllDoorsClosed)`で取得する。`TrainAtcDoorInputAdapter`は同じTrainRootのTIMS MasterBusを参照し、`Door/HasValidState`がtrueの場合に`Door/AllClosed`を採用する。ATC PrefabにAdapterとControllerの参照を設定する。

取得失敗時は`hasDoorState`をfalseとし、前回の全閉状態を残さない。ドア未全閉・取得失敗のどちらも全閉未確認として転動防止を要求する。ドア状態だけの取得失敗を理由にATC全体を故障扱いにはせず、常用最大で保持する。

### 常用ブレーキ設定

`ITrainAtcBrakeSettingsInputSource.TryReadBrakeSettings(TrainAtcBrakeSettingsInput input)`で取得する。`TrainAtcBrakeSettingsInputAdapter`はTIMSのMasterBusに公開された三つの設定を確認してからコピーする。

| 項目 | 内容・公開元のキー |
| --- | --- |
| `brakeTargetDecelerationsMps2` | B1から常用最大までの通常ノッチごとの設定減速度[m/s²]。`TimsRoot.BrakeTargetDecelerationsMps2Key` |
| `brakeSubstepCount` | 通常ノッチ1段あたりの刻み数。`TimsRoot.BrakeSubstepCountKey` |
| `maximumServiceBrakeStep` | 常用最大の刻み段。`TimsRoot.MaximumServiceBrakeStepKey` |

常用最大段は`(通常ノッチ数 − 1) × brakeSubstepCount + 1`と一致する必要がある。欠損・不正時は`hasBrakeSettings`をfalseとし、前回の設定を残さない。

基準段の選択は`TrainAtcBrakeLogic.TryGetBaseBrakeStep()`から`TrainAtcBrakeHelper.TryGetNearestBrakeStep()`を呼ぶ。現在位置の常用パターンに保存した勾配補正前の減速度と、刻みごとの設定減速度を比較して最も近い段を選ぶ。差が同じ場合は小さい段を採用する。選択とヒステリシス制御はBrakeLogicの中で完結し、親Logicから基準段を渡さない。

### 測定速度と電文

`SpeedSensor.TryGetSignedMeasuredSpeedMps()`を直接読む。固定前方向を正とする符号付き速度を位置積算に使い、照査には速度の絶対値を使う。TIMS経由の表示速度を位置更新へ戻さない。

両端の受電器はSimulation側で物理位置にある軌道回路から電文を取得する。車上ATCはその受電結果を直接参照し、車上は使用側の保持位置を測定速度で更新し、非使用側をその最新位置と受電器間隔から求め直す。

電文は地上で選択したEdgeを起点とする`routeAtoB`・`routeBtoA`を直接持つ。車上は照査方向で`GetRoute()`から経路を取得し、`atcEdgePath[0]`が現在位置のEdgeと一致することを確認する。Edge ID・方向をキーとする辞書は使わない。`TrackAtcTravelDirection`と`OverrunProtectionMode`はCircuit側の定義を使用する。

## 位置の初期化・更新

`TrainSimulationController`は編成内のATCを一つ収集し、固定前後の受電器と速度センサーを接続する。固定前側の速度センサーを優先し、存在しなければ号車indexが最小のセンサーを使用する。

最初の電文を受信してから、受電器位置の`TrainTrackSample`を初期位置へ変換する。`TrainAtcInitializationLogic.TryResolvePosition()`が物理Edge ID・位置をATC Edge ID・Edge内位置へ変換し、`FrontFacesAtoB`を編成の向きとして渡す。結合Edgeの`physicalSpans`で累積距離と向きを変換する。共通部分で複数のEdgeに対応する場合は、受信経路の先頭Edgeを選択の手掛かりにする。変換はSimulation側に置き、車上Logicから物理状態を読み直さない。

ATC Graphは`TrainSimulationController`の`Atc Graph Asset`に割り当てる。`TrainAtcLogic.TryInitializePosition()`はGraphからEdge辞書を作り、Graphと線区最高速度をContextへ設定して、PositionLogicへ初期位置を渡す。初期化・位置補正の成功は戻り値で確認する。開始時にまだ位置を一意に解決できない場合は、受信が揃ってから初期化する。初期化完了後に実位置から自動補正する処理は設けない。

以降の更新は、親Logicが前回の使用受電器を保存し、`TrainAtcOperationLogic.UpdateOperation()` → `TrainAtcPositionLogic.UpdatePosition()` → `TrainAtcOperationLogic.UpdateCurrentPosition()`の順に呼ぶ。OperationLogicは先に操作・有効状態・使用受電器・電文を選択し、照査位置と方向を消去する。PositionLogicが位置を更新した後、OperationLogicが既知の選択端から照査位置と方向を設定する。

PositionLogicは既知の使用側を優先して基準端にする。使用側が不明でも他側が既知なら、その位置を基準にする。基準端を測定速度×今回の経過時間で進める。共通部分で選択Edgeが変わった場合は同じ物理区間へ対応し直すが、分岐後に他方の枝へ位置を付け替えない。実際の移動方向は測定速度の符号と編成の向きから求める。境界では基準端側の受信電文・前回採用経路・Graphから一意な接続を解決する。受信時刻との整合を目的とする積分式変更は今回の対象に含めない。

非使用側は独立して速度積算せず、毎tick基準端の最新位置から他端を相対解決する。固定前方向の変位は「基準端の`ReceiverDistanceFromFrontM` − 他端の`ReceiverDistanceFromFrontM`」で求める。同じEdge内でも複数Edgeを跨ぐ場合でも、基準端側の電文・保持経路・Graphから一意に辿れる場合だけ新しい位置を採用する。

相対解決は`hasCarMasses`がtrue、両端の取り付け距離が有限・非負、固定後側距離が固定前側距離以上の場合に行う。間隔0も許可する。距離条件の不正や分岐の曖昧さで解決できなければ、相対解決の対象端だけを不明にし、最後の位置を診断用に残す。使用側が既知ならその側の照査を続ける。片側が不明でも既知の他側を基準に相対解決を毎tick再試行し、解決できれば使用側・非使用側によらず自然に既知へ戻る。不明端の過去の保持位置から積算を再開する復帰は認めない。

固定前後の位置把握は`State.position.isFrontPositionKnown`・`isRearPositionKnown`で個別に管理する。読み取り専用の`IsPositionKnown`は両フラグのANDで、診断用に残す。ATC有効時は選択端だけの既知を必要とし、非使用側の不明や`IsPositionKnown=false`だけを故障原因にしない。基準端自身の位置更新失敗や、経過時間不正、または経過時間が正のときの速度欠損・不正では両端を不明にする。経過時間0では位置を進めず、PositionLogicは速度欠損だけで既知フラグを落とさないが、ValidationLogicは速度欠損を入力不正とする。今回の最新位置を確定できない古い他端の既知フラグは残さない。両端不明からの復帰は明示補正だけとする。最後に確認できた位置は、既知フラグがfalseの間は制御に使わない。

キー切・レバーサ中立でも位置更新は継続する。操作状態を取得済みならその運転台側の既知位置を優先し、不明なら他側の既知位置を基準にする。操作状態を取得できない場合は既知の固定前側を優先する。固定後側だけが既知なら固定後側を基準にする。ATC無効時の位置入力は片側以上が既知なら正常、両側不明なら不正とする。無効時の照査用受電器・位置・方向・電文は未選択とする。

受電器切替時の特別な位置初期化は行わない。新しい使用側が既知ならその位置で更新と照査を始める。不明でも他側が既知なら今回の相対解決を試し、解決できなければ使用不可とする。切替時は旧パターンを保持せず、新しい側の有効電文からパターンを生成し直す。

| 有効運転台 | レバーサ | 照査する編成の固定方向 |
| --- | --- | --- |
| 固定前側 | 前進 | 固定前側 |
| 固定前側 | 後退 | 固定後側 |
| 固定後側 | 前進 | 固定後側 |
| 固定後側 | 後退 | 固定前側 |

受電器は有効運転台側を選ぶ。照査方向を選択位置の`frontFacesAtoB`でEdgeのA→B・B→Aへ変換する。レバーサが指定する照査方向と、測定速度が示す実際の移動方向は区別する。

## StateとLogicの担当

`TrainAtcState`は次の子Stateを持つ。防護方式をパターン内へ重複保存しない。

| Stateの参照先 | 担当Logic・保持する情報 |
| --- | --- |
| `State.position` | `TrainAtcPositionLogic`。両端位置、初期化済み、`isFrontPositionKnown`・`isRearPositionKnown`。`IsPositionKnown`は両端既知のANDを返す診断用プロパティ |
| `State.operation` | `TrainAtcOperationLogic`。位置更新前に操作状態・電源・有効状態・使用受電器・電文を選び、更新後に照査位置・方向を確定する |
| `State.validation` | `TrainAtcValidationLogic`。入力正常性、無信号時間、Adopt・Retain・Unusable、候補経路と確認済みの計算入力 |
| `State.protectionMode` | `TrainAtcProtectionModeLogic`。Normal・Restricted・Noneと、その確定結果、保持方式・停止限界・終端方向、処理済み開扉操作番号 |
| `State.pattern` | `TrainAtcPatternLogic`。採用経路、常用・非常・独立ORPのサンプル、現在位置の許容・目標速度、接近・降下状態 |
| `State.brake` | `TrainAtcBrakeLogic`。常用・非常要求、非常保持、目標段・現在段、ヒステリシス履歴と待ち時間 |
| `State.isAtcHealthy` | `TrainAtcLogic`。各工程で確定した結果を集約した全体の正常性 |

入力・受信の判定はValidationLogicでまとめる。無信号中は、設定した猶予内で前回経路と方向が一致し、受電器が切り替わっていない場合だけパターンを保持する。無信号から回復した場合は後続工程の成功を待たずタイマーを0へ戻す。入力不正・保持不可・猶予超過・生成失敗の場合もブレーキとOutputの更新は実行する。

各子Logicは自分のStateだけを更新し、子同士で呼び合わない。親Logicの公開APIは`Calculate()`・`TryInitializePosition()`・`TryCorrectPosition()`とする。地点情報の取得を親Logicの公開APIとして案内しない。パターン内部の経路準備と距離・勾配計算は`TrainAtcPatternHelper`が担当する。

## 計算設定

`TrainAtcSettingsAsset`をControllerの`Settings Asset`へ割り当て、`Awake()`と`OnValidate()`で`context.Settings`へコピーする。共有Assetを計算中に変更しない。未割当の場合はSettingsの初期値を使用し、実行中のAsset内容を毎tickで読み直さない。

以下はクラスの初期値であり、`Atc/Data/TrainAtcSettings.asset`に保存済みの値とは区別する。

| 項目 | 初期値 | 用途 |
| --- | --- | --- |
| `maximumSamplingIntervalM` | 5m | 最大サンプル間隔 |
| `patternApproachWarningTimeSeconds` | 5秒 | 降下開始前の接近予告時間 |
| `serviceDecelerationMps2`・`emergencyDecelerationMps2` | 0.5・1.2m/s² | 常用・非常の計算用減速度 |
| `normalBrakeReleaseMarginKmh` | 3km/h | 常用要求の緩解幅 |
| `brakeStepChangeIntervalSeconds` | 0.1秒 | テーブルによる刻み段の切替間隔 |
| `brakeStepTable` | 基準段−2〜＋2の5行 | 増段・減段閾値と刻み増減数。連番でなくてもよい |
| `noSignalTimeoutSeconds` | 1秒 | 前回パターンを保持できる無信号猶予。0は即時使用不可 |
| `maximumDownhillGradientPermille` | 0‰ | 経路外の非常・ORP補正に使う線区最大下り勾配。手動設定 |
| `serviceStopMarginM`・`emergencyStopMarginM` | 100m・5m | Noneの常用停止余裕と非常停止余裕の保存値。現行の非常停止目標は終端1サンプル手前 |
| `orpSpeedLimitKmh`・`orpDecelerationMps2` | 25km/h・0.7m/s² | ORPの低速制限と専用減速度 |
| `orpMinimumTargetMarginM` | 100m | ORPの低速到達目標と独立ORP積分範囲 |

`maximumOperatingSpeedKmh`はGraphから初期化時に取り込み、設定Assetには保存しない。`TrainAtcSettings.CopyFrom()`でも上書きしない。速度余裕10km/hは`TrainAtcPatternLogic.EmergencySpeedMarginKmh`で常用以外の初期上限・常設制限・ORPに共通で使用する。

## OutputとTIMSへの接続

`TrainAtcOutputLogic.UpdateAtcOutput(context)`は工程2〜7の後に必ず実行し、ContextからOutputを生成する。Stateは変更しない。

| Outputの参照先 | 内容 |
| --- | --- |
| `Output.state` | 電源・有効状態・正常性、ATCブレーキ開放（未実装） |
| `Output.brake` | 非常保持を反映した非常指令、常用要求、常用の刻み段、ドア全閉未確認による転動防止 |
| `Output.pattern` | 未丸めの許容速度[m/s]、現示可否と5km/h刻みで切り下げた現示速度[km/h]、接近・ORP・Signal |

表示条件は[全体処理仕様の工程8](TrainAtcProcessingFlow.md#8-outputを生成)を参照する。速度表示に丸めた値をブレーキ照査へ戻さない。

### 制御要求の接続

`TimsNotchController.CollectInput()`が、同じTrainRootの`TrainAtcController.Context.Output.brake`を毎tick読む。常用段を`Input.atcBrakeStep`、非常指令を`Input.isAtcEmergency`へ渡し、手動ノッチなどと合成する。非常保持中のOutputの常用段は0で、非常要求は別の項目で出力する。

この制御経路はMasterBusの表示収集を経由しない。ATC未搭載では要求なし、取得済みATCのコンポーネント無効時はTIMS側で非常要求とする。異なる編成の参照や重複配置は有効な接続として扱わない。

### 表示情報の接続

`TrainAtcTimsDisplayAdapter`は`ITimsMasterBusSource`として、TIMSのMaster収集時点でATCの最新Outputを読む。表示用の転送は既存のMaster収集周期に従い、ブレーキの直接接続とは周期を分ける。

MasterBusの機器名は`ATC`とする。

| 項目名 | 型・内容 |
| --- | --- |
| `IsPowerOn`・`IsEnabled`・`IsHealthy`・`HasFault` | Bool。電源・有効状態・正常性・故障 |
| `IsBrakeReleased` | Bool。ATCブレーキ開放。未実装のためfalse |
| `IsRollingPreventing` | Bool。ドア全閉未確認による転動防止 |
| `IsNormalBrakeRequired`・`IsEmergencyBrakeRequired` | Bool。常用要求と、非常保持を反映した非常指令 |
| `BrakeStep` | Int。常用の刻み段 |
| `HasValidPattern` | Bool。パターン成立かつATC正常・有効 |
| `IsSpeedIndicated` | Bool。速度現示の表示可否。UIはこのフラグに従う |
| `IsOrpActive`・`IsPatternApproaching` | Bool。ORP・接近表示 |
| `Signal` | Int。None=0・Red=1・Green=2 |
| `PatternAllowSpeedMps` | Float。Outputの未丸めの許容速度[m/s] |
| `PatternAllowSpeedKmh` | Float。Outputで決めた現示速度[km/h]。速度現示不可時も転送する |
| `EmergencyPatternAllowSpeedKmh` | Float。通常非常の許容速度[km/h] |
| `NormalSpeedPatternMps`・`EmergencySpeedPatternMps` | Float配列。各サンプルの積分後の速度[m/s] |
| `PatternAtcEdgeIds` | 文字列配列。採用経路のEdge ID列 |
| `SamplingIntervalM`・`PathLengthM`・`DistanceOnPathM` | Float。サンプル間隔・経路長・現在位置の経路内距離[m] |

パターンの使用可否や速度現示の表示可否で転送項目を間引かず、今回のOutputとStateの値を毎回転送する。パターン全体と距離の診断情報はStateから取り出し、表示状態とブレーキ要求はOutputの確定結果を使用する。ATCやAdapterが無効になった場合は、表示可否をfalse、速度・距離を0、配列を空にして前回値を残さない。

ORP表示中も`PatternAllowSpeedKmh`を転送する。`IsSpeedIndicated`がfalseになるため、UI側で速度現示を消灯する。独立ORPの速度配列を既存の通常非常の配列へ上書きしない。

## 検証状況

移行後のUnityでの実走は未検証。電文境界の無信号猶予、パターン現示、常用介入と刻み切替、非常保持・解除を実際の走行で確認する。
