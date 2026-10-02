# Logicの関数呼び出し図

照合した実装: `832f50fead7a57658f39045ee3cfcd58562cbdc6`。

地上ATC・Interlockingの既存draw.ioと同じく、箱は関数、矢印は直接呼び出しを表す。状態遷移図や実行順序図ではない。公開APIを青、内部関数を白、別Logic/Calculator/Validatorへの明示的な呼び出しを橙・破線で示す。呼び出し回数はまとめ、同じ呼び出し元/先の組を1本にする。条件・状態更新は下の実装メモと機械抽出結果に記録する。

対象は実装のある全 `*Logic.cs`。Controller・View・Unity/UI描画、Editor・テスト・Definition・Contextは図にしない。`TrainStatusDisplayLogic`はPresentation名前空間だが、純粋な判定・文字列変換のみを持つLogicなので含める。表示コンポーネントのライフサイクルやUI描画へは範囲を広げない。TIMSのSpeed/Door/Safety等は独立Logicではなく既存Logicを使う接続・Adapterが中心のため、そのController図は作らない。

計算だけの装置に状態機械を仮定しない。別装置の入力はContext経由で受けるため、入力データの流れを関数呼び出しの矢印へ読み替えない。注入された接続resolver・interfaceの実装先や、Controllerからの呼び出し順も推測しない。標準ライブラリ、Math/Mathf、Context/State/Outputへのアクセスは外部関数箱としては載せない。

## 対象一覧

|Logic|図|方法|
|---|---|---|
|`TrackAtcLogic`|[既存](../Track/Atc/TrackAtcStateTransitions.drawio)|11関数・10内部呼び出し組|
|`TrackInterlockingLogic`|[既存](../Track/Interlocking/TrackInterlockingLogicCalls.drawio)|35関数・43内部呼び出し組|
|`TrackCircuitSimulationLogic`|[追加](../Track/Simulation/Circuit/TrackCircuitSimulationLogicCalls.drawio)|2関数・1内部呼び出し組|
|`TrackConnectionLogic`|[追加](../Track/Simulation/Connection/TrackConnectionLogicCalls.drawio)|6関数・8内部呼び出し組|
|`BrakeControlDeviceLogic`|[追加](../Train/Equipment/Brake/ControlDevice/BrakeControlDeviceLogicCalls.drawio)|2関数・1内部呼び出し組|
|`DoorControlLogic`|[追加](../Train/Equipment/Door/DoorControlLogicCalls.drawio)|2関数・1内部呼び出し組|
|`MasterControllerLogic`|[追加](../Train/Equipment/Operation/MasterController/MasterControllerLogicCalls.drawio)|15関数・29内部呼び出し組|
|`PositionSwitchLogic`|[追加](../Train/Equipment/Operation/Switches/PositionSwitchLogicCalls.drawio)|8関数・12内部呼び出し組|
|`EbDeviceLogic`|[追加](../Train/Equipment/Safety/Eb/EbDeviceLogicCalls.drawio)|3関数・2内部呼び出し組|
|`SpeedSensorLogic`|[追加](../Train/Equipment/SpeedSensor/SpeedSensorLogicCalls.drawio)|1関数・0内部呼び出し組|
|`TimsBrakeLogic`|[追加](../Train/Equipment/Tims/Brake/TimsBrakeLogicCalls.drawio)|10関数・9内部呼び出し組|
|`TimsCommunicationLogic`|[追加](../Train/Equipment/Tims/Communication/TimsCommunicationLogicCalls.drawio)|4関数・1内部呼び出し組|
|`TrainStatusDisplayLogic`|[追加](../Train/Equipment/Tims/Display/TrainStatus/TrainStatusDisplayLogicCalls.drawio)|3関数・0内部呼び出し組|
|`TimsNotchLogic`|[追加](../Train/Equipment/Tims/Notch/TimsNotchLogicCalls.drawio)|4関数・3内部呼び出し組|
|`TimsDirectionLogic`|[追加](../Train/Equipment/Tims/Operation/Direction/TimsDirectionLogicCalls.drawio)|1関数・0内部呼び出し組|
|`TimsTractionLogic`|[追加](../Train/Equipment/Tims/Traction/TimsTractionLogicCalls.drawio)|4関数・3内部呼び出し組|
|`VvvfLogic`|[追加](../Train/Equipment/Traction/Vvvf/VvvfLogicCalls.drawio)|7関数・6内部呼び出し組|
|`BrakeCylinderLogic`|[追加](../Train/Simulation/Brake/BrakeCylinderLogicCalls.drawio)|3関数・2内部呼び出し組|
|`DoorSimulationLogic`|[追加](../Train/Simulation/Door/DoorSimulationLogicCalls.drawio)|3関数・2内部呼び出し組|
|`TrainLoadLogic`|[追加](../Train/Simulation/Load/TrainLoadLogicCalls.drawio)|2関数・0内部呼び出し組|
|`TrainPhysicsLogic`|[追加](../Train/Simulation/Physics/TrainPhysicsLogicCalls.drawio)|7関数・5内部呼び出し組|
|`TrainTrackPathLogic`|[追加](../Train/Simulation/TrackPosition/TrainTrackPathLogicCalls.drawio)|4関数・5内部呼び出し組|
|`TrainTrackPositionLogic`|[追加](../Train/Simulation/TrackPosition/TrainTrackPositionLogicCalls.drawio)|2関数・1内部呼び出し組|
|`TrainTrackSamplingLogic`|[追加](../Train/Simulation/TrackPosition/TrainTrackSamplingLogicCalls.drawio)|4関数・2内部呼び出し組|
|`MotorLogic`|[追加](../Train/Simulation/Traction/Motor/MotorLogicCalls.drawio)|8関数・7内部呼び出し組|

