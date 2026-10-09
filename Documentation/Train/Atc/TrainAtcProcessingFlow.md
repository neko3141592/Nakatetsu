# 車上ATC 全体処理仕様

本書は、車上ATCの処理順と各工程の役割を定める。関数の呼び出し構造を整理する際は、この処理順を基準とする。Stateは工程ごとに分け、全体のContextにまとめる。現在の実装の入口と参照構成は、第7節に記載する。

## 1. 全体の処理順（確定）

制御は1〜7の順に行い、最後に8で外部向けのOutputを生成する。

| 順番 | 工程 | 役割 |
| --- | --- | --- |
| 1 | 入力をスナップショット | 操作状態・測定速度・両端の受電結果などを、今回の更新で使用する入力として取り込む |
| 2 | 有効状態・使用受電器を決定 | マスコンキー・運転台方向・レバーサから、ATCの有効状態、使用受電器と電文を決める |
| 3 | 列車位置を更新し、照査位置を確定 | 既知の使用側を優先して基準端を進め、他端を受電器間隔から再解決する。その後、使用側の最新位置と照査方向を確定する |
| 4 | 入力・受信状態を判定 | 入力の取得可否と基本的な正常性、使用受電器の電文を確認し、採用・保持・使用不可を判定する |
| 5 | 防護方式を確定 | 受信したNormal・Restricted・Noneに、停止限界・終端方向によるORP保持と開扉操作による解除を反映する |
| 6 | パターンを更新 | 防護方式に応じて常用・非常・ORPパターンを生成し、現在位置の許容速度・目標速度・接近状態を求める |
| 7 | ブレーキ出力を決定 | パターンを照査し、ATC常用・非常の要求、停止保持、非常保持・解除を決める |
| 8 | Outputを生成 | 確定したStateから、TIMS向けの表示情報とブレーキ指令を生成する |

## 2. 各工程の扱い

### 1. 入力をスナップショット

今回の更新で使用する操作状態・測定速度・両端の受電結果・計算用入力を取り込む。取得できたかどうかも保持し、取得失敗による初期値を正常な操作状態として扱わない。

両端の受電結果を取り込み、使用する受電器の選択は工程2で行う。無信号時に、受信電文を前回の電文で置き換えない。

開扉操作は`hasDoorOpeningOperation`と`doorOpeningOperationRevision`で取り込む。TIMSが受け付けた開扉指令の番号を使用し、閉扉指令ではこの番号を変えない。全閉未確認やドア状態の取得失敗を、開扉操作として扱わない。

### 2. 有効状態・使用受電器を決定

`TrainAtcOperationLogic.UpdateOperation()`は今回のマスコンキー・運転台方向・レバーサから、ATCの有効状態、使用受電器と電文を決定する。位置更新より先に呼び、前回の照査位置と方向は消去する。今回の照査位置は、工程3の位置更新後に同じLogicの`UpdateCurrentPosition()`で確定する。

#### ATCの有効条件（確定）

| 項目 | 条件・動作 |
| --- | --- |
| `isAtcPowerOn` | 常にtrue。キー切・レバーサ中立・操作状態の取得失敗によってfalseにしない |
| `isAtcEnabled` | 操作状態を確認でき、マスコンキーが投入され、レバーサが前進または後退の場合にtrueとする |
| キー切・レバーサ中立 | `isAtcEnabled`をfalseとし、速度照査を行わない。位置更新は継続する |
| 操作状態の取得失敗 | 正常なキー切・中立と区別し、取得失敗の結果を工程4へ渡す |
| `selectedReceiver` | ATC有効時は有効運転台側の固定前側・固定後側を選ぶ。レバーサの方向で受電器を切り替えない |

この有効条件と、工程5で決定するNormal・Restricted・Noneの防護方式は別の情報として扱う。

#### 読み書きする情報

以下の状態は`State.operation`に保持する。PositionLogicはこの選択結果を参照し、OperationStateを書き換えない。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `hasCabState`・`cab`：有効運転台の号車・前後側、マスコンキー、レバーサ、マスコン非常位置 |
| Input | 読む | `frontTelegram`・`rearTelegram`：使用受電器側の電文を選択するための両端の受信結果 |
| State | 更新する | `hasCabState`・`cab`：確認した操作状態と、その確認結果 |
| State | 更新する | `isAtcPowerOn`・`isAtcEnabled`：今回の電源状態と有効状態 |
| State | 更新する | `selectedReceiver`・`currentTelegram`：使用受電器と、その側の今回の電文。未受信はnull |
| State | 消去後に更新する | `hasCurrentPosition`・`currentPosition`・`currentTravelDirection`：`UpdateOperation()`で消去し、位置更新後の`UpdateCurrentPosition()`で確定する |
| State | 読む | `State.position.isPositionInitialized`、選択端の位置既知フラグと位置：`UpdateCurrentPosition()`で使用する |
| Output | 読み書きしない | 外部向けの表示情報・ブレーキ指令は工程8で生成する |

照査方向は運転台側とレバーサから編成の固定前後方向を求め、選択端の最新位置の`frontFacesAtoB`でEdge上のA→B・B→Aへ変換する。選択端が不明なら照査位置は設定せず、方向をUnspecifiedにする。非使用側だけが不明の場合は、既知の使用側から照査位置を設定できる。

キー切・レバーサ中立では照査用受電器を未選択とし、`hasCurrentPosition=false`、`currentPosition=null`、`currentTelegram=null`、`currentTravelDirection=Unspecified`とする。Inputの両端電文やPositionStateの保持位置は消去しない。位置更新の基準端は工程3で決める。

親Logicは`UpdateOperation()`前の`selectedReceiver`を保存し、今回の選択結果との差から受電器変更を判定する。工程4へ引数で渡し、PatternStateへ運転台側などの写しを追加しない。

### 3. 列車位置を更新し、照査位置を確定

`TrainAtcPositionLogic.UpdatePosition()`は、工程2で選んだ使用側が既知ならその側を基準にする。使用側が不明でも他側が既知なら、その既知側を基準にする。基準端を測定速度で進めた後、その最新位置と両端受電器の取り付け距離の差から、他端の位置を毎tick求め直す。両端を独立して速度積算する方式は使用しない。

位置更新直後、親Logicが`TrainAtcOperationLogic.UpdateCurrentPosition()`を呼ぶ。最新の選択端が既知の場合だけ、今回の照査位置と方向をOperationStateへ設定する。子Logic同士は呼び合わない。

