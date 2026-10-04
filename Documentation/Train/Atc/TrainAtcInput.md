# 車上ATCの入力

`TrainAtcController.CollectInput()`で有効運転台の操作状態・測定速度・各車両の質量と中心位置・両端の受信電文を取り込み、`TrainAtcLogic.Calculate()`で位置更新・正常判定・有効運転台側の電文と位置の選択・ブレーキパターン生成・ブレーキ出力の更新を行う。

## Logicの分割

処理順と更新前の不正判定は`TrainAtcLogic`に置き、次の独立したstaticクラスを呼ぶ。分割したクラス同士は呼び合わず、計算結果は引数やContextのStateで受け渡す。各処理はOutputを参照せず、最後に`TrainAtcOutputLogic.UpdateOutput(context)`でContextからOutputを生成する。

| クラス | 処理 |
| --- | --- |
| `TrainAtcLogic` | 全体の処理順、入力・電文・猶予・保持可否の判定、基準ノッチの取得、外部向けの公開関数 |
| `TrainAtcPositionLogic` | 両端の位置初期化・補正・更新、有効運転台側の位置と進行方向の選択 |
| `TrainAtcBrakePatternLogic` | 経路の地点情報と距離、パターンの生成・保持・消去、現在位置のパターン情報の更新 |
| `TrainAtcBrakeLogic` | 常用・非常の判定、目標段と実出力段の更新 |
| `TrainAtcOutputLogic` | Stateの確定結果から許容速度・接近・ORP・Signal・ブレーキ指令をOutputへ反映 |

`TrainAtcLogic`から呼ぶ入口は`internal`、各クラス内で使う補助関数は`private`とする。外部から必要な位置初期化・位置補正・地点情報・経路距離の取得と`Calculate()`だけを、`TrainAtcLogic`の`public`関数として公開する。

`TrainAtcLogic.GetBaseBrakeStep()`が`TrainAtcNotchHelper.TryGetNearestBrakeStep()`を呼び、取得した基準段を`TrainAtcBrakeLogic.UpdateBrakeState()`へ渡す。取得失敗は−1として渡し、常用介入が必要な場合は従来どおり常用最大段へ切り替える。非常速度の余裕は`TrainAtcLogic`の`EmergencySpeedMarginKmh`をパターン更新へ引数で渡し、常設速度制限とORPで同じ値を使う。

[関数の呼び出し図](TrainAtcTransitions.drawio)は1ファイルにつき1ページとし、関数を重複させない。同じファイル内の呼び出しは線で結び、別ファイルへの呼び出し先は関数の説明欄に記載する。公開関数は青、それ以外は白とし、アクセス修飾子や行数は図に記載しない。

Logic内では`CaptureCabState()`が運転台状態の確認と保持を、`CaptureCurrentTelegram()`が有効運転台側の電文選択とコピーを担当する。

## 車上ATCの計算設定

`TrainAtcController`のInspectorの`Settings Asset`に`TrainAtcSettingsAsset`を割り当てる。ATC Prefabには`Assets/Nakatetsu/Train/Equipment/Atc/Data/TrainAtcSettings.asset`を設定済み。

`Awake()`とControllerの`OnValidate()`で設定値を`context.Settings`へコピーする。Logicからは、例えば`context.Settings.maximumSamplingIntervalM`で読む。Contextごとに設定値を保持するため、Contextの変更で共有Assetや他の編成の設定は変わらない。Assetが未割当の場合はContextの初期値を使う。実行中のAsset内容変更を毎tickで読み直す処理は設けない。

| 項目 | 初期値 | 内容 |
| --- | --- | --- |
| `maximumSamplingIntervalM` | 5m | パターンの最大サンプル間隔 |
| `serviceDecelerationMps2` | 0.5m/s² | 常用の計算用減速度。暫定値 |
| `emergencyDecelerationMps2` | 1.2m/s² | 非常の計算用減速度。暫定値 |
| `normalBrakeReleaseMarginKmh` | 3km/h | 常用要求を解除する、パターン速度からの低下幅。0以上を設定し、降下中・非降下の両方に適用 |
| `brakeStepChangeIntervalSeconds` | 0.1秒 | テーブルによる実出力段の切り替え間隔。Inspectorの最小値は0.01秒 |
| `brakeStepTable` | 基準段−2〜＋2の5行 | 基準段からの増減数と、その段へ強める・弱める速度偏差[km/h] |
| `noSignalTimeoutSeconds` | 1秒 | 無信号を許容する連続時間。超過で非常、0は即時非常 |
| `maximumDownhillGradientPermille` | 0‰ | 線区の最大下り勾配の大きさ。経路外の車両に対する非常側の補正に使用 |
| `serviceStopMarginM` | 100m | 過走防護が`None`の場合の常用停止余裕距離 |
| `emergencyStopMarginM` | 5m | 非常停止余裕距離の保存値。現在の停止目標は1サンプル手前で、この設定値は参照しない |
| `orpSpeedLimitKmh` | 25km/h | ORPの常用パターンの制限速度 |
| `orpMinimumTargetMarginM` | 100m | ORPの低速到達位置に必要な最低余裕距離 |