## 図を作らない空実装

|ファイル|理由|
|---|---|
|`Assets/Nakatetsu/Train/Equipment/LoadWeightDevice/Scripts/LoadWeightDeviceLogic.cs`|空ファイル。関数・状態更新の実装なし|
|`Assets/Nakatetsu/Train/Simulation/Orchestration/Scripts/TrainSimulationLogic.cs`|空ファイル。関数・状態更新の実装なし|

荷重計には空のLogicファイルしかなく、独立した処理を捏造しない。編成Simulationの空Logicも同様で、Controllerのオーケストレーションを代わりに図へ入れない。

## 条件分岐・状態更新の実装メモ

### TrackAtcLogic

ソース: [`TrackAtcLogic.cs`](../../Assets/Nakatetsu/Track/Atc/Scripts/TrackAtcLogic.cs)

既存図を保持。C#宣言された全関数とクラス内直接呼び出しの組を今回の検証で再照合した。詳細の分岐・代入式は検証JSONを参照。

### TrackInterlockingLogic

ソース: [`TrackInterlockingLogic.cs`](../../Assets/Nakatetsu/Track/Interlocking/TrackInterlockingLogic.cs)

既存図を保持。C#宣言された全関数とクラス内直接呼び出しの組を今回の検証で再照合した。詳細の分岐・代入式は検証JSONを参照。

### TrackCircuitSimulationLogic

ソース: [`TrackCircuitSimulationLogic.cs`](../../Assets/Nakatetsu/Track/Simulation/Circuit/TrackCircuitSimulationLogic.cs)

保持・計算対象: 占有状態の再計算。

- 最初に全回路を占有にする。Graph・占有範囲・回路区間の入力が不正なら、その安全側状態を維持して終了する。
- 全入力の検証後に、同一Edgeの範囲重複で各回路の占有を更新する。境界一致も占有。

### TrackConnectionLogic

ソース: [`TrackConnectionLogic.cs`](../../Assets/Nakatetsu/Track/Simulation/Connection/Scripts/TrackConnectionLogic.cs)

保持・計算対象: 要求位置・実位置・転換中の離散状態。