#### 読み書きする情報（確定）

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `hasSpeedMeasurement`・`signedSpeedMps`・`deltaTimeSeconds`：使用側の移動量 |
| Input | 読む | `frontTelegram`・`rearTelegram`：位置を一意に解決するための受信経路 |
| Input | 読む | `hasCarMasses`・`frontReceiverDistanceFromFrontM`・`rearReceiverDistanceFromFrontM`：取り付け距離の取得可否と、非使用側を再解決する受電器間隔 |
| State | 読む | `State.operation.selectedReceiver`・`isAtcEnabled`・`hasCabState`・`cab`：位置更新の基準端を決める操作結果 |
| State | 読む・更新する | `isPositionInitialized`・`isFrontPositionKnown`・`isRearPositionKnown`：初期化と固定前後それぞれの位置把握の結果 |
| State | 読む・更新する | `frontPosition`・`rearPosition`：各端の最後に確認できたEdge ID・Edge内距離・編成の向き |
| State | 読む | `IsPositionKnown`：`isFrontPositionKnown && isRearPositionKnown`を返す読み取り専用の診断集約。照査の有効条件には使用しない |
| State | 読む | `State.pattern.atcEdgePath`・`pathStartTravelDirection`：前回採用した経路と始点方向 |
| その他の参照 | 読む | `Graph`・`atcEdgesById`：Edgeの長さ、接続、定義済み経路 |
| Output | 読み書きしない | 外部向けの表示情報・ブレーキ指令は工程8で生成する |

前側・後側は編成の固定前側・固定後側である。運転台変更や後退によって、保持位置や既知フラグの前後を入れ替えない。位置把握の結果はPositionStateだけが所有する。位置不明側の保持位置は診断記録であり、制御用の既知位置として使わない。

#### 基準端と移動量

ATC有効時は、`selectedReceiver`が既知なら基準にする。選択端が不明なら他端の既知位置を基準にして、選択端を相対再解決する。無効時は、操作状態を取得できていればその運転台側の既知位置を優先し、不明なら他端の既知位置を使う。操作状態が取得できない場合は既知の固定前側を優先し、固定後側だけが既知なら固定後側を使う。両端が不明なら位置更新できず、明示補正が必要となる。基準端を表す別のStateは保存しない。

今回の基準端の移動量は、既存の式を維持する。

```text
符号付き移動距離[m] = signedSpeedMps[m/s] × deltaTimeSeconds[s]
```

実際の移動方向は測定速度の符号と基準端の`frontFacesAtoB`から求める。Edge境界では受信経路・前回採用経路・Graphを参照し、一意な移動先を解決する。共通部分で選択Edgeが変わった場合は同じ物理区間へ対応し直すが、分岐後に別の枝へ位置を付け替えない。

基準端が既知で移動解決も成功した場合、その端の最新位置を採用する。不明端の最後に確認できた位置は速度積算に使わない。基準端自身の移動解決に失敗した場合、そのtickの新しい相対位置も確定できないため両端を不明にし、古い他端の既知フラグを残さない。経過時間が不正な場合、または経過時間が正のときに測定速度を取得できない・値が不正な場合も両端を不明にする。両端不明からの復帰は明示的な`TryCorrectPosition()`による補正だけとする。経過時間0の位置更新では移動距離を0とし、PositionLogicは速度欠損だけで既知フラグを落とさない。工程4の速度入力確認は別に行い、速度欠損・不正は入力不正とする。

この変更では、受信時刻と速度積分の時刻を揃えるための積分式変更は行わない。

#### 他端の相対解決と復帰

基準端を更新した後、固定前端からの取り付け距離を使って次の相対変位を求める。

```text
基準端から他端への固定前方向の変位[m]
    = 基準端のReceiverDistanceFromFrontM − 他端のReceiverDistanceFromFrontM
```

基準端の最新位置をコピーし、その編成の向きに沿って相対変位を辿る。同じEdge内で収まる場合は距離から求め、Edgeを跨ぐ場合は基準端側の今回の電文、保持経路、Graphから一意な接続を順に解決する。複数のEdgeを跨ぐ場合も同じ手順で扱う。解決できた位置だけを採用し、対象端の既知フラグをtrueにする。

`hasCarMasses`による取得確認、両端距離の有限・非負、固定後側距離が固定前側距離以上であることを相対解決の条件にする。間隔0も許可する。これらの条件を満たさない場合や、一意に辿れない分岐では、相対解決の対象端だけを不明にし、最後に確認できた位置を診断用に残す。既知の使用側を基準にできている場合、その位置、パターン、照査を非使用側の失敗で止めない。使用側自身が相対解決の対象であり、今回も不明のままなら、その側の照査は使用不可となる。使用側固有の入力不正は、工程4の既存の入力判定に従う。

片側だけが不明なら、毎tickその時点の既知基準端の最新位置から再解決する。使用受電器かどうかによらず、経路を一意に解決できれば明示補正や実位置の読み直しなしで既知へ戻る。不明端の過去の保持位置から速度積算を再開して復帰することはない。基準端の最新位置が得られなければ、新しい相対位置は採用できない。

受電器切替時に実位置を取り直す処理や特別な初期化は行わない。新しい使用側が既知なら、その保持位置を今回の基準にして更新する。不明でも他側が既知なら相対再解決を試し、今回も不明のままなら工程4以降で使用不可として扱う。切替時の旧パターン保持は禁止し、新しい側の有効電文から生成し直す。

#### 工程4との役割分担

工程3の経路参照は位置解決だけに使用する。進路の開通状態だけを理由に、一意に特定できる位置の解決を省略しない。無信号の積算・猶予、電文・パターンの採用可否は工程4で判定する。

工程4はATC有効時に選択端だけの既知フラグと`hasCurrentPosition`を要求する。非使用側の不明や、診断用`IsPositionKnown=false`だけを理由に入力不正としない。ATC無効時は、少なくとも片側が既知なら位置入力を正常とし、両側不明を正常な無効状態へ置き換えない。

### 4. 入力・受信状態を判定

入力の取得可否と基本的な正常性をまとめて確認し、工程2で選択した受電器の電文を確認する。無信号時間、電文の使用可否、前回パターンの保持可否から、今回の処理を決定する。

#### 読み書きする情報