減速度は設定値を読み、各サンプルで編成全体の勾配補正を加えて速度積分に使う。0以下・NaN・Infinityの設定値や、補正後の非正・不正な有効減速度ではパターンを消す。線区最高速度と区間制限はTrack側の設定として扱い、車上設定Assetには含めない。

`brakeStepTable`は`List<TrainAtcBrakeStepCondition>`としてInspectorから編集できる。各行は`brakeStepOffset`（基準段からの刻み増減数）、`increaseToDeviationKmh`（この段へ強める閾値）、`decreaseToDeviationKmh`（この段へ弱める閾値）を持つ。基準段からの増減数の小さい順に並べる。増減数は連番でなくてもよく、例えば−2・−1・0・＋2・＋4も使用できる。初期値は−2・−1・0・＋1・＋2の5行で、両方の閾値を増減数×0.5km/hとする。`CopyFrom()`はリストと各行の値をコピーし、AssetとContext間で共有しない。Logicからは`context.Settings.brakeStepTable`で取得できる。`TrainAtcLogic.GetBaseBrakeStep()`で現在位置のパターン減速度から基準段を取得し、`CreateBrakeStep()`の降下中の分岐で、前回の選択行と速度偏差から`State.brake.targetBrakeStep`を更新する。選択行は`targetBrakeStepTableIndex`へ保持し、停止保持・緩解・降下区間からの退出で−1へ戻す。`CreateBrakeStep()`は降下中の目標段を設定した直後に`UpdateCurrentBrakeStep()`を呼ぶ。同関数は`Input.deltaTimeSeconds`で経過時間を計測し、`Settings.brakeStepChangeIntervalSeconds`ごとに`currentBrakeStep`を目標へ1step近づける。初回の基準段と、停止保持・完全緩解・降下していない場合の切り替えは即時とする。目標段の計算と現在段の追従は実装済みで、`UpdateBrakeState()`から毎tick`CreateBrakeStep()`を呼ぶ。停止保持中は`isNormalRequired`をtrueにし、停止判定は0.1km/h以下、緩解判定は、現在速度がパターン速度から`Settings.normalBrakeReleaseMarginKmh`（初期値3km/h）を引いた速度以下の場合とする。テーブルや基準段、切り替え時間が不正な場合は`SetMaximumServiceBrakeStep()`で現在段・目標段を入力された常用最大へ即時に切り替え、履歴と待ち時間を消す。常用段は`Output.brake.brakeStep`へ反映し、非常保持中は0にする。TIMSは毎tick出力を読んで、手動ノッチと合成する。

経路外の車両に対する非常側の勾配補正には、車上設定Assetの`maximumDownhillGradientPermille`を使う。両方向の走行を考慮した線区の最大下り勾配を、0以上の大きさ[‰]で設定する。Logicは負号を付けて下り勾配として使用する。初期値の0‰は平坦線区の設定値とし、勾配のある線区ではその最大値を手動で設定する。負数・NaN・Infinityではパターンを消す。`CopyFrom()`で他の計算設定と同様にContextへコピーする。

Graph全体からの自動算出や設定値の自動変更は行わず、今回の経路と関係のない勾配プロファイルの不正はパターン計算を妨げない。経路内の車両の勾配は、引き続きGraphから取得する。

線区最高速度は、生成元の`TrackAtcGraphCompileDefinition.maximumOperatingSpeedKmh`へ設定し、Graph生成時に`TrackAtcGraphDefinition.maximumOperatingSpeedKmh`へコピーする。NtLineは80km/hを設定済み。