- 初期化はContextを消去してGraph・Normal/Reverse・接続定義を検証し、定義のコピーと初期位置を保持する。
- 別位置への要求では実位置をUnknown、IsMovingをtrueにする。転換中の別要求は拒否。同じ要求位置の完了通知で位置を確定する。
- 接続解決は初期化・定義・実位置の確定を照査する。転換中やUnknownでは相手Edgeを返さない。

### BrakeControlDeviceLogic

ソース: [`BrakeControlDeviceLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Brake/ControlDevice/Scripts/BrakeControlDeviceLogic.cs)

保持・計算対象: 入力から圧力・力を計算。

- 毎回Outputをゼロ初期化し、健全で有効面積・最大圧が正のシリンダーだけを集計する。
- 有効シリンダーがなければ終了。有効面積を合計し、最小の最大圧を共通上限として目標力から目標BC圧を求める。

### DoorControlLogic

ソース: [`DoorControlLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Door/Scripts/DoorControlLogic.cs)

保持・計算対象: 左右の指令・既読revisionを保持。

- 速度入力と開許可を照査し、左右それぞれResolveを呼ぶ。速度は有限・非負・開許可速度以下であることが必要。
- 指令欠損ではHold。初回またはrevision変更時だけ指令を採用する。既読revisionは保持する。
- 開許可がないOpenはHoldへ変更し、許可復帰後にも自動再実行しない。再操作によるrevision変更が必要。

### MasterControllerLogic

ソース: [`MasterControllerLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Operation/MasterController/Scripts/MasterControllerLogic.cs)

保持・計算対象: 力行・制動・逆転器の離散状態。

- 設定・初期化後はNormalizeで範囲を補正する。力行と制動が同時に正なら力行をゼロにする。
- SetPowerPosition/SetBrakePositionは範囲に収め、正の段を設定した側が反対側をゼロにする。
- 逆転器の変更は入力有効・力行ゼロ・非常ブレーキ位置・Reverse〜Forwardの範囲がすべて成立する場合だけ。
- 段送りは現在の力行/制動位置で呼び出し先を分ける。公開Setter自体にはisInputEnabledによる受付制限はない。

### PositionSwitchLogic

ソース: [`PositionSwitchLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Operation/Switches/Scripts/PositionSwitchLogic.cs)

保持・計算対象: positionを保持する離散状態。

- Configureで最小・最大・ばね復帰位置を正規化し、現在位置も範囲へ補正する。
- SetPositionは補正後の位置が同じならfalse、変われば状態を更新してtrue。MoveOneStepはdirectionの符号がゼロなら変更しない。
- ReleaseはhasSpringReturnがtrueのときだけSetPositionを呼ぶ。運転台選択スイッチの共通Logicもこの図が対象。

### EbDeviceLogic

ソース: [`EbDeviceLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Safety/Eb/Scripts/EbDeviceLogic.cs)

保持・計算対象: 無操作時間・前回操作・非常ラッチ。

- ラッチ済みでは、有効入力・絶対速度0.01 m/s未満・力行位置ゼロの場合だけReset。それ以外は非常要求とブザーを保持する。
- 未作動で監視対象外ならReset。対象は有効な速度、運転台有効、入力有効、設定された作動速度以上。
- resetRequestedまたは前回とのマスコン操作差で無操作時間をゼロにし、それ以外は非負deltaTimeを加算する。
- warningDelay以上でブザー、warningDelay+warningDuration以上で非常ラッチ。作動後のresetRequestedだけでは解除しない。

### SpeedSensorLogic

ソース: [`SpeedSensorLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/SpeedSensor/Scripts/SpeedSensorLogic.cs)

保持・計算対象: 測定Outputの再計算。

- 物理速度あり、NaN/Infinityでない場合だけhasMeasurementをtrueにする。
- 測定速度は符号付き物理速度の絶対値。入力欠損・非有限値では測定無効で値はゼロ。