入力確認・受信判定の結果、採用候補の経路情報、確認済みの計算用情報を`State.validation`へ保持する。採用・保持・使用不可は`TrainAtcValidationResult`で表す。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `deltaTimeSeconds`：無信号時間の積算に使う経過時間[s] |
| Input | 読む | `hasSpeedMeasurement`・`signedSpeedMps`：測定速度の取得可否と値 |
| Input | 読む | `hasCarMasses`・`cars`：各車両の質量と中心位置の取得可否、および計算用の値 |
| Input | 読む | `frontReceiverDistanceFromFrontM`・`rearReceiverDistanceFromFrontM`：受電器の取り付け位置。使用する側の計算用位置を確認する |
| Input | 読む | `hasBrakeSettings`・`brakeSettings`：ブレーキ設定の取得可否、刻み段数・常用最大段・設定減速度 |
| State | 読む | `hasCabState`・`cab`・`isAtcEnabled`：工程2で確認した操作状態と有効状態 |
| State | 読む | `isPositionInitialized`・`isFrontPositionKnown`・`isRearPositionKnown`・`hasCurrentPosition`：工程3で確定した選択端の位置確認結果。無効時は片側以上の既知を要求する |
| State | 読む | `currentPosition`・`currentTravelDirection`：今回の電文に対応する経路情報を取得するための位置と照査方向 |
| State | 読む | `currentTelegram`：工程2で選択した受電器側の電文 |
| その他の参照 | 読む | `Graph`・`atcEdgesById`：現在の受電器位置のEdgeに対応する軌道回路を取得する |
| State | 読む | 前回採用したパターン・経路・始点方向：選択した受電器が経路上にあり、照査方向が経路の方向と一致するかを確認する |
| State | 読む・更新する | `noSignalElapsedSeconds`：連続した無信号時間[s] |
| Settings | 読む | `noSignalTimeoutSeconds`：無信号の猶予時間[s] |
| Settings | 読む | サンプル間隔・最高速度・計算用減速度など、計算に必要な基本設定 |
| State | 更新する | 入力確認・受信判定の結果、および採用・保持・使用不可の判定結果 |
| State | 更新する | 採用候補の経路情報と、確認した計算用入力を後続工程へ渡すための情報 |
| Output | 読み書きしない | 外部向けの表示情報・ブレーキ指令は工程8で生成する |

#### 判定の順序（確定）

1. **入力を確認する。** 操作状態・位置・測定速度・車両質量・ブレーキ設定などが取得でき、計算に必要な基本値が正常かを確認する。工程2・3で確認済みの操作状態と位置については、その結果を利用する。
2. **受信状態を確認する。** 電文の有無と、現在の受電器位置に対応する軌道回路の電文を読めるかを確認し、無信号時間を更新する。現在位置・照査方向に対応する経路情報も確認する。
3. **採用・保持・使用不可を決定する。** 無信号の場合は、猶予時間と前回パターンの保持可否を確認する。

入力不正は、無信号の猶予で隠さず使用不可とする。前回の有効なパターンがあっても、計算に必要な入力の取得失敗や基本値の不正を、無信号時の保持で補わない。

| 判定結果 | 後続の処理 |
| --- | --- |
| 新しい情報を採用 | 工程5で防護方式を確定し、工程6で新しいパターンを生成する |
| 前回パターンを保持 | 前回採用した経路と防護方式を維持し、工程6では現在位置のパターン情報だけを更新する |
| 使用不可 | 使用できるパターンを残さず、工程7で運転モードに応じたブレーキ要求を決定する |

無信号時は、設定した猶予内で、前回の有効なパターンを引き続き使用できる場合だけ保持する。猶予を超えた場合、または保持できるパターンがない場合は使用不可とする。

使用受電器を切り替えた場合は、取り付け位置を基準にした勾配補正の条件が変わるため、旧パターンを保持しない。新しい電文があれば生成し直す。受電器の変更は工程2の前後の選択結果を`TrainAtcLogic`で比較し、工程4へ引数で渡す。PatternStateへ運転台側などの写しを追加しない。

#### 無信号の定義とタイマー（確定）

次のいずれかの場合を無信号とする。

- 電文がない。
- 電文を読めない。現在の受電器位置に対応する軌道回路の電文がない場合を含む。

| 受信状態 | `noSignalElapsedSeconds`の更新 |
| --- | --- |
| 無信号 | 正常な経過時間`Input.deltaTimeSeconds`を加算する |
| 無信号ではない | 常に0へリセットする |

タイマーの積算・リセットは工程4で行う。無信号でない場合のリセットは、他の入力の正常性や、後続の経路採用・パターン生成の成功を条件にしない。電文を読める状態に戻った時点で、後続工程の成功を待たずリセットする。

経過時間そのものが不正な場合は入力不正として扱い、その値をタイマーへ加算しない。

確認済みのキー切・レバーサ中立では受電器を選択していないため、無信号時間を積算せず0へ戻す。ATCを有効にした場合は、その受電器の今回の受信結果から判定を開始する。

#### 後続工程との役割分担（確定）

工程4では判定結果を作り、実際のパターン生成・消去は工程6で行う。防護方式の確定は工程5、非常ブレーキの判定は工程7で行う。

親Logicは工程5の後、工程6の前に`TrainAtcValidationLogic.ValidateProtectionSettings`を呼ぶ。ORP保持・解除を反映して今回使用する方式に必要な設定を確認し、不正な場合はValidationStateを使用不可にする。受信した方式だけを基準に設定を確認しない。

工程5・6では、経路の接続や計算結果など、その工程で初めて分かる失敗を確認する。工程4で確認済みの入力の取得可否・基本的な正常性と、無信号時間・猶予の判定は繰り返さない。後続工程で初めて判明した失敗も、使用不可の結果としてブレーキ判定とOutput生成へ渡す。

電文には軌道回路IDがないため、現在Edgeの軌道回路IDが定義されていることを確認し、現在Edge IDと照査方向のキーで経路を読む。入力の許容範囲はValidationLogicと、そのHelperで確認する。

### 5. 防護方式を確定

新しい情報を採用する場合は、受電器から取得した経路情報の`Normal`・`Restricted`・`None`に、ORP保持・開扉時解除を反映し、今回使用する方式を確定する。

有効な`Normal`・`Restricted`を採用したとき、その方式、停止限界のEdge IDと、その終端Edge上の進行方向を保持する。停止限界・終端方向が同じ間に`None`を受信した場合だけ、以前の方式を使用する。新しい有効な`Normal`・`Restricted`を受信したら、同じ対象でも使用方式と保持方式を更新する。現在Edgeや経路の先頭が変わったことだけでは解除しない。新しい経路は今回の受信値を使用し、受信電文やInputを書き換えない。