車上の位置初期化が成功したときだけ、Graphの値を`context.Settings.maximumOperatingSpeedKmh`へ取り込む。未初期化時は0であり、初期化失敗で候補Graphの値を部分的に取り込まない。負の値・NaN・InfinityはGraph生成と車上初期化の両方で拒否する。0は速度上限0として扱い、別の速度へ置き換えない。

この値は車上設定Assetに保存せず、`TrainAtcSettings.CopyFrom()`でも上書きしない。パターン計算でm/sが必要な場合は、`context.Settings.maximumOperatingSpeedKmh / 3.6f`で換算する。開始後にGraphを編集しても自動では読み直さず、初期化時の値を使う。

## ゲーム開始時の位置初期化

`TrainSimulationController`の最初の`Calculate()`で、移動処理より前に一度だけ両端の受信機位置の`TrainTrackSample`を取得する。各受信機の`SimulationAssignment`の号車indexと`offsetFromCarCenterM`を使うため、車両中心以外に取り付けてもその位置で初期化する。

`TrainAtcInitializationLogic.TryResolvePosition()`が`EdgeId`と`DistanceOnEdgeM`をATCエッジとATCエッジ内距離に変換し、`FrontFacesAtoB`を編成の向きとして渡す。物理エッジとの対応情報を読む処理はSimulation側に置き、車上Logicでは読まない。区間の共有境界は始点側の区間を使い、続きのない終端では終点側の区間を使う。複数区間の重なりや対応するエッジの欠損は初期化失敗とする。

`TrainAtcLogic.TryInitializePosition()`で`State.frontPosition`と`State.rearPosition`を保持し、線区最高速度を`Settings.maximumOperatingSpeedKmh`へ取り込む。`frontFacesAtoB`は各受信機位置のATCエッジに対する固定前側（Tc1）の向きで、有効運転台やレバーサ位置によって反転しない。

`CaptureCurrentPosition()`で有効運転台側を`State.currentPosition`へ設定し、レバーサが指定する進行方向を`State.currentTravelDirection`へ設定する。位置を選択できた場合は`hasCurrentPosition`がtrueとなる。運転台を取得できなければ現在位置の選択を解除し、進行方向を`Unspecified`にする。両端の保持位置は消さない。

キー操作・運転台変更・受信機の再接続では初期化し直さない。開始時にグラフ・受信機・初期配置が不足していた場合も後のステップではTrackSampleを読み直さず、`isPositionInitialized`はfalse、`isHealthy`と`isAtcEnabled`はfalseとする。

受信機が電文受信のために毎回読む物理位置は、車上ATCの保持位置へ転送しない。初期設定後の移動は速度発電機の測定値から積算する。

## ATCエッジ位置の更新

`UpdateAtcEdgePosition()`は、速度発電機の符号付き測定速度`Input.signedSpeedMps`と`Input.deltaTimeSeconds`から移動距離を求める。正は編成の固定前方向（Tc1側）、負は固定後方向。速度の符号で実際の移動方向を決め、レバーサ位置からは求めない。

`TrainSimulationController.ResolveReferences()`で固定前側の速度発電機を車上ATCへ接続する。固定前側になければ号車indexが最小の速度発電機を使う。ATCは`TryGetSignedMeasuredSpeedMps()`を直接読み、TIMS向けの絶対値の速度出力は従来どおり保持する。

キーが「切」、または有効運転台を取得できないときも、両端の`frontPosition`・`rearPosition`を更新する。更新後に`CaptureCurrentPosition()`で有効運転台側をコピーする。`currentPosition`だけを更新して次回に元の位置へ戻ることはない。

エッジの終端を越えた場合は、残りの移動距離を次のエッジへ引き継ぐ。次のエッジへの進入がNode A側かNode B側かに応じてエッジ内距離と`frontFacesAtoB`を更新し、エッジを複数越える場合も順に処理する。初期設定時の物理エッジ対応情報は位置更新で読まない。

接続先は各受信機側の電文にある、現在エッジと実際の移動方向に対応する`atcEdgePath`を優先する。一回の更新で複数エッジを越えても、取得した経路を引き継ぐ。`GetRetainedBrakePattern()`で前回採用したパターンを取得できた場合は、有効運転台側の受信機の位置更新にその経路も渡す。電文がない・不正・対応する経路が使えない場合でも、保持経路から分岐先を解決できる。経路で解決できない場合は、出口のNodeに接続する次のエッジが一つなら進む。複数ある場合も、定義済みの進路から接続先が一つに絞れるなら進路を解決する。