### TimsBrakeLogic

ソース: [`TimsBrakeLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Tims/Brake/Scripts/TimsBrakeLogic.cs)

保持・計算対象: 指令配分・Workspace/Outputの再計算。

- 非常要求をOutputへ写し、前回指令を消去する。非常時は配分計算を行わず終了する。
- Workspace初期化→必要減速度→必要総力→最低込め→残余力→車両質量比配分→回生配分→追加空制→Outputの順。
- 回生目標は飽和付き均等配分。追加空制は実回生力から求め、他車余剰回生で減らすが最低込めは減らさない。
- 空制力を圧へ変換し圧力上限を適用してから力を再計算し、圧力と力の指令を一致させる。

### TimsCommunicationLogic

ソース: [`TimsCommunicationLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Tims/Communication/Scripts/TimsCommunicationLogic.cs)

保持・計算対象: 収集周期の経過時間・バス参照。

- ShouldCollectSourcesは不正なdeltaTime/intervalでfalse。初回またはinterval<=0なら即時収集し経過時間をゼロにする。
- 周期到達では端数を残す。複数周期を越えてもこの呼び出しの収集許可は1回。
- Calculateは重複sourceIdを拒否し、初期化されたlocalBus付き端末がある送信元だけを選ぶ。
- CollectFloatFromCarsは値と取得成功フラグを別々に返す。欠損を有効なゼロと扱わない。これらAPI同士に直接の呼び出しはない。

### TrainStatusDisplayLogic

ソース: [`TrainStatusDisplayLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Tims/Display/TrainStatus/Scripts/TrainStatusDisplayLogic.cs)

保持・計算対象: 表示用判定・文字列変換のみ、状態保持なし。

- Presentation名前空間だがLogicとして純粋な判定/変換を持つため対象に含める。描画コンポーネントは対象外。
- ResolveMotionは表示無効/非電動車ならCoasting、測定欠損/非有限力ならUnavailable。実測力>1 NでPower、<-1 NでRegen、その他Coasting。
- ResolveDirectionは運転台・逆転器がともに±1の場合だけ積を返し、その他はゼロ。FormatCarNumberは1未満を空文字、その他を全角数字へ変換する。
- 3つの公開関数間に直接の呼び出しはない。状態機械や表示更新順の矢印を付けない。

### TimsNotchLogic

ソース: [`TimsNotchLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Tims/Notch/Scripts/TimsNotchLogic.cs)

保持・計算対象: 指令Outputの再計算。

- 前回制動出力を消去し、制動→力行→ラベルの順に計算する。
- 運転台無効・入力欠損・ATC非常・手動非常・許可最大段超過では非常要求。通常制動はATCと手動の大きい段を採用し、同段なら手動表示。
- 力行は毎回ゼロから始める。非常、手動/ATC/TASC制動、解決済み制動のいずれかがあれば力行しない。
- 力行値は現実装では手動入力だけを採用し範囲に収める。ATOを実装済みとして描かない。

### TimsDirectionLogic

ソース: [`TimsDirectionLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Tims/Operation/Direction/Scripts/TimsDirectionLogic.cs)

保持・計算対象: 選択入力からOutputを再計算。

- 毎回None/Neutral/方向符号ゼロへ初期化する。前後の選択入力がそろわなければ終了。
- 前Front・後Rearなら前運転台、前Rear・後Frontなら後運転台。それ以外の組合せは無効。
- 選ばれた運転台の逆転器入力がある場合だけ取得し、運転台符号×逆転器符号を編成方向とする。

### TimsTractionLogic

ソース: [`TimsTractionLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Tims/Traction/Scripts/TimsTractionLogic.cs)

保持・計算対象: 力行指令・領域Outputの再計算。