停止限界または終端方向が変わると以前の保持を解除する。開扉操作でも保持を解除し、今回の受信方式を採用する。開扉操作を確認したtickでは再保持しないが、次tick以降に有効な`Normal`・`Restricted`を採用すれば、同じ停止限界・終端方向でも保持を開始できる。

#### 読み書きする情報（確定）

工程5で決める防護方式は`TrainAtcProtectionModeState.overrunProtectionMode`だけで管理する。工程6〜8もこの値を参照し、PatternStateなどに同じ方式を重複して保存しない。`isProtectionModeKnown`で方式を確定できたかを表す。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `hasDoorOpeningOperation`・`doorOpeningOperationRevision`：開扉操作番号の取得可否と、今回確認した番号 |
| State | 読む | 工程4の採用・保持・使用不可の判定結果と、採用候補の経路情報 |
| State・Graph | 読む | 現在位置・照査方向とEdgeの接続。停止限界が経路末尾と一致することを確認し、終端方向を求める |
| State | 読む・更新する | `hasHeldOrp`・`heldProtectionMode`・`heldStopAtcEdgeId`・`heldStopTravelDirection`：防護方式保持の有無・方式・対象 |
| State | 読む・更新する | `doorOpeningOperationRevision`：処理済みの開扉番号。同じ操作を繰り返し解除の契機にしない |
| State | 読む・更新する | `overrunProtectionMode`：保持・解除を反映した今回使用する方式。パターン保持時は前回値を維持する |
| State | 更新する | `isProtectionModeKnown`：方式の確定結果。取得失敗や使用不可の場合はfalseにする |
| Output | 読み書きしない | 外部向けの表示情報・ブレーキ指令は工程8で生成する |

#### 採用・保持・使用不可の扱い（確定）

最初に開扉操作番号を比較し、新しい操作なら処理済み番号を更新して`hasHeldOrp`を解除する。停車速度や全閉状態は解除条件にしない。キー切・レバーサ中立、無信号、入力不正の場合もこの処理を行う。開扉入力を取得できない場合は、新しい操作があったとは判定しない。

| 工程4の判定結果 | 工程5の処理 |
| --- | --- |
| 新しい情報を採用 | 受信方式と停止限界・終端方向を確認し、ORP保持・解除を反映した方式を保存する。工程6では今回の経路と方式でパターンを作り直す |
| 前回パターンを保持 | 使用中の方式と確定結果を維持する。開扉操作でORP保持対象を解除しても、無信号猶予中は既存パターンと方式をそのまま使用する。次に有効なNoneを受信すればNoneを採用する |
| 使用不可 | `isProtectionModeKnown`をfalseにし、防護方式を使ったパターン生成へ進まない。ORP保持情報だけで受信・入力不正を補わない |

方式を取得できない場合や、Normal・Restricted・None以外の不正な方式の場合も使用不可とする。`None`は過走防護なしという正常な指定であり、方式の取得失敗を表す値として使用しない。`isProtectionModeKnown`がfalseの場合は、方式の値をパターン生成や照査に使用しない。

工程6で生成に失敗した場合は、`TrainAtcPatternLogic`が自分のパターンを消去し、`isValid`をfalseにする。`TrainAtcLogic`はその結果を集約して`TrainAtcState.isAtcHealthy`をfalseにする。工程6からProtectionModeStateやValidationStateを直接変更しない。新しい方式と古いパターンを組み合わせて照査しない。パターンを保持する場合は方式も維持し、パターン側に方式の写しを持たせない。

### 6. パターンを更新

新しい情報を採用する場合は、工程5で確定した防護方式と経路・制限情報から、常用・非常パターンを生成する。独立ORPパターンはRestrictedの場合に生成する。

今回の防護方式は、工程5が`TrainAtcProtectionModeState`に保存した`overrunProtectionMode`から取得する。パターン側へ方式をコピーしない。

前回パターンを保持する場合は再生成せず、保持した経路とパターンを使用する。

生成・保持のどちらの場合も、今回の現在位置に対応する許容速度・目標速度・接近状態を更新する。保持中も、直前の許容速度や接近状態に固定しない。

生成や現在位置の参照に失敗した場合は、使用不可とする。生成内部の工程と呼び出しのルールは、以下のとおりとする。

#### 読み書きする情報（確定）

工程4で確認済みの計算用入力を使用する。Inputの取得可否・基本的な正常性と、無信号時間・猶予の判定は、この工程で繰り返さない。表中のフィールド名は既存のContextとの対応を示し、防護方式の保持先は工程5で定めた`TrainAtcProtectionModeState`とする。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `cars`：工程4で確認済みの各車両の質量`massKg`と中心位置`centerDistanceFromFrontM`。編成全体の勾配補正に使用する |
| Input | 読む | `frontReceiverDistanceFromFrontM`・`rearReceiverDistanceFromFrontM`：工程4で確認済みの受電器の取り付け位置。使用する側から各車両中心までの相対距離を求める |
| State | 読む | 採用・保持・使用不可の判定結果と、工程4の採用候補の経路情報 |
| State | 読む | `TrainAtcProtectionModeState`：工程5で確定した今回の防護方式と確定結果 |
| State | 読む | `currentPosition`・`currentTravelDirection`：経路の基準方向と、現在位置のパターン情報を求めるための位置・方向 |
| State | 読む | 工程4で確認した使用受電器の取り付け位置。パターン内に運転台側・レバーサを保存しない |
| State | 読む | `State.pattern`：保持する場合の前回採用した経路・始点方向・速度配列。方式はProtectionModeStateを参照する |
| その他の参照 | 読む | `Graph`・`atcEdgesById`：経路の接続、Edgeの長さ、常設速度制限、勾配 |
| Settings | 読む | 最高速度・サンプル間隔・常用／非常／ORPの減速度・停止目標の設定・ORPの速度と距離・線区最大下り勾配・接近予告時間 |
| State | 更新する | パターン内の経路・始点方向・経路長・サンプル間隔。運転台側・レバーサ・防護方式は重複して保存しない |
| State | 更新する | `normalPattern`・`emergencyPattern`・`orpPattern`：各サンプルの速度上限・勾配補正前の減速度・目標速度・接近状態・降下状態 |
| State | 更新する | `State.pattern.isValid`：今回の現在位置でパターンを使用できるか |
| State | 更新する | `State.pattern.distanceOnPathM`と、現在位置の常用／非常／ORPの許容速度・目標速度・接近状態、常用／非常の降下状態 |
| State | 更新する | 生成や現在位置の参照に失敗した場合の使用不可の結果 |
| Output | 読み書きしない | 表示用の値とブレーキ指令は工程8で生成する |