この位置更新では、進路の開通・鎖錠状態やエッジの通行許可方向で接続先を除外しない。未開通の進路や逆方向への移動でも、接続先を一意に特定できれば位置を更新する。通行・停止の制御は後続のブレーキ処理で扱う。

測定速度の欠損・不正値、負または不正な経過時間、接続先の特定失敗などでは`isPositionKnown`をfalseにする。最後に確認できた両端の位置は記録として残し、片側だけ更新しない。`hasCurrentPosition`・`isHealthy`・`isAtcEnabled`はfalseとなり、次回の入力が正常でも位置不明を維持する。

復帰には`TrainAtcController.TryCorrectPosition(frontPosition, rearPosition)`を使い、両端の位置と編成の向きを明示的に設定する。両端が現在のATCグラフ上で有効な場合だけ、`isPositionKnown`をtrueに戻す。TrackSampleの自動再読込は行わない。

再接続時など経過時間が0の呼び出しは、位置を動かさず測定欠損でも位置不明にしない。接続先のない終端にちょうど到達した場合は終端位置を保持し、そこから先へ移動した場合は位置不明とする。循環や異常な移動量で探索が終わらなくならないよう、一回の更新の探索回数をグラフのエッジ数に応じて制限する。

## レバーサが指定する進行方向

`State.currentTravelDirection`は、有効運転台とレバーサから決める現在ATC Edge上の進行方向で、`TrackAtcTravelDirection.AtoB`または`BtoA`を保持する。

レバーサの前進・後進は有効運転台を基準としているため、まず編成の固定前後に変換する。

| 有効運転台 | レバーサ | 編成の固定前後での指定方向 |
| --- | --- | --- |
| 固定前側 | 前進 | 固定前側 |
| 固定前側 | 後進 | 固定後側 |
| 固定後側 | 前進 | 固定後側 |
| 固定後側 | 後進 | 固定前側 |

その指定方向と`currentPosition.frontFacesAtoB`を使って、現在EdgeのA→B・B→Aへ変換する。位置更新後に計算するため、次Edgeの接続によって編成の向きが変われば進行方向もそのEdgeに合わせて更新される。

停止中もレバーサの指定方向を保持する。中立、有効運転台の取得失敗、位置未初期化・位置不明の場合は`Unspecified`とし、前回の指定方向を残さない。キーが「切」でも運転台と位置が取得できれば方向を更新する。

位置更新に使う実際の移動方向は、引き続き速度発電機の符号から求める。レバーサの指定と逆方向に動いた場合も、その測定値で位置を更新する。

## ATCグラフの参照元

`TrainSimulationController`のInspectorの`Atc Graph Asset`に路線の`TrackAtcGraphAsset`を割り当てる。NtLine Sceneには`Assets/Nakatetsu/Track/NtLine/Data/NtLineAtcGraph.asset`を設定済み。

初期化ではその`Definition`を渡し、車上Logicからは`context.Graph`で読む。参照できるのは初期化成功後で、未初期化時はnull。地上ATCの`Context.Input`などにある占有・連動の動的な状態は車上から参照しない。

`TryInitializePosition()`では、`TryCreateAtcEdgesById()`でEdge IDから`TrackAtcGraphEdge`を引く辞書を作成する。両端の位置確認も成功した場合だけ、`context.atcEdgesById`に保持する。Edgeがnull、IDが未設定、IDが重複している場合は初期化に失敗し、Graphや辞書を部分的に保持しない。

初期化後のEdge検索には`context.atcEdgesById.TryGetValue(atcEdgeId, out var edge)`を使う。既存の位置更新・進路解決の`TryGetAtcEdge()`も、この辞書を参照する。毎tickの再作成は行わず、開始時に渡した路線データのEdge IDと構成は固定として扱う。

## 運転台の操作状態

ATC Prefabにある`TrainAtcCabInputAdapter`が、既存の`TrainCabResolver`でTIMSの有効運転台を取得し、対応する`MasterController`を直接読む。入力操作と同じ運転台選択を使う。

`Context.Input.hasCabState`がtrueのとき、`Context.Input.cab`に次の値が入る。

