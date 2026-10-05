# 車上ATC 全体処理仕様

本書は、車上ATCの処理順と各工程の役割を定める。関数の呼び出し構造を整理する際は、この処理順を基準とする。Stateは工程ごとに分け、全体のContextにまとめる。現在の実装の入口と参照構成は、第7節に記載する。

## 1. 全体の処理順（確定）

制御は1〜7の順に行い、最後に8で外部向けのOutputを生成する。

| 順番 | 工程 | 役割 |
| --- | --- | --- |
| 1 | 入力をスナップショット | 操作状態・測定速度・両端の受電結果などを、今回の更新で使用する入力として取り込む |
| 2 | 列車位置を更新 | 前回の位置と今回の測定値から、両端の受電器位置を更新する |
| 3 | 有効状態・使用受電器を決定 | マスコンキー・運転台方向・レバーサから、ATCの有効状態と使用受電器を決める |
| 4 | 入力・受信状態を判定 | 入力の取得可否と基本的な正常性、使用受電器の電文を確認し、採用・保持・使用不可を判定する |
| 5 | 防護方式を確定 | 採用する経路情報・制限情報から、地上側が指定したNormal・Restricted・Noneを取得する |
| 6 | パターンを更新 | 防護方式に応じて常用・非常・ORPパターンを生成し、現在位置の許容速度・目標速度・接近状態を求める |
| 7 | ブレーキ出力を決定 | パターンを照査し、ATC常用・非常の要求、停止保持、非常保持・解除を決める |
| 8 | Outputを生成 | 確定したStateから、TIMS向けの表示情報とブレーキ指令を生成する |

## 2. 各工程の扱い

### 1. 入力をスナップショット

今回の更新で使用する操作状態・測定速度・両端の受電結果・計算用入力を取り込む。取得できたかどうかも保持し、取得失敗による初期値を正常な操作状態として扱わない。

両端の受電結果を取り込み、使用する受電器の選択は工程3で行う。無信号時に、受信電文を前回の電文で置き換えない。

### 2. 列車位置を更新

前回位置と今回の測定値から、両端の受電器位置を更新する。位置更新は、工程3で使用受電器を決める前に行う。

#### 読み書きする情報（確定）

前回採用した経路は`State.pattern.atcEdgePath`と`pathStartTravelDirection`に保持する。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `hasSpeedMeasurement`：測定速度を取得できたか |
| Input | 読む | `signedSpeedMps`：編成の固定前方向を正とする測定速度[m/s] |
| Input | 読む | `deltaTimeSeconds`：今回の移動量を求める経過時間[s] |
| Input | 読む | `frontTelegram`・`rearTelegram`：両端の受信電文。Edge境界で移動先を特定するための経路情報を参照する |
| State | 読む | `isPositionInitialized`：初期位置と編成の向きが設定済みか |
| State | 読む | `frontPosition`・`rearPosition`：両端の前回のEdge ID・Edge内位置・編成の向き |
| State | 読む | `State.pattern.atcEdgePath`・`pathStartTravelDirection`：前回採用した経路と始点方向 |
| State | 読む・更新する | `isPositionKnown`：位置を把握できているか。位置更新・解決に失敗した場合は、その結果を後続工程へ渡す |
| State | 更新する | `frontPosition`・`rearPosition`：今回のEdge ID・Edge内位置・編成の向き |
| その他の参照 | 読む | `Graph`・`atcEdgesById`：Edgeの長さ、接続、定義済み経路を取得する |
| Output | 読み書きしない | 外部向けの表示情報・ブレーキ指令は工程8で生成する |

前側・後側は編成の固定前側・固定後側を意味する。運転台変更によって、工程2で更新する両端の位置を入れ替えない。

#### 移動量と方向

今回の移動量は、次の式で求める。

```text
符号付き移動距離[m] = signedSpeedMps[m/s] × deltaTimeSeconds[s]
```

移動方向は実際の測定速度の符号から求める。各受電器位置に保持した`frontFacesAtoB`を使い、編成の固定前後方向を現在EdgeのA→B・B→Aへ変換する。Edgeを越えた場合は、新しいEdgeに対する編成の向きも更新する。

レバーサから決める照査方向と、照査に使用する`currentPosition`の選択は工程3で行う。

#### 経路の参照と工程4との役割分担

工程2では、分岐先を特定するために両端の受信電文内の経路を参照する。無信号時は前回採用した経路を使用し、Graphの接続や定義済み経路から移動先が一意に特定できる場合も位置更新を行う。進路の開通状態だけを理由に、一意に特定できる移動先の解決を省略しない。