#### 車両中心位置・受電器の取り付け位置の用途（確定）

パターンの各サンプルでは、受電器がその地点に到達したときの各車両中心の位置を求める。受電器と車両中心の取り付け位置は、どちらも編成の固定前端からの距離であり、その差から相対距離を求め、経路の進行方向を反映する。

例えば、進行方向に対して受電器の8m後ろに車両中心がある場合、受電器がpath上の100mに到達したとき、その車両中心は92mにある。各車両中心の勾配を取得し、車両質量で重み付けして合算することで、編成全体の勾配補正を求める。

#### パターン本体と現在位置の更新（確定）

工程6は、次の2段階で扱う。

1. **パターン本体を更新する。** 新しい情報を採用する場合は生成し、保持の場合は前回の配列を維持する。使用不可の場合は消去する。
2. **現在位置の情報を更新する。** 生成・保持したパターンから、今回の現在位置での許容速度・目標速度・接近状態・降下状態を求める。使用不可の場合は、現在位置のパターン情報を初期値へ戻す。

#### 新しいパターンの生成順序（確定）

`TrainAtcPatternLogic`の入口となる関数に、次の生成順序を明示する。`TrainAtcLogic`はその入口を呼び、生成内の細かい関数を直接並べない。現在の生成工程は、以下の関数で構成する。

| 生成内の順番 | 関数名 | 担当 |
| --- | --- | --- |
| 1 | `PreparePath` | Edge列・方向・累積距離・経路長を準備する |
| 2 | `InitializePatterns` | サンプル間隔を決め、常用・非常・ORPの配列を初期化する。ORPはRestrictedの場合に用意する |
| 3 | `ApplyPermanentSpeedLimits` | 常設速度制限を反映する |
| 4 | `ApplyStopTargets` | 防護方式に応じた速度上限・停止目標を設定する |
| 5 | `CalculateGradientCorrections` | 各サンプルの編成全体の勾配補正を準備する |
| 6 | `IntegratePatterns` | 準備した減速度で終端から始点へ積分する |
| 7 | `UpdatePatternSections` | 目標速度・接近区間・降下区間を設定する |
| 8 | `AdoptPattern` | 採用した経路と、その始点Edgeでの進行方向を保存する。運転台側・レバーサ・防護方式はパターン内へコピーしない |

常用・非常・ORPの速度上限や積分範囲などの制御条件は、既存のパターン仕様を維持する。各工程は、担当するパターンと区間にだけ処理を適用する。

常用・非常・ORPとも、パターンが下がり切った地点から、その地点の速度で1秒間進む距離までを降下区間・接近区間に含める。延長距離は「下がり切った速度[m/s] × 1秒」とし、実際の走行時間を積算する処理にはしない。区間フラグと目標速度に反映し、許容速度や積分済みの曲線は変更しない。延長の境界がサンプル間にある場合は、その境界を含むサンプル区間まで延長する。停止目標が0m/sの場合は延長距離も0mとする。

#### 関数の呼び出しルール（確定）

- **全体の工程間の呼び出しは`TrainAtcLogic`が行う。** 各子Logicの入口を、処理順に記載する。パターン生成内の順序は`TrainAtcPatternLogic`の入口に記載する。
- 各工程の関数は、自分の処理を完了して呼び出し元へ戻る。次の工程を呼ばない。
- `TrainAtcLogic`以外の、分割したLogicクラス同士は呼び合わない。必要な情報は、準備したデータとして後続工程へ渡す。
- 各工程内の細かい補助処理は、同じクラス内の`private`関数として呼ぶ。同じ子Logic内で共有する処理は、第5節のルールに従って対応するHelperへ分けてもよい。
- `TrainAtcLogic`から呼ぶ、別クラスに置いた各工程の入口は`internal`とする。`TrainAtcLogic`内の補助関数も`private`とし、外部公開が必要なAPIだけ`public`とする。
- 分割先は独立した`static`クラスとし、`partial`で分割しない。
- 生成内の補助関数は必要な失敗を呼び出し元へ返し、補助関数ごとにパターンを消去しない。消去処理は`TrainAtcPatternLogic`の取りまとめ部分の一か所に集める。`TrainAtcLogic`からPatternStateを直接消去しない。
- 生成途中で失敗した場合は、残りの生成工程を実行せず使用不可とする。現在位置のパターン情報を初期値へ戻し、工程7・8は実行する。
- 保持時は生成工程を省略し、前回パターンから現在位置の情報だけを更新する。
- 工程4で確認済みの入力と受信状態は再判定しない。経路の接続や計算結果など、この工程で初めて分かる失敗を確認する。
- この工程ではOutputを読み書きしない。

経路情報・勾配補正・生成途中のサンプルはローカルの作業用データに保持し、計算成功後にPatternStateへ採用する。

### 7. ブレーキ出力を決定

運転モード、パターンの使用可否、現在速度、現在位置のパターン情報から、常用・非常の要求を決定する。常用の段数制御、停止保持、非常保持・解除をこの工程で行う。

ブレーキ出力は毎tick更新する。パターンを保持している場合も照査を継続する。

工程4〜6で使用不可となっても、この工程を省略しない。運転モードと使用不可の結果に応じて非常要求などを決定する。

#### 読み書きする情報（確定）