- 準備未完了または制動段ありではゼロ力を配分する。配分処理はhasForceCommandをtrueにし、利用可能装置へ均等配分する。
- 定格出力と質量・加速度から定加速終了速度を計算し、速度以下は定加速、それを超えると定出力領域。
- 力行段ゼロでは目標力ゼロ。BC緩解条件が成立しなければ目標力をゼロとしBC Interlock表示。
- 勾配起動時はBC照査を通す。車両別BC圧がなければ単一BC圧を閾値未満で照査する。

### VvvfLogic

ソース: [`VvvfLogic.cs`](../../Assets/Nakatetsu/Train/Equipment/Traction/Vvvf/Scripts/VvvfLogic.cs)

保持・計算対象: 滑り周波数・電圧比・位相の連続状態。

- 目標力>0.01 NでPower、<-0.01 NでRegen、それ以外はNeutral。車輪/モーター回転から基準周波数と目標トルクを計算する。
- Neutralでは滑り周波数をゼロへ応答させる。駆動時はトルク偏差、デッドバンド、速度制限付き滑り上限で更新する。
- 指令なしでも電圧比または滑りが減衰中なら周波数を維持する。低回転のPowerでは起動周波数・電圧ブーストを適用する。
- 電圧比をMoveTowardsで更新し、位相を進めて三相電圧を出力する。MotorLogicの計算関数を直接呼ぶが、MotorLogic.Calculateは呼ばない。

### BrakeCylinderLogic

ソース: [`BrakeCylinderLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/Brake/Scripts/BrakeCylinderLogic.cs)

保持・計算対象: BC圧の連続状態。

- 現在圧・目標圧を設定範囲に制限し、不健全なら目標圧ゼロにする。
- 目標圧が現在圧を上回る場合は込め速度、その他は緩解速度を使い、非負の時間幅でMoveTowardsする。
- 更新圧×1000×有効面積×機械効率から実空制力を出力する。

### DoorSimulationLogic

ソース: [`DoorSimulationLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/Door/Scripts/DoorSimulationLogic.cs)

保持・計算対象: openingRatio・closedContact・DoorStatus。

- state欠損ではfalse。時間・開度の不正またはfaultでclosedContact=false、status=Fault。faultだけならvalid=trueを返す場合がある。
- 有効入力ではOpenで開度増加、Closeで減少、Holdで維持。端点残差だけを0/1へ丸める。
- 開度ゼロならClosed、1ならOpen、中間では指令によりOpening/Closing/Stopped。Faultは永続ラッチではなく次回の有効入力で再評価する。
- 開閉時間のばらつきは号車・側・戸の決定的ハッシュから求め、variationの正側上限を20%にする。

### TrainLoadLogic

ソース: [`TrainLoadLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/Load/TrainLoadLogic.cs)

保持・計算対象: Output計算とApplyOutputによるState反映。

- 乗客数を容量範囲へ補正し、非負の乗客平均質量・荷物・空車質量から総質量を計算する。
- 現実装では支持質量=総質量。ApplyOutputが各OutputをStateに写してisInitialized=trueにする。
- CalculateからApplyOutputへの直接呼び出しはない。呼び出し順の管理はController側であり図の対象外。

### TrainPhysicsLogic

ソース: [`TrainPhysicsLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/Physics/TrainPhysicsLogic.cs)

保持・計算対象: 符号付き速度・加速度・変位の連続状態。

- 変位をゼロにして時間幅を検証する。停止付近で制動力が非制動力を保持できる場合は速度・加速度・変位をゼロにする。
- 保持しない場合は加速度→速度の順。走行抵抗は速度逆向き、停止中は非制動力を超えて動かさない範囲。
- 速度が符号を跨ぐ計算では停止までの時間だけで変位を積分し、次速度をゼロにする。それ以外は台形積分。
- CalculateGradeForceNとCaptureOutPutは独立APIで、Calculateから直接呼ばない。CaptureOutPutはStateをOutputへ写す。

### TrainTrackPathLogic