| 項目 | 内容 |
| --- | --- |
| `carIndex` | 有効運転台の号車index。固定前側は0、固定後側は編成の最後尾 |
| `isFrontCab` | 有効運転台が固定前側か。falseは固定後側。号車indexと前後の選択は別々に保持する |
| `isKeyInserted` | マスコンキーが「入」か |
| `isInputEnabled` | マスコンの操作入力が許可されているか |
| `powerPosition` | 力行ノッチ |
| `brakePosition` | 非常位置を含む制動ノッチ |
| `serviceBrakePosition` | 常用最大までの制動ノッチ |
| `reverserPosition` | 有効運転台を基準としたレバーサ位置 |
| `isNeutral` | ノッチがNか |
| `isEmergencyBrake` | マスコンが非常位置か。TIMSの集約した非常要求とは別 |

キーが「切」でも操作状態は取得する。有効運転台なし、対応マスコンの欠損・無効・重複、取得元の無効化などで読めなければ`hasCabState`をfalseにし、`cab`を初期値へ戻す。`hasCabState`がfalseのときは`carIndex`を含めて使用しない。

取得した値はマスコンのStateへの参照ではなく、収集時点の値として保持する。収集後のキー・ノッチ操作は次回の`CollectInput()`で反映する。

ATC本体は入力元のインターフェースだけを参照し、TIMSに依存しない。運転台の選択と機器取得はIntegrationのアダプターが担当する。

## 車両質量と受信機の取り付け位置

ATC Prefabの`TrainAtcMassInputAdapter`が、`TrainRoot.ConsistDefinition`とTIMSから勾配補正用の入力を収集する。Controllerは`ITrainAtcMassInputSource`を参照し、車上LogicからTrainRoot・TIMS・応荷重装置を直接参照しない。

各号車について、編成定義の`emptyMassKg`・`lengthM`と、TIMSのLocal Busにある`BrakeControlDeviceTimsBusSource.MassKgKey`を読む。TIMSの値は応荷重装置に基づく空車質量込みの総質量であり、定義の空車質量を再び足さない。積載分を`max(0, 測定質量 − 空車質量)`として求め、その値を定義の空車質量へ加える。測定値が空車質量より小さい場合は定義の空車質量を下限とする。

| 項目 | 内容 |
| --- | --- |
| `Input.hasCarMasses` | 全号車の質量と中心位置、両端の受信機取り付け距離を取得できたか |
| `Input.cars` | 固定前側から順に並んだ`TrainAtcCarInput`の列 |
| `TrainAtcCarInput.massKg` | 空車質量と測定した積載分を含む車両総質量[kg] |
| `TrainAtcCarInput.centerDistanceFromFrontM` | 固定前端から車両中心までの距離[m] |
| `Input.frontReceiverDistanceFromFrontM` | 固定前端から固定前側の受信機までの取り付け距離[m] |
| `Input.rearReceiverDistanceFromFrontM` | 固定前端から固定後側の受信機までの取り付け距離[m] |

車両中心は、前の号車までの車両長の合計に当該号車の半分の長さを加えて求める。受信機の取り付け距離は、その車両の中心距離から`OffsetFromCarCenterM`を引いて求める。受信機の静的な取り付け位置だけを使用し、移動後のTrackSampleをATCの位置推定や勾配計算へ取り込まない。

全号車の入力と受信機の取り付け距離が揃った場合だけ`hasCarMasses`をtrueにする。TrainRoot・編成定義・TIMS・号車の測定質量がない、質量や車両長が非正または不正、受信機がないなどの場合はfalseにし、車両リストと取り付け距離を初期値へ戻す。前回の質量を残さない。質量入力の不成立はブレーキパターン生成の失敗として扱い、パターンを消す。

パターン生成時は有効運転台側の受信機位置を基準とし、レバーサが指定した固定前後方向で各サンプルの車両中心位置を求める。勾配の取得・経路外の代用・質量による補正と積分は[ブレーキパターン仕様](TrainAtcBrakePatternSpecification.md)の第4節に記載する。

## TIMSのブレーキ設定

`TimsRoot`が`ITimsMasterBusSource`として、`TimsSettingsAsset`の常用ブレーキ設定をMasterBusへ公開する。通常ノッチごとの減速度はkm/h/sからm/s²へ変換する。公開は既存のMaster収集周期に従い、初回は即時、その後の初期値は0.25秒ごととする。