工程7では、ブレーキの要求と指令値を決め、Stateに保存する。外部向けのOutputへの反映は工程8で行う。表中のフィールド名は既存のContextとの対応を示す。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `signedSpeedMps`：工程4で確認済みの測定速度。速度照査と停止判定に使用する |
| Input | 読む | `deltaTimeSeconds`：工程4で確認済みの経過時間。刻み段の切替待ち時間に使用する |
| Input | 読む | `brakeSettings`：工程4で確認済みのブレーキ設定。設定減速度、刻み段数、常用最大段を使用する |
| Input | 読む | `hasDoorState`・`areAllDoorsClosed`：編成全体の全閉確認。未全閉・取得失敗の間は転動防止を要求する |
| State | 読む | `isAtcEnabled`、操作状態・入力の確認結果、および工程4〜6の使用可否の結果 |
| State | 読む | `cab`：マスコン非常位置など、保持・解除の判定に使う確認済みの操作状態 |
| State | 読む | `State.pattern.isValid`：今回の現在位置でパターンを使用できるか |
| State | 読む | 現在位置の常用・非常・ORP許容速度、防護方式、常用の降下状態 |
| State | 読む | 現在位置の常用パターンに使った勾配補正前の減速度。基準刻み段の選択に使用する |
| State | 読む・更新する | `brake.isNormalBrakeRequired`：常用の介入状態 |
| State | 更新する | `brake.isRollingPreventing`：毎tick更新するドア全閉未確認の転動防止状態 |
| State | 読む・更新する | `brake.targetBrakeStep`・`brake.currentBrakeStep`：常用の目標刻み段と現在刻み段 |
| State | 読む・更新する | `brake.brakeStepTableIndex`：ヒステリシスで保持するテーブルの選択行 |
| State | 読む・更新する | `brake.brakeChangeElapsedSeconds`：刻み段の切替待ち時間 |
| State | 更新する | `brake.isEmergencyBrakeRequired`：今回の非常要求 |
| State | 読む・更新する | `brake.isEmergencyHold`：解除条件を満たすまで保持する非常要求 |
| Settings | 読む | `brakeStepTable`・`normalBrakeReleaseMarginKmh`・`brakeStepChangeIntervalSeconds`：速度偏差テーブル、緩解幅、刻み切替時間 |
| Output | 読み書きしない | 決定したブレーキ指令は工程8で反映する |

#### 基準刻み段とブレーキ要求（確定）

パターンの計算に使った勾配補正前の減速度から、ブレーキ設定の中で最も近い基準刻み段を選ぶ処理も、工程7に含める。その基準段と速度偏差から常用の目標段・現在段を決定する。

常用の介入・緩解、速度偏差テーブルのヒステリシス、刻み段の時間付き切替、停止保持、非常の作動・保持・解除は、既存の[ブレーキパターン・ブレーキ出力仕様](TrainAtcBrakePatternSpecification.md)に従う。この工程の整理を理由に、それらの制御条件を変更しない。

工程4で確認済みの入力の取得可否・基本的な正常性は再判定せず、その確認結果と工程6のパターン使用可否を利用する。使用不可の場合も、常用・非常の状態を今回の結果に応じて更新し、工程8へ渡す。

ドア全閉状態は転動防止の入力とする。開扉操作番号によるORP保持解除とは区別し、全閉未確認の間は常用最大へ即時切替し、全閉確認後は通常の介入・緩解判定へ戻る。取得失敗だけではATC故障や非常の原因を追加しない。確認済みキー切・中立では転動防止も解除し、他の原因による非常保持は既存の優先順位に従う。

### 8. Outputを生成

位置・パターン・ブレーキのStateが確定した後に、外部向けのOutputを生成する。

表示用の速度・接近・ORP・Signalなどと、常用・非常のブレーキ指令をStateから反映する。Outputを作る処理は、位置更新・パターン生成・ブレーキ判定から分離する。

使用不可の場合もこの工程を実行し、今回のブレーキ指令と表示状態を反映する。

#### 読み書きする情報（確定）

既存のOutput項目を維持し、確定したStateから外部向けの値を生成する。Inputを参照する場合も、工程4で確認済みの値を表示用に使用する。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `signedSpeedMps`：常用パターンの接近表示を、現在速度と目標速度から求めるための確認済み測定速度 |
| State | 読む | `isAtcEnabled`：Signalなどの表示状態に使用するATC有効状態 |
| State | 読む | パターンの使用可否、現在位置の距離、常用／非常／ORPの許容速度、目標速度、接近状態。防護方式は`TrainAtcProtectionModeState`から読む |
| State | 読む | `brake.isEmergencyHold`・`brake.currentBrakeStep`：工程7で確定した非常保持と常用の現在刻み段 |
| State | 書き換えない | 位置・パターン・ブレーキの状態は工程7までの確定結果を維持する |
| Output | 更新する | `state.isAtcPowerOn`・`state.isAtcEnabled`・`state.isAtcHealthy`：電源・有効状態・ATC全体の正常性 |
| Output | 更新する | `state.isBrakeReleased`：ATCブレーキ開放。未実装のためfalse |
| Output | 更新する | `pattern.allowSpeedMps`：常用・非常と、Restricted時のORPを比較した許容速度[m/s] |
| Output | 更新する | `pattern.isSpeedIndicated`・`pattern.indicatedSpeedKmh`：速度現示の表示可否と、5km/h刻みで切り下げた現示速度[km/h] |
| Output | 更新する | `pattern.isPatternApproaching`・`pattern.isOrpOperating`・`pattern.signal`：パターン接近、ORP、Signalの表示情報 |
| Output | 更新する | `brake.isEmergencyBrakeRequired`・`brake.isNormalBrakeRequired`・`brake.brakeStep`：非常・常用要求と常用の刻み段 |
| Output | 更新する | `brake.isRollingPreventing`：Stateで確定したドア全閉未確認の転動防止状態 |

#### 生成ルール（確定）

- Stateを書き換えず、位置更新・パターン計算・ブレーキ判定を行わない。
- 非常保持中は`Output.brake.isEmergencyBrakeRequired`をtrueとし、Outputの常用段`brake.brakeStep`を0にする。それ以外は、Stateの現在刻み段を反映する。
- 許容速度は常用・非常の低い方を使用し、Restrictedの場合だけ独立ORPの許容速度も比較する。生成していないORPの初期値を通常時の速度比較へ含めない。
- 速度現示はATCが正常かつ有効で、ORPが作動していない場合に表示する。許容速度をkm/hへ変換し、5km/h刻みで切り下げる。
- 接近表示は常用パターンの接近区間内、かつ現在速度の絶対値が常用目標速度以上の場合に有効とする。
- ORP表示はRestricted、かつ独立ORPパターンの降下区間内の場合に有効とする。降下区間には、第6工程で定めた降下終了後の延長区間も含む。
- ATCが異常または無効の場合はSignalをNoneにする。正常かつ有効の場合は、常用目標速度が0より大きくORP非作動ならGreen、それ以外はRedにする。
- 使用不可時は工程6がパターンを消去した結果を反映し、前回のパターン表示を残さない。ブレーキ指令はパターン表示の使用可否にかかわらず、今回のStateから生成する。
- 入力や受信状態の判定を繰り返さず、工程4〜7で確定した結果を使用する。

#### TIMS転送・表示側との役割分担（確定）

Outputの許容速度`pattern.allowSpeedMps`は計算値をm/sで保持する。モニター用の`pattern.indicatedSpeedKmh`は工程8でkm/hへ変換して丸める。TIMS転送側と表示側は、この表示用の値と表示可否を使用する。