この工程では、経路の接続など、位置解決に必要な確認を行う。無信号時間の積算・猶予の判定と、電文や前回パターンを今回の制御に使用できるかの判定は工程4で行う。

位置を更新・解決できなかった場合は、その結果を後続工程へ渡す。

### 3. 有効状態・使用受電器を決定

今回のマスコンキー・運転台方向・レバーサから、ATCの有効状態、使用する受電器、照査に使用する位置と進行方向を決定する。有効状態は`isAtcPowerOn`と`isAtcEnabled`で表す。

#### ATCの有効条件（確定）

| 項目 | 条件・動作 |
| --- | --- |
| `isAtcPowerOn` | どのような状態でも常にtrue。キー切・レバーサ中立・操作状態の取得失敗によってfalseにしない |
| `isAtcEnabled` | 操作状態を確認でき、マスコンキーが投入され、レバーサが前進または後退の場合にtrueとする |
| キー切・レバーサ中立 | `isAtcEnabled`をfalseとし、速度照査を行わない |
| 操作状態の取得失敗 | 正常なキー切・中立と区別し、取得失敗の結果を後続工程へ渡す |
| 両端の位置更新 | `isAtcEnabled`に関係なく工程2で継続する |

この有効条件と、工程5で決定するNormal・Restricted・Noneの防護方式は別の情報として扱う。

#### 読み書きする情報

以下の状態は`State.operation`に保持する。使用受電器は`selectedReceiver`で識別する。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `hasCabState`：操作状態を取得できたか |
| Input | 読む | `cab.carIndex`・`cab.isFrontCab`：有効運転台の号車と編成の前後側 |
| Input | 読む | `cab.isKeyInserted`・`cab.reverserPosition`：マスコンキーの投入状態とレバーサ位置 |
| Input | 読む | `frontTelegram`・`rearTelegram`：使用受電器側の電文を選択するための両端の受信結果 |
| State | 読む | `isPositionInitialized`・`isPositionKnown`：照査に使用する位置を選択できるか |
| State | 読む | `frontPosition`・`rearPosition`：工程2で更新した両端の位置と編成の向き |
| State | 更新する | `hasCabState`・`cab`：確認した操作状態と、その確認結果 |
| State | 更新する | `isAtcPowerOn`・`isAtcEnabled`：今回のATCの電源状態と有効状態 |
| State | 更新する | `selectedReceiver`：使用受電器の選択結果 |
| State | 更新する | `hasCurrentPosition`・`currentPosition`：照査に使用する位置と、その位置を選択できたか |
| State | 更新する | `currentTravelDirection`：運転台側・レバーサ・編成の向きから求める照査方向 |
| State | 更新する | `currentTelegram`：選択した受電器側の受信電文。無信号時はnullとして工程4へ渡す |
| Output | 読み書きしない | 外部向けの表示情報・ブレーキ指令は工程8で生成する |

照査方向は運転台側とレバーサから編成の前後方向を求め、選択位置の`frontFacesAtoB`でEdge上のA→B・B→Aへ変換する。工程2で移動方向に使う測定速度の符号とは役割を分ける。

#### キー切・レバーサ中立時の保持・消去（確定）

キー切・レバーサ中立でも、工程2で両端の受電器位置を保持し、列車の移動に応じて更新を続ける。照査用の受電器選択は解除し、工程3では`currentPosition`・`currentTelegram`を残さない。

| State | キー切・レバーサ中立時の扱い |
| --- | --- |
| `frontPosition`・`rearPosition` | 保持し、工程2で更新を続ける |
| `isPositionKnown` | キー切・レバーサ中立そのものを理由にfalseにしない。工程2の位置更新結果を維持する |
| 使用受電器の選択結果 | 未選択にする |
| `hasCurrentPosition` | falseにする |
| `currentPosition` | 初期値へ戻す |
| `currentTelegram` | nullにする |
| `currentTravelDirection` | 照査位置の選択解除に合わせてUnspecifiedにする |

Inputの`frontTelegram`・`rearTelegram`は、工程2の位置解決に使う両端の受信スナップショットとして扱う。キー切・レバーサ中立時に工程3の選択結果を消す処理とは分ける。

無信号時間と、電文や前回パターンを今回の制御に使用できるかは工程4で判定する。使用受電器は有効運転台側を選ぶ。

### 4. 入力・受信状態を判定