ATC Prefabの`TrainAtcBrakeSettingsInputAdapter`が、同じTrainRoot配下のTIMSのMasterBusを読み、`TrainAtcController.CollectInput()`で次の値を毎tick取り込む。ATCのAssemblyからTIMSを直接参照せず、`ITrainAtcBrakeSettingsInputSource`を通してコピーする。

| 項目 | 内容 |
| --- | --- |
| `Input.hasBrakeSettings` | 減速度表・刻み数・常用最大段の3項目を確認できたか |
| `Input.brakeSettings.brakeTargetDecelerationsMps2` | B1から常用最大まで、通常ノッチごとの設定減速度[m/s²] |
| `Input.brakeSettings.brakeSubstepCount` | 通常ノッチ間の刻み数 |
| `Input.brakeSettings.maximumServiceBrakeStep` | 常用最大の刻み段。0は緩解 |

刻み段はB1を1stepとし、常用最大は`(通常ノッチ数 − 1) × 刻み数 ＋ 1`で求める。B7・4刻みなら常用最大は25step。減速度表は刻み段へ展開せず、通常ノッチの値を保持する。

`TrainAtcNotchHelper.TryGetNearestBrakeStep(decelerationMps2, Input.brakeSettings, out brakeStep)`で、指定した減速度[m/s²]に最も近い刻み段を取得する。TIMSと同じ式で各刻み段の減速度を補間し、緩解の0step（減速度0）から常用最大までを比較する。差が同じ場合は小さい段を選ぶ。B1=0.5m/s²、B2=1.0m/s²、4刻みで0.8m/s²を指定した場合は、0.75m/s²の3step（B1-2）を返す。入力減速度・設定が不正ならfalseを返し、`brakeStep`は0とする。この場合は選択結果として使用しない。

TIMS未取得・複数配置・無効化、タグ欠損、空の減速度表、非正の刻み数・最大段、表の長さと最大段の不一致、減速度の負数・NaN・Infinityでは取得失敗とし、`hasBrakeSettings`をfalse、数値を0、表を空にする。前回の値やTIMSの設定Assetへの参照を残さない。TIMS側でも不正な設定や無効化時には3つのタグを削除する。

設定の受信、減速度から刻み段を選択するHelper、Logicから降下中の常用ノッチ制御への受け渡しまで実装済みとする。設定未取得の場合は非常作動原因として扱い、常用最大段の初期値0で緩解しない。キー切・レバーサ中立を確認できた場合の解除を優先する。

## TIMSへのブレーキ出力

`Output.brake.brakeStep`へ`State.brake.currentBrakeStep`を反映する。非常保持中は`Output.brake.isEmergency`をtrue、常用段を0にする。TIMSの`TimsNotchController`は同じ編成のATCを自動取得し、毎tickこの2項目を読んで手動段と合成する。現在の更新順では前tickのATC指令を使い、表示収集周期による待ち時間は設けない。

常用の試験では、キー入・有効運転台・レバーサ前進または後退・正常な受信電文とパターンを確認し、マスコンをNにする。降下していない区間ではパターン超過でTIMSのノッチ表示とBC圧が上がり、パターン速度から`Settings.normalBrakeReleaseMarginKmh`を引いた速度以下になればATC段が0へ戻る。降下区間では基準段から速度偏差テーブルに従って刻み段を切り替える。ATCの非常が保持されている場合は、原因を解消し、停止中にマスコンを非常位置へ入れてからNへ戻す。

## 正常判定と有効状態

- `isHealthy`は毎回判定し直す。位置の初期設定が未完了・位置不明、または有効運転台・対応マスコン・キー状態などを取得できない場合はfalse。号車index・ノッチ・レバーサ位置などの取得値が不正な場合もfalseにする。
- キーが「切」と正常に取得でき、位置更新も正常な場合は、`isHealthy`はtrue、`isAtcPowerOn`と`isAtcEnabled`はfalse。
- キーが「入」と取得できれば`isAtcPowerOn`はtrue。`isAtcEnabled`は正常・キー入・レバーサが中立以外の3条件で判定する。
- 操作入力の禁止、レバーサ中立、非常ノッチは取得可能な状態であり、それだけで`isHealthy`をfalseにしない。
- 運転台状態が取得不能・不正値の場合は運転台状態と`currentTelegram`を初期値へ戻し、前回値を使わない。運転台入力だけの異常なら、次回の取得が正常になれば正常状態へ戻る。位置不明は明示的な位置補正まで維持する。
- ATCが無効の場合は、無信号時間を0へ戻し、`ClearBrakePattern()`で前回の経路と速度配列を消す。無効時も`UpdateBrakeState()`を呼び、キー切・レバーサ中立を確認できた場合はATC要求と非常の保持状態をクリアする。入力取得失敗で初期値になったキー切や中立では解除しない。
- キー入・レバーサが前進または後退の場合、無信号時間の超過・保持可能なパターンなし・ブレーキ設定未取得・速度未取得・位置不明・パターン計算失敗、および運転台状態の取得失敗では、`State.brake.isEmergencyHold`をtrueにし、`Output.brake.isEmergency`へ出力する。原因が解消し、測定速度の大きさが0.1km/h以下で、マスコンが非常位置の場合に解除する。キー切・中立の確認によるクリアを優先する。