TIMS転送側はパターン使用可否や表示可否で転送項目を間引かず、速度や区間情報とともに`ATC/IsSpeedIndicated`を毎回転送する。速度計UIはこのフラグに従って現示し、`HasValidPattern`や速度タグの削除を表示可否の代わりに使用しない。

ORP表示中は`pattern.isSpeedIndicated`をfalseにし、UI側で速度現示を消灯する。現示速度も転送を続け、Outputの計算上の許容速度は消灯のために0へ置き換えない。

## 3. 処理全体で守る事項（確定）

- 入力の取得失敗や電文の使用不可を理由に、ブレーキ判定とOutput生成を省略しない。
- 入力の取得可否と基本的な正常性は工程4でまとめて確認し、入力不正を無信号の猶予で補わない。
- 新規生成・保持・使用不可の判断を、後続工程へ明示的に渡す。
- 無信号の猶予中に保持するのは、採用済みの経路・防護方式・パターンとする。
- 保持中も現在位置のパターン情報とブレーキ要求を更新する。
- Outputは処理の最後に生成し、各計算工程の入力にはしない。
- 防護方式は`TrainAtcProtectionModeState`だけで管理する。各工程用のStateへ同じ値を重複して保存しない。
- 現在の運転台操作は工程2の操作状態を参照し、PatternStateに採用時の運転台側・レバーサを重複して保存しない。経路の始点方向は、現在Edgeでの照査方向とは別の情報として保持する。

## 4. ContextとStateの分割（確定）

工程ごとにStateのクラスとファイルを分け、それらを全体のStateにまとめる。Contextは共通のInput・State・Output・Settingsと、Graphなどの共通参照を保持する。工程ごとに独立したContextやInput・Outputを作る構成にはしない。

各工程が保持するStateの担当を示す。

| 工程 | State | 保持する状態 |
| --- | --- | --- |
| 2・3の位置更新後 | `TrainAtcOperationState` | 操作状態、ATC有効状態、使用受電器・電文、更新後に確定する照査位置・方向 |
| 3 | `TrainAtcPositionState` | 固定前後の位置と個別の既知フラグ、編成の向き、初期化結果。両端既知のANDは読み取り専用の診断情報 |
| 4 | `TrainAtcValidationState` | 入力・受信状態の判定結果、無信号の積算時間、採用・保持・使用不可の判断、採用候補の経路 |
| 5 | `TrainAtcProtectionModeState` | 使用する防護方式と確定結果、Normal・Restricted保持の有無・保持方式・停止限界・終端方向、処理済み開扉操作番号 |
| 6 | `TrainAtcPatternState` | 採用済みの経路・パターン、現在位置の許容速度・目標速度・接近状態 |
| 7 | `TrainAtcBrakeState` | 常用・非常の要求、目標段・現在段、段変更の経過時間、非常保持 |

工程1のスナップショットは共通のInput、工程8の外部向け結果は共通のOutputに置く。各工程のStateは、全体のStateを通して他工程から参照できるようにする。

防護方式の保持先は`TrainAtcProtectionModeState`に統一する。パターン保持時はここに保持した方式も維持し、方式変更時はパターンを作り直す。`TrainAtcPatternState`には防護方式を保存しない。

工程6の生成途中の経路・勾配補正などの作業用データは、採用済みパターンと分ける。失敗した計算途中の内容を、使用可能なパターンとして残さない。

各工程の表に記載した既存のフィールド名は、データの役割を示す。工程ごとのStateは`TrainAtcState`にまとめ、`TrainAtcContext.State`から参照する。処理ロジックの実装は、以下のルールに従って進める。

## 5. 実装ルール（確定）

### Logicの分割単位と呼び出し

LogicはStateと同じファイル単位で分割する。対応するStateとLogicの名前を揃える。

| State | 子Logic |
| --- | --- |
| `TrainAtcPositionState` | `TrainAtcPositionLogic` |
| `TrainAtcOperationState` | `TrainAtcOperationLogic` |
| `TrainAtcValidationState` | `TrainAtcValidationLogic` |
| `TrainAtcProtectionModeState` | `TrainAtcProtectionModeLogic` |
| `TrainAtcPatternState` | `TrainAtcPatternLogic` |
| `TrainAtcBrakeState` | `TrainAtcBrakeLogic` |

工程8のOutput生成は`TrainAtcOutputLogic`で扱う。

- 子Logicの呼び出しは`TrainAtcLogic`に集約する。子Logic同士で関数を呼び合わない。
- 基本的に、各子Logicに入口となるトップレベルの関数を設ける。その関数に大まかな処理の流れを上から順に記載し、詳細処理は補助関数へ分ける。
- 全体の工程順は`TrainAtcLogic`、各工程内の処理順は対応する子Logicのトップレベル関数に置く。
- Logicは独立した`static class`とし、`partial`で分割しない。
- `TrainAtcLogic`から呼ぶ子Logicの入口は`internal`、同じクラス内の補助関数は`private`にする。`TrainAtcLogic`は外部公開が必要な関数だけを`public`にする。

### State・Input・Outputの更新担当

各子Logicが書き換えてよいStateは、自分のファイル名に対応するStateだけとする。他のStateは参照できるが、直接変更しない。他のStateが保持する配列・オブジェクトの参照を通した変更も行わない。

| Logic | 書き換えてよい情報 |
| --- | --- |
| `TrainAtcLogic` | `TrainAtcState`直下の全体状態。子Stateを直接変更せず、担当する子Logicの入口を呼ぶ |
| `TrainAtcPositionLogic` | `TrainAtcPositionState` |
| `TrainAtcOperationLogic` | `TrainAtcOperationState` |
| `TrainAtcValidationLogic` | `TrainAtcValidationState` |
| `TrainAtcProtectionModeLogic` | `TrainAtcProtectionModeState` |
| `TrainAtcPatternLogic` | `TrainAtcPatternState`と、その中の個々のパターン・サンプル |
| `TrainAtcBrakeLogic` | `TrainAtcBrakeState` |
| `TrainAtcOutputLogic` | Outputのみ。Stateは書き換えない |

`isAtcHealthy`は`TrainAtcState`直下に置き、`TrainAtcLogic`が各工程の結果を集約して更新する。ValidationStateには工程4の入力・受信判定と採用・保持・使用不可の結果だけを保持する。工程5・6の失敗を理由に、別の子LogicからValidationStateの結果やProtectionModeStateの確定結果を書き換えない。