入力の取得可否と基本的な正常性をまとめて確認し、工程3で選択した受電器の電文を確認する。無信号時間、電文の使用可否、前回パターンの保持可否から、今回の処理を決定する。

#### 読み書きする情報

入力確認・受信判定の結果、採用候補の経路情報、確認済みの計算用情報を`State.validation`へ保持する。採用・保持・使用不可は`TrainAtcValidationResult`で表す。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 読む | `deltaTimeSeconds`：無信号時間の積算に使う経過時間[s] |
| Input | 読む | `hasSpeedMeasurement`・`signedSpeedMps`：測定速度の取得可否と値 |
| Input | 読む | `hasCarMasses`・`cars`：各車両の質量と中心位置の取得可否、および計算用の値 |
| Input | 読む | `frontReceiverDistanceFromFrontM`・`rearReceiverDistanceFromFrontM`：受電器の取り付け位置。使用する側の計算用位置を確認する |
| Input | 読む | `hasBrakeSettings`・`brakeSettings`：ブレーキ設定の取得可否、刻み段数・常用最大段・設定減速度 |
| State | 読む | `hasCabState`・`cab`・`isAtcEnabled`：工程3で確認した操作状態と有効状態 |
| State | 読む | `isPositionInitialized`・`isPositionKnown`・`hasCurrentPosition`：工程2・3の位置確認結果 |
| State | 読む | `currentPosition`・`currentTravelDirection`：今回の電文に対応する経路情報を取得するための位置と照査方向 |
| State | 読む | `currentTelegram`：工程3で選択した受電器側の電文 |
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

使用受電器を切り替えた場合は、取り付け位置を基準にした勾配補正の条件が変わるため、旧パターンを保持しない。新しい電文があれば生成し直す。受電器の変更は工程3の前後の選択結果を`TrainAtcLogic`で比較し、工程4へ引数で渡す。PatternStateへ運転台側などの写しを追加しない。

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

工程5・6では、経路の接続や計算結果など、その工程で初めて分かる失敗を確認する。工程4で確認済みの入力の取得可否・基本的な正常性と、無信号時間・猶予の判定は繰り返さない。後続工程で初めて判明した失敗も、使用不可の結果としてブレーキ判定とOutput生成へ渡す。

電文には軌道回路IDがないため、現在Edgeの軌道回路IDが定義されていることを確認し、現在Edge IDと照査方向のキーで経路を読む。入力の許容範囲はValidationLogicと、そのHelperで確認する。

### 5. 防護方式を確定

新しい情報を採用する場合は、受電器から取得した経路情報・制限情報に含まれる地上側の防護方式を採用する。

- `Normal`
- `Restricted`
- `None`

車上側が独自にNormal・Restricted・Noneを選び直す処理とはしない。前回パターンを保持する場合は、前回採用した防護方式を維持する。方式や経路を採用できない場合は、使用不可の結果を後続工程へ渡す。

#### 読み書きする情報（確定）

工程5で決める防護方式は`TrainAtcProtectionModeState.overrunProtectionMode`だけで管理する。工程6〜8もこの値を参照し、PatternStateなどに同じ方式を重複して保存しない。`isProtectionModeKnown`で方式を確定できたかを表す。

| 区分 | 読み書き | 必要な情報・更新結果 |
| --- | --- | --- |
| Input | 直接読まない | 工程4で確認した情報を使用する |
| State | 読む | 工程4の採用・保持・使用不可の判定結果 |
| State | 読む | 採用候補の経路情報。新しく採用する防護方式を取得する |
| State | 読む・維持する | `TrainAtcProtectionModeState.overrunProtectionMode`：前回パターンを保持する場合は、ここに保持した方式を維持する |
| State | 更新する | `TrainAtcProtectionModeState.overrunProtectionMode`：新しい情報を採用する場合の防護方式 |
| State | 更新する | `isProtectionModeKnown`：方式の確定結果。取得失敗や使用不可の場合はfalseにする |
| Output | 読み書きしない | 外部向けの表示情報・ブレーキ指令は工程8で生成する |

#### 採用・保持・使用不可の扱い（確定）

| 工程4の判定結果 | 工程5の処理 |
| --- | --- |
| 新しい情報を採用 | 採用候補の経路情報から方式を取得し、`TrainAtcProtectionModeState`へ保存する。方式を変更する場合は必ずパターンも作り直す |
| 前回パターンを保持 | `TrainAtcProtectionModeState`の方式と確定結果を維持する。方式が未確定の場合は保持できない |
| 使用不可 | `isProtectionModeKnown`をfalseにし、防護方式を使ったパターン生成へ進まず、使用不可の結果を後続工程へ渡す |

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