## 有効運転台側の電文

前側運転台なら`Input.frontTelegram`、後側運転台なら`Input.rearTelegram`をコピーして`State.currentTelegram`に設定する。レバーサによる前進・後進とは別に、有効運転台の固定前後で選択する。

選択側が未受信なら`currentTelegram`はnullとなる。反対側の電文で補わない。未受信は無信号として後続の処理で扱い、それだけでATC自体の正常判定をfalseにはしない。`isValid = false`の不正電文もその状態を保持する。

### 無信号時間とパターンの保持

電文がnull、`isValid = false`、または現在のEdge IDと`currentTravelDirection`に対応するキーがない場合を、無信号時間の積算対象とする。`CheckBrakePatternInput()`で毎tickの`Input.deltaTimeSeconds`を`State.noSignalElapsedSeconds`へ加算する。電文が存在するだけでは積算を解除せず、新しい電文からパターンを生成し、現在位置で使用できることまで確認できた場合だけ0へ戻す。

無信号時間が`Settings.noSignalTimeoutSeconds`以下の間は、前回採用した経路・速度配列・過走防護モードを保持する。受信結果の`currentTelegram`は今回のままとし、前回電文で置き換えない。保持中も位置を積算し、現在の経路内距離・サンプルindex・補間比率から許容速度を毎tick更新して、常用ブレーキの照査を続ける。

前回の有効なパターンがない、設定が0、無信号時間が閾値を超えた、または現在位置が保持経路の外に出た場合は、パターンを消して即時に非常要求を出す。運転台またはレバーサを切り替えた場合も、前回の経路を引き継がない。キー切・中立・ATC無効の場合はパターンと無信号時間を消す。

計算用入力・設定の不正は、`TryGetBrakePatternInputs()`でまとめて確認し、猶予を設けない。許容時間が負数・NaN・Infinityの場合も不成立とする。位置不明や新しい電文の経路・勾配などによる計算失敗も即時の不成立とする。`Calculate()`は`UpdateBrakePattern()`の前に`CheckBrakePatternInput()`を呼ぶ。同関数は入力・電文・猶予・保持経路を確認し、`Create`（新規生成）・`Retain`（前回保持）・`Clear`（消去）のいずれかを決める。判定結果は`State.brakePatternUpdateDecision`へ毎tick置き換えて保持する。型は`TrainAtcContext.cs`の`TrainAtcBrakePatternUpdateMode`と`TrainAtcBrakePatternUpdateDecision`で定義する。`UpdateBrakePattern()`はStateの判定結果を読んで生成・保持・消去だけを行い、入力や電文を判定し直さない。実際の生成に失敗した場合も消去する。非常判定で電文やパターン位置をもう一度確認しない。常用ノッチ用の速度偏差テーブルの不正は、従来どおり常用最大段へ切り替える。


## TIMSへの表示情報

`TrainAtcBrakePatternLogic.UpdateCurrentPattern()`は、更新前の判定と実際の生成結果に基づく使用可否・path内距離・サンプルindex・補間比率を受け取る。保持時の位置情報は`CheckBrakePatternInput()`で一度だけ求め、新規生成時の位置情報は生成後に`Calculate()`で一度だけ求める。前後サンプルの速度と目標速度の二乗を補間し、現在地点の常用・非常の許容速度と目標速度、降下状態、予告・降下区間内かどうか、path内距離と使用可否を`State.brakePattern`へ保持する。現在地点のパターンを取得できなければ、これらの値を初期値へ戻す。`ClearBrakePattern()`でも同じ情報を消す。保持中も現在位置から計算し、許容速度を固定しない。