各工程の失敗は、担当するStateの結果、または戻り値として`TrainAtcLogic`へ渡す。全体の正常性の集約は入力確認の再実行とはせず、各工程で確定した結果だけを使用する。確認できたキー切・レバーサ中立だけを理由に故障扱いにしない。

Inputは工程1のスナップショット時に取り込み、LogicとHelperからは変更しない。Outputは工程8の`TrainAtcOutputLogic`だけが生成する。

### 文法と改行

`if`・`else`・`for`・`foreach`・`while`などのブロックは、処理が一つだけでも`{}`と改行を省略しない。条件と処理を一行に詰め込まず、既存コードと同じように開始・終了の波括弧を別行に置く。

### Helperの分割と制限

同じ子Logic内の複数の関数で共通の処理を使う場合は、`Assets/Nakatetsu/Train/Equipment/Atc/Scripts/Helper/`に対応するHelperを作ってよい。Helperへの分割は必須ではない。

- 名前は対応するLogicの`Logic`を`Helper`へ置き換える。例えば、`TrainAtcPositionLogic`に対応するファイルは`TrainAtcPositionHelper.cs`とする。
- Helper内の関数はすべて`static`にする。
- Helperの引数にContextを渡さない。必要な配列・変数だけを明示的に渡す。
- Helperは原則としてState・Input・Outputを直接変更せず、引数から計算した結果を戻り値または`out`引数で返す。
- 子Logicは自分に対応するHelperだけを使用する。別の子Logicに対応するHelperを呼ぶことは禁止する。
- 子Logicから呼ぶHelperの関数は`internal`とし、Helper内だけで使う補助関数は`private`にする。

例えば、`TrainAtcPositionLogic`から`TrainAtcPositionHelper`は呼べるが、`TrainAtcPatternLogic`から`TrainAtcPositionHelper`は呼べない。

## 6. 詳細仕様との関係

入力と既存の受け渡しは[車上ATCの入力](TrainAtcInput.md)、制動計算とブレーキ制御の詳細は[ブレーキパターン・ブレーキ出力仕様](TrainAtcBrakePatternSpecification.md)を参照する。

本書は正式なAtcの全体処理順、工程ごとのStateとLogicの分割、および実装ルールを定める。旧実装の詳細仕様・調査記録を読む場合も、現在の型・メンバー・受け渡しは本書と現行コードを参照する。

## 7. Atcの実装範囲

関数ごとの処理順と分岐は、[ATCの状態遷移図](TrainAtcTransitions.drawio)を参照する。Logicファイルごとにページを分け、共通補助関数も同じページにまとめる。旧実装の図と作業時のバックアップは、[Archive](Archive/)へ保存する。

工程1〜8の実装は`Assets/Nakatetsu/Train/Equipment/Atc/`へ統一する。旧実装は削除し、NewAtcとして作成した処理を正式なAtcとして使用する。OutputをTIMSの表示とブレーキ指令へ接続し、Prefab・Sceneの参照も正式な実装へ揃える。

| 工程 | 実装の入口 | 更新先 |
| --- | --- | --- |
| 1 | `TrainAtcController.CollectInput` | Input。`Calculate`で今回の経過時間を設定する |
| 2 | `TrainAtcOperationLogic.UpdateOperation` | `State.operation`。有効状態・使用受電器・電文を選び、前回照査位置を消去する |
| 3 | `TrainAtcPositionLogic.UpdatePosition` | `State.position`。基準端を更新し、他端を相対再解決する |
| 3の位置更新後 | `TrainAtcOperationLogic.UpdateCurrentPosition` | `State.operation`。既知の選択端の最新位置・照査方向を確定する |
| 4 | `TrainAtcValidationLogic.UpdateValidation` | `State.validation` |
| 5 | `TrainAtcProtectionModeLogic.UpdateProtectionMode` | `State.protectionMode` |
| 6 | `TrainAtcPatternLogic.UpdatePattern` | `State.pattern` |
| 7 | `TrainAtcBrakeLogic.UpdateBrakeState` | `State.brake` |
| 8 | `TrainAtcOutputLogic.UpdateAtcOutput` | Output |

工程2〜8の呼び出し順は`TrainAtcLogic.Calculate`に集約する。前回の使用受電器を保存し、`UpdateOperation` → `UpdatePosition` → `UpdateCurrentPosition` → `UpdateValidation`の順に呼ぶ。工程5の後に`TrainAtcValidationLogic.ValidateProtectionSettings`を呼び、工程6までの結果から全体の`State.isAtcHealthy`を決め、失敗時にも工程7・8を実行する。

- 受電器は旧ATCと同じく、有効運転台側を選ぶ。照査方向は運転台側・レバーサ・受電器位置の編成の向きから決める。
- 入力・共通設定の基本値は工程4で確認する。ORP固有の設定やNoneの停止余裕は、工程5で確定した使用方式を基準に`ValidateProtectionSettings`で確認する。
- 新しい経路を採用した場合は、旧ATCと同じく毎回パターンを生成する。無信号中の保持ではサンプルを生成し直さず、現在位置の値を更新する。
- 現在位置の勾配補正前減速度は、各パターンの`currentDecelerationMps2`に保持し、工程7の基準刻み段の選択に使う。
- 生成途中の経路・勾配補正・サンプルはローカルの作業データに置き、計算が成功してから採用する。サンプルは1パターンあたり最大1,000,000点とし、上限を超える計算は使用不可とする。

Controllerは受電器・速度センサー・InputSourceを参照する。InputSourceと位置型は正式なAtcの型へ統一し、旧InputSourceや旧位置型を変換する互換処理は残さない。ゲーム開始時の初期化と、明示的な位置補正のAPIを保持する。

`TrainSimulationController`はAtcを機器として収集し、両端受電器・速度センサーを接続する。ゲーム開始時に一度だけTrackSampleから取得した位置を渡す。編成内のATCはCommonに配置した一つを使用する。

Settingsの定義・AssetはAtcに置き、従来の設定項目・初期値・コピー機能を維持する。`Atc/Data/TrainAtcSettings.asset`は設定済みの値を引き継ぐ。線区最高速度は初期化時にGraphから取得し、Settings Assetの値では上書きしない。

旧実装の調査記録と詳細仕様は履歴として保持する。本節と現在の状態遷移図を、正式な実装の参照先とする。移行後のUnityでの実走は未検証であり、電文の境界通過・パターン表示・常用介入・非常保持と解除の確認が必要となる。