ドア状態は転動防止専用の入力とする。全閉未確認の間は常用最大へ即時切替し、全閉確認後は通常の介入・緩解判定へ戻る。取得失敗だけではATC故障や非常の原因を追加しない。確認済みキー切・中立では転動防止も解除し、他の原因による非常保持は既存の優先順位に従う。

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
- 現在の運転台操作は工程3の操作状態を参照し、PatternStateに採用時の運転台側・レバーサを重複して保存しない。経路の始点方向は、現在Edgeでの照査方向とは別の情報として保持する。

## 4. ContextとStateの分割（確定）

工程ごとにStateのクラスとファイルを分け、それらを全体のStateにまとめる。Contextは共通のInput・State・Output・Settingsと、Graphなどの共通参照を保持する。工程ごとに独立したContextやInput・Outputを作る構成にはしない。

各工程が保持するStateの担当を示す。

| 工程 | State | 保持する状態 |
| --- | --- | --- |
| 2 | `TrainAtcPositionState` | 両端の受電器位置、編成の向き、位置の初期化・更新結果 |
| 3 | `TrainAtcOperationState` | 操作状態の確認結果、ATC有効状態、使用受電器、選択した位置・進行方向・電文 |
| 4 | `TrainAtcValidationState` | 入力・受信状態の判定結果、無信号の積算時間、採用・保持・使用不可の判断、採用候補の経路 |
| 5 | `TrainAtcProtectionModeState` | 使用する防護方式と、その確定結果。方式の保持先はこのStateだけとする |
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
| 2 | `TrainAtcPositionLogic.UpdatePosition` | `State.position` |
| 3 | `TrainAtcOperationLogic.UpdateOperation` | `State.operation` |
| 4 | `TrainAtcValidationLogic.UpdateValidation` | `State.validation` |
| 5 | `TrainAtcProtectionModeLogic.UpdateProtectionMode` | `State.protectionMode` |
| 6 | `TrainAtcPatternLogic.UpdatePattern` | `State.pattern` |
| 7 | `TrainAtcBrakeLogic.UpdateBrakeState` | `State.brake` |
| 8 | `TrainAtcOutputLogic.UpdateAtcOutput` | Output |

工程2〜8の呼び出し順は`TrainAtcLogic.Calculate`に集約する。工程6までの結果から全体の`State.isAtcHealthy`を決め、失敗時にも工程7・8を実行する。

- 受電器は旧ATCと同じく、有効運転台側を選ぶ。照査方向は運転台側・レバーサ・受電器位置の編成の向きから決める。
- 入力・共通設定の基本値は工程4で確認する。ORP固有の設定やNoneの停止余裕は、その方式を使う場合だけ確認する。
- 新しい経路を採用した場合は、旧ATCと同じく毎回パターンを生成する。無信号中の保持ではサンプルを生成し直さず、現在位置の値を更新する。
- 現在位置の勾配補正前減速度は、各パターンの`currentDecelerationMps2`に保持し、工程7の基準刻み段の選択に使う。
- 生成途中の経路・勾配補正・サンプルはローカルの作業データに置き、計算が成功してから採用する。サンプルは1パターンあたり最大1,000,000点とし、上限を超える計算は使用不可とする。

Controllerは受電器・速度センサー・InputSourceを参照する。InputSourceと位置型は正式なAtcの型へ統一し、旧InputSourceや旧位置型を変換する互換処理は残さない。ゲーム開始時の初期化と、明示的な位置補正のAPIを保持する。

`TrainSimulationController`はAtcを機器として収集し、両端受電器・速度センサーを接続する。ゲーム開始時に一度だけTrackSampleから取得した位置を渡す。編成内のATCはCommonに配置した一つを使用する。

Settingsの定義・AssetはAtcに置き、従来の設定項目・初期値・コピー機能を維持する。`Atc/Data/TrainAtcSettings.asset`は設定済みの値を引き継ぐ。線区最高速度は初期化時にGraphから取得し、Settings Assetの値では上書きしない。

旧実装の調査記録と詳細仕様は履歴として保持する。本節と現在の状態遷移図を、正式な実装の参照先とする。移行後のUnityでの実走は未検証であり、電文の境界通過・パターン表示・常用介入・非常保持と解除の確認が必要となる。