ブレーキ判定は`State.brakePattern.hasValidPattern`とStateの許容速度を使う。`TrainAtcBrakeLogic.UpdateBrakeState()`まで完了した後、`TrainAtcOutputLogic.UpdateOutput(context)`を呼ぶ。同関数はContextだけを引数に受け、Stateを変更せず、表示用情報とブレーキ指令をOutputへまとめて反映する。常用パターンの予告区間内かつ現在の測定速度の大きさがStateの目標速度以上なら、`Output.isPatternApproaching`をtrueとする。非常保持中は`Output.brake.isEmergency`をtrue、常用段を0にし、それ以外は`State.brake.currentBrakeStep`を出力する。

`Output.isOrpActive`は、有効なパターンがあり、採用した過走防護モードが`Restricted`で、現在位置が非常パターンの予告・降下区間内で、`State.brakePattern.emergencyPatternTargetMps`が0m/sの場合だけtrueとする。前後サンプルのどちらかの`isPatternApproachSection`がtrueなら区間内として扱い、現在速度は判定条件に含めない。無信号の猶予中も保持パターンの現在位置から更新し、パターンが不成立ならfalseへ戻す。

ATC Prefabの`TrainAtcTimsDisplayAdapter`が`ITimsMasterBusSource`を実装し、TIMSの収集時に`TrainAtcController.Context`から最新の計算結果を読み、同じ編成のMasterBusへ公開する。`Output.isOrpActive`がtrueの場合、現示用の`ATC/PatternAllowSpeedKmh`を0km/hにする。それ以外は現在位置の常用パターン速度を公開する。制御用のState・Outputの許容速度は計算した値を保持する。収集周期は`TimsCommunicationController.masterCollectionIntervalSeconds`で設定し、初期値は0.25秒。通常の収集はEquipment計算より前なので、前tickまでに確定した表示情報を取得する。車上ATC本体からTIMS Assemblyを参照しない。

`ATC/IsNormalBrakeRequired`をBoolとして公開し、`State.brake.isNormalRequired`をそのまま渡す。常用要求・停止保持中はtrue、キー切・中立による解除後はfalseとなる。表示Adapter・ATC・TrainRootの無効化時はfalseへ戻す。公開は他のATC表示と同じMaster収集周期に従い、ブレーキ指令そのものの毎tickの受け渡しとは別に行う。

`ATC/IsEmergencyBrakeRequired`をBoolとして公開し、`Output.brake.isEmergency`を渡す。作動原因が解消しても非常を保持している間はtrueとし、解除後はfalseへ戻す。パターン不成立時も公開する。モニターPrefabの「ATC非常」はこのタグへ接続し、常用と同じMaster収集周期で表示する。表示Adapter・ATC・TrainRootの無効化を確認した場合もfalseへ戻す。

`Output.signal`には`TrainAtcSignal`を保持する。ATC無効は`None`。ATCが有効で、現在位置の常用パターン速度か目標速度が0m/sなら`Red`、それ以外は`Green`とする。有効でもパターンを取得できなければ、速度の初期値0として`Red`にする。MasterBusには`ATC/Signal`としてIntを公開し、`None=0`、`Red=1`、`Green=2`とする。

収集を待つ間もATCの位置更新・パターン計算・表示用速度の計算は毎tick行う。無信号の猶予中は保持パターンから表示を更新し、猶予超過などによるパターン不成立時の消灯は次の収集時に反映する。表示Adapter自体の無効化では収集を待たずに表示を消す。

公開するのは、電源・有効・正常・故障・常用要求・非常要求・ORP状態、常用パターン接近状態、Signal、現在地点の常用・非常パターン速度、パターン全体、サンプル間隔、経路長、現在path内距離、経路のEdge ID列。タグ一覧と欠損時の扱いは[TIMS Bus一覧](../Tims/BusTagMap.md)に記載する。

速度計の三角表示は常用パターン速度を設定した刻み幅（既定5km/h）で低い方へ切り捨てて表示する。モニターPrefabの「ATC電源」「ATC」「故　障」「ATC常用」「ATC非常」とORPの画像は接続済み。「ATC開放」の表示項目は未公開。ブレーキ指令の接続は「TIMSへのブレーキ出力」に記載する。