ソース: [`TrainTrackPathLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/TrackPosition/TrainTrackPathLogic.cs)

保持・計算対象: Graph探索・状態を保存しない。

- 現在位置とoffsetを検証し、編成前方向を考慮して隣接Edgeを探索する。保存経路は使わない。
- TryGetAdjacentは接続resolver、隣接Edgeの妥当性、進入ノードの一意性を確認し、方向と進入距離を返す。
- 探索回数上限はTrainTrackPositionLogic.guard。失敗や上限到達ではfalse。resolverの実装装置は注入されるため特定しない。

### TrainTrackPositionLogic

ソース: [`TrainTrackPositionLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/TrackPosition/TrainTrackPositionLogic.cs)

保持・計算対象: Edge ID・距離・編成向きの状態。

- 不正なGraph・Edge・有限値照査または距離のfloat範囲超過ではOutputを無効にする。
- 符号付き変位を編成向きに合わせ距離へ加え、越境処理後にTrainTrackSamplingLogic.RefreshOutputを呼ぶ。
- 越境ではまず境界へ距離を制限する。接続失敗またはguard到達ならそこで終了。成功時だけEdge・編成向き・残距離を更新する。

### TrainTrackSamplingLogic

ソース: [`TrainTrackSamplingLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/TrackPosition/TrainTrackSamplingLogic.cs)

保持・計算対象: サンプルOutputの再構築。

- ConfigureLayoutで各車中心/台車のoffsetを保存しOutputを無効にする。長さと台車間距離の配列数不一致は例外。
- RefreshOutputは全車の前後台車サンプルを取得し、先頭前台車〜最後尾後台車の占有Edge範囲を構築する。
- サンプルまたは占有構築の失敗では全Outputを無効化。成功時だけCompleteUpdateする。
- 位置探索はTrainTrackPathLogic、線路評価はTrackEdgeCalculatorへ委譲する。Graphの現在接続を毎回使用する。

### MotorLogic

ソース: [`MotorLogic.cs`](../../Assets/Nakatetsu/Train/Simulation/Traction/Motor/Scripts/MotorLogic.cs)

保持・計算対象: 等価回路からOutputを計算。

- 定格トルクを求め、周波数<0.01 Hzまたは電圧<1 Vでは定格トルクを残して他出力をResetする。
- 絶対滑り率<0.0001では電気出力をゼロにして終了する。
- 通常は周波数比例の等価回路から電流・トルク・電力を計算する。回生の符号は滑り率から伝わる。
- 回転/周波数の変換関数はVVVFからも直接呼ばれる。駆動装置のController接続はこの図の対象外。

## 再検証

必要な解析環境は `tree-sitter==0.21.3` と `tree-sitter-languages==1.10.2`。C#の構文木から宣言と呼び出しを抽出するため、コメントや文字列内の関数名を誤って呼び出しと数えない。

```sh
python Documentation/Validation/LogicDiagrams/build_logic_diagrams.py . --check
```

[`CodeCheck.json`](../Validation/LogicDiagrams/CodeCheck.json)にソースSHA-256、全関数・全内部呼び出し組、明示的な外部Logic/計算補助呼び出し、実装の条件式と代入式を記録する。代入式にはローカル変数も含まれる。これは状態更新だけを抽出したものではなく、コード照合のための原文参照である。外部Logic等の同名関数の宣言存在も確認する。既存2図は既存形式どおり内部呼び出しだけを照合し、外部呼び出しの図示を後付けしない。

描画はdraw.ioで使われるmxGraph 4.2.2とChromeで確認する。再描画には同フォルダーの `render_logic_diagrams.cjs` と `mxgraph@4.2.2`、`puppeteer-core` が必要。

```sh
node Documentation/Validation/LogicDiagrams/render_logic_diagrams.cjs /path/to/repository /path/to/node_modules /path/to/Google\ Chrome /tmp/logic-diagram-render
```

チェック結果は [`RenderCheck.json`](../Validation/LogicDiagrams/RenderCheck.json)。描画画像はPRに重複収録しない。解析・描画の検証であり、Unityの動作検証や装置のテスト実行ではない。
