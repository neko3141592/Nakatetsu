# 車上ATC ブレーキパターン・ブレーキ出力仕様

本書を車上ATCのブレーキパターン計算とブレーキ出力に関する実装用仕様とする。確定事項を記録し、未決事項と区別する。正式な実装は`Assets/Nakatetsu/Train/Equipment/Atc/`に統一する。常用・非常・独立ORPの生成と現在位置の更新、常用のヒステリシスと刻み切替、非常速度超過の照査と保持・解除、TIMSへの接続を実装済みとする。全体の処理順・Stateの更新担当・最新の表示条件は[全体処理仕様](TrainAtcProcessingFlow.md)、入力とTIMSのタグは[車上ATCの入力・状態・出力](TrainAtcInput.md)を参照する。移行後のUnityでの実走は未検証。

## 1. 対象

`TrainAtcLogic`から呼ぶ`TrainAtcPatternLogic.UpdatePattern()`で生成する、次のパターンを対象とする。

- ATC常用ブレーキパターン
- ATC非常ブレーキパターン
- Restricted時の独立ORPパターン

地上電文の進行可能なATC Edge列と停止限界を使い、車上がATC Graphから距離・常設速度制限・勾配を取得して計算する。地上から距離や計算済みの速度パターンを受け取らない。

経路内のEdge IDに対応するEdgeは、初期化時に作成した`context.atcEdgesById`から取得する。辞書の詳細は[車上ATCの入力](TrainAtcInput.md)に記載する。

パターンの生成と、現在位置での速度照査・ブレーキ出力は分ける。ブレーキ出力の確定事項は第6節に記録する。

計算設定は`TrainAtcSettingsAsset`に保存し、Controllerから`context.Settings`へコピーする。最大サンプル間隔、常用・非常の計算用減速度と停止余裕距離、常用ブレーキの速度偏差テーブル、線区の最大下り勾配、ORPの制限速度と最低余裕距離、無信号の許容時間を保持する。計算用減速度の暫定初期値は常用0.5m/s²、非常1.2m/s²とする。無信号の許容時間`noSignalTimeoutSeconds`の初期値は1秒、0は猶予なしとする。初期値と受け渡しの詳細は[車上ATCの入力](TrainAtcInput.md)に記載する。

線区最高速度は生成元の`TrackAtcGraphCompileDefinition.maximumOperatingSpeedKmh`へ設定し、生成済みGraphへ引き継ぐ。車上初期化時に`context.Settings.maximumOperatingSpeedKmh`へ取り込み、パターン初期化では常用をこの値、非常をこの値にLogicの共通定数`EmergencySpeedMarginKmh`を加えた値で初期化する。保持単位はm/sとする。回路ごとの基本速度制限と区間制限は、生成済みGraphの`speedLimitSections`から参照し、線区最高速度より低い値を優先する。

## 2. 経路と距離の基準（確定）

電文の進行順に並んだATC Edge列を、1次元の経路として扱う。

- 最初のEdgeの進行方向に対する進入端を0mとする。
- 各Edgeの全長を合計し、最後のEdgeの進行方向に対する退出端までを`pathLengthM`とする。
- 地上電文の停止限界は、この経路の終端に対応する。
- 現在の受信機位置は、経路上の距離に換算してパターンを参照する。受信機位置を経路の原点にはしない。
- Edgeをまたぐ停止余裕も、経路に沿った距離として扱う。

Edge内距離はNode Aからの距離で保持するため、経路上の距離へ変換するときは進行方向を反映する。

| Edgeの進行方向 | そのEdgeの進入端からの距離 |
| --- | --- |
| A→B | Node AからのEdge内距離 |
| B→A | Edge長 − Node AからのEdge内距離 |

先頭Edgeの方向は電文のキーで区別し、以降のEdgeの方向は順序付きEdge列の接続から求める。

現在Edgeでどちらの方向の電文を使用するかは、`State.operation.currentTravelDirection`で選ぶ。この方向は、有効運転台とレバーサから編成の固定前後での指定方向を求め、`currentPosition.frontFacesAtoB`を使って現在EdgeのA→B・B→Aに変換する。停止中も同じ方法で決める。中立、または運転台・現在位置を選択できない場合は`Unspecified`とする。レバーサ中立ではATCを無効とし、直前の進行方向を使って照査を継続しない。

この指定方向と、位置積算に用いる速度センサーの符号による実際の移動方向は別に扱う。

### 経路情報と距離の計算（実装済み）

経路の準備は`TrainAtcPatternLogic.PreparePath()`から`TrainAtcPatternHelper.TryPreparePath()`を呼ぶ。Edge辞書・順序付きEdge ID列・始点の進行方向を渡し、各Edgeの方向と経路始点からの累積距離を持つ`TrainAtcPatternPathEdge`の列を作る。生成時は経路内の速度制限と勾配プロファイルも確認する。

先頭Edgeの方向には採用時の`State.operation.currentTravelDirection`を使う。以降のEdgeの方向は、直前のEdgeの出口Nodeとの接続から求める。保持時は保存済みの`State.pattern.pathStartTravelDirection`を使い、現在Edgeが変わっても経路の原点と方向を変えない。

`TrainAtcPatternHelper.TryGetPathDistance(path, atcEdgeId, distanceOnAtcEdgeM, out distanceOnPathM, out direction)`は、準備済み経路とEdge内位置から経路内距離・そのEdgeの方向を返す。指定Edgeより前のEdge長の合計に、進入端からの距離を加える。A→BではNode Aからの距離、B→AではEdge長からNode A基準の距離を引いた値を使う。前のEdgeの退出端と次のEdgeの進入端は同じ経路内距離になる。

経路は同じEdge IDを重複して含めない。欠損したEdge、不正な長さ、接続から方向を決められない経路は準備に失敗する。距離変換では指定Edgeが経路にあることとEdge内位置を確認し、失敗時はfalseを返す。ContextやStateをHelperから書き換えない。

勾配は経路内の`gradientProfiles`から線形補間し、B→Aでは符号を反転する。0mとEdge全長の両端を含む距離順のデータを使用し、欠損した勾配を0‰として補わない。常設速度制限は`speedLimitSections`から読み、共有境界や重複区間では低い方を採用する。

これらの処理はパターン生成と現在位置の更新の内部計算とし、地点情報や経路距離を`TrainAtcLogic`の公開APIとして提供しない。制御用の現在距離は`State.pattern.distanceOnPathM`に保持する。

## 3. サンプリングと保持情報（確定）

### サンプリング

最大サンプル間隔は`context.Settings.maximumSamplingIntervalM`から取得し、初期値を5mとする。経路の始点・終端の両方にサンプルが一致するよう等間隔で分割する。

経路長を`L`、分割数を`N`、実際のサンプル間隔を`Δx`とする。正の経路長に対して、次の式で求める。

```text
N = ceil(L / 5m)
サンプル数 = N + 1
Δx = L / N
各サンプルの位置 = i × Δx  （i = 0 ... N）
```

例えば経路長が12mの場合は、0m・4m・8m・12mの4点とする。`samplingIntervalM`には最大値の5mではなく、実際の間隔を保持する。

### パターンの保持情報

以下の項目は`State.pattern`を基準とする。個別パターンの現在値は、その`normalPattern`・`emergencyPattern`・`orpPattern`に保持する。

| 項目 | 内容 |
| --- | --- |
| `pathLengthM` | 最初のEdgeの進入端から最後のEdgeの退出端までの経路長[m] |
| `samplingIntervalM` | 実際のサンプル間隔[m] |
| `isValid` | 今回の現在位置でパターンを使用できるか。ブレーキ判定とOutput生成で共有 |
| `distanceOnPathM` | 現在位置のpath内距離[m] |
| `normalPattern.currentAllowSpeedMps` | 現在位置で補間した常用許容速度[m/s]。float |
| `emergencyPattern.currentAllowSpeedMps` | 現在位置で補間した非常許容速度[m/s]。float |
| `orpPattern.currentAllowSpeedMps` | 現在位置で補間した独立ORP許容速度[m/s]。float。ORPがない場合は0 |
| `normalPattern.currentTargetSpeedMps` | 現在位置で補間した常用の目標速度[m/s] |
| `emergencyPattern.currentTargetSpeedMps` | 現在位置で補間した非常の目標速度[m/s] |
| `orpPattern.currentTargetSpeedMps` | 現在位置で補間した独立ORPの目標速度[m/s]。ORPがない場合は0 |
| `normalPattern.isDecelerationSection` | 現在位置が常用パターンの減速区間か。bool |
| `emergencyPattern.isDecelerationSection` | 現在位置が非常パターンの減速区間か。bool |
| `normalPattern.isApproachSection` / `emergencyPattern.isApproachSection` | 現在位置が常用・非常それぞれの予告・降下区間内か |
| `orpPattern.isApproachSection` | 現在位置が独立ORPの予告・降下区間内か。ORPがない場合はfalse |
| `atcEdgePath` | 進行順に並んだATC Edge IDの列。IDは文字列 |
| `pathStartTravelDirection` | 採用時の先頭Edgeの進行方向。現在Edgeが変わっても保持 |
| `normalPattern` | 各サンプル位置の常用パターン情報 |
| `emergencyPattern` | 各サンプル位置の非常パターン情報 |
| `orpPattern` | Restricted時に生成する独立ORPパターン。同じ経路とサンプル間隔を使用。通常非常と別の配列で保持 |

`State.pattern`は宣言時に生成し、`atcEdgePath`は`List<string>`として保持する。経路は電文内のリストを参照せず、今回のEdge ID列をコピーする。 防護方式は`State.protectionMode`だけで保持する。現在の運転台・レバーサは`State.operation.cab`を参照し、PatternStateへ重複保存しない。

現在位置の許容速度は`TrainAtcPatternLogic.UpdateCurrentPattern()`で前後サンプルの速度の二乗を補間して、個別パターンの`currentAllowSpeedMps`へ保持する。常用・非常・独立ORPの目標速度は`currentTargetSpeedMps`へ同じ方法で補間する。接近は前後サンプルのいずれか、降下と減速度は現在位置を含むサンプル区間から取得する。参照失敗時と`ClearPattern()`では使用可否・距離・現在値・区間フラグを初期値へ戻す。

現在位置の減速区間は、現在位置を含む区間の始点サンプルの`isDecelerationSection`を各パターンから取得する。サンプル境界ではその地点から先の区間を使う。降下終了後の延長区間も同じフラグで保持する。パターン消去時は全パターンの区間フラグをfalseへ戻す。

常用・非常・独立ORPは、それぞれ`TrainAtcPattern`の`samples`に`List<TrainAtcPatternSample>`として保持する。各サンプルの情報は次の通りとする。

| 項目 | 内容 |
| --- | --- |
| `allowSpeedMps` | その地点の積分後の許容速度[m/s] |
| `targetSpeedMps` | 減速先の目標速度[m/s]。予告区間でも引き継ぐ |
| `decelerationMps2` | 勾配補正前の計算用減速度[m/s²]。常用・非常それぞれの設定値 |
| `isDecelerationSection` | 減速区間と、下がり切った速度で1秒進む距離の延長区間 |
| `isApproachSection` | 減速区間、設定時間分だけ手前の予告区間、降下終了後の延長区間 |

初期化時は目標速度を初期上限速度と同じ値にし、両方の区間フラグをfalseにする。勾配補正した有効減速度は積分だけに使い、サンプルに保存する減速度には加算しない。TIMSへ公開する既存の速度配列は、サンプルの`allowSpeedMps`だけを取り出す。

### 実装済みの初期化

`PreparePath()`で経路長を求め、`InitializePatterns()`でサンプル間隔を決める。常用は`Settings.MaximumOperatingSpeedMps`、非常とRestricted時の独立ORPは線区最高速度に`EmergencySpeedMarginKmh`を加え、m/sへ換算した値で初期化する。ORP以外では独立ORPのサンプルを生成しない。

Edge ID・長さ・接続・プロファイルは`PreparePath()`、分割数と計算値は`InitializePatterns()`で確認する。共通入力と方式別の設定は更新前の`TrainAtcValidationLogic.UpdateValidation()`、防護方式は`TrainAtcProtectionModeLogic.UpdateProtectionMode()`で確定する。生成途中の内容はローカルに置き、全工程が成功するまでStateへ採用しない。`UpdatePattern()`は失敗時に`ClearPattern()`で経路と全パターンを消去する。電文未受信・不正・対応キーなしの場合は、第6節の無信号猶予を適用する。

この初期化では常設速度制限・停止余裕・ORP・制動曲線をまだ反映しない。

### 実装済みの常設速度制限

`UpdatePattern()`は初期化成功後に`ApplyPermanentSpeedLimits()`を呼ぶ。初期化または反映に失敗した場合は、生成を中止してパターンを一括消去する。

`ApplyPermanentSpeedLimits()`は各Edgeの`speedLimitSections`を読み、常用へ常設速度制限、非常へ常設速度制限に`EmergencySpeedMarginKmh`を加えた速度上限を反映する。共通定数の初期値は10km/hとし、非常側の初期上限・常設制限・ORPで同じ定数を使う。保持単位はm/sであり、各配列の現在値と比較して低い方を採用する。重複区間もそれぞれの低い方を優先する。

先頭Edgeの進行方向には`State.operation.currentTravelDirection`を使う。以降は直前のEdgeの出口Nodeから進入する側を判定し、Node AならA→B、Node BならB→Aとする。接続先がない・両端が同じ出口Nodeに一致して方向を特定できない場合は失敗とする。

制限区間はNode Aからの距離で定義されているため、B→Aの場合は開始を`Edge長 − 元の終了位置`、終了を`Edge長 − 元の開始位置`へ変換する。そのEdgeより前のEdge長の合計を加え、経路上の距離にする。

サンプル間の短い制限も取りこぼさないよう、ユーザー指定により制限区間を前後のサンプルまで広げる。開始は`floor(開始距離 / samplingIntervalM)`、終了は`ceil(終了距離 / samplingIntervalM)`とし、両端を含めて反映する。隣接する制限区間の共有境界では低い方の速度上限を採用する。

速度区間のリストが空なら、そのEdgeには個別の常設制限がないものとして扱う。リストや要素がnull、区間がEdgeの範囲外・逆順・長さ0、不正な数値や負の速度制限を含む場合は失敗とする。0km/hの速度制限は有効とする。

列車後端が制限区間を抜けるまでの制限保持、停止目標・ORP・制動曲線はこの処理に含めない。

## 4. 勾配補正と制動曲線の積分（実装済み）

1. 常用の許容速度を線区の営業最高速度、非常の許容速度を営業最高速度＋共通の速度余裕で初期化する。
2. 経路上の位置に対応する常設速度制限を反映する。
3. 停止目標、またはORP適用時の低速到達目標を設定する。
4. 各サンプル区間を編成が移動したと仮定して、各車両が通る区間の最小勾配を求める。
5. 各車両の質量で重み付けした勾配から有効減速度を求める。
6. 経路の終端から始点へ制動曲線を計算し、既に設定した速度上限と比較して低い方を採用する。

### 車両質量と車両位置

各車両の空車質量と長さは、`TrainRoot.ConsistDefinition`から取得する。応荷重装置の測定質量は、TIMSの各号車のLocal Busにある`BrakeControlDeviceTimsBusSource.MassKgKey`から取得する。TIMSの値は空車質量を含む総質量のため、空車質量をそのまま加算しない。

```text
積載質量 = max(0, TIMSの測定質量 − 定義の空車質量)
車両質量 = 定義の空車質量 + 積載質量
```

`TrainAtcMassInputAdapter`がこれらを収集し、`Input.cars`に各車両の総質量[kg]と固定前端から車両中心までの距離[m]を保持する。車両中心は、それより前の車両長の合計に当該車両長の半分を加えた位置とする。車両間隔を別途加算しない。

Controllerは、固定前後の受信機の取り付け位置を`Input.frontReceiverDistanceFromFrontM`・`rearReceiverDistanceFromFrontM`へ保持する。各値は該当車両の中心距離から受信機の`OffsetFromCarCenterM`を引いて求める。毎回の物理的なTrackSample位置は使用しない。

パターンのサンプル地点を、有効運転台側の受信機が通る位置として扱う。固定前側へ進む場合、各車両の中心位置は次の式とする。

```text
車両中心の経路内距離 = サンプル地点の経路内距離
    + 選択した受信機の固定前端からの距離
    − 車両中心の固定前端からの距離
```

固定後側へ進む場合は、受信機と車両中心の距離差の符号を反転する。固定前後の走行方向は`State.operation.currentTravelDirection`と`currentPosition.frontFacesAtoB`から求める。運転台の前後だけで決めないため、後進時も実際に先頭となる車両を使用する。

### 車両ごとの勾配と経路外の扱い

経路内にある車両中心の勾配は、静的Graphから取得する。進行方向に対して上りを正とする。各サンプル区間ごとに車両位置と勾配を計算し直し、現在の編成位置で求めた勾配を経路全体へ一律に適用しない。

`TrainAtcPatternHelper.TryGetMinimumGradient()`は、指定した経路区間の最小勾配を返す。区間の両端だけでなく、区間内の勾配プロファイルの各点とEdge境界の両側も確認する。プロファイル間は線形補間のため、この確認で区間内の最小値を取得する。Edgeごとの進行方向を反映し、B→Aなら勾配の符号を反転して比較する。

勾配プロファイル全体の数値・順序・両端の確認は、`PreparePath()`から`TrainAtcPatternHelper.TryPreparePath()`を呼ぶ際、経路内の各Edgeに対して1回行う。勾配補正中の`TrainAtcPatternHelper.GetGradient()`は二分探索で前後のプロファイルを取得して補間し、区間内の変化点も二分探索した開始位置から対象区間だけを確認する。

車両中心が経路の始点より手前、または終端より先にある場合は、次の近似を使う。

| 対象 | 経路外にある車両に使う勾配 |
| --- | --- |
| 常用 | 進行方向の先頭車両の中心が通る区間の最小勾配 |
| 非常 | ATC設定に保持する線区の最大下り勾配 |

先頭車両が通る区間の端が経路外なら、その端を最寄りの経路端へ制限して勾配を取得する。全区間が経路外なら、最寄りの経路端の勾配を使う。

非常で使う最大の下り勾配は、`Settings.maximumDownhillGradientPermille`へ0以上の大きさ[‰]で設定する。使用時には負号を付ける。初期値の0‰は平坦線区の設定値とする。両方向の走行を対象とした線区の最大値を手動で設定し、Graph全体からの自動算出や設定値の自動変更は行わない。設定値が負数・NaN・Infinityの場合は計算に失敗する。経路内の車両には、この代用値ではなくGraphから取得した勾配を使う。

勾配プロファイルの確認は、今回の計算で参照する経路内のEdgeを対象とする。欠損や不正な勾配を0‰として補わないが、今回の経路に含まれないEdgeの勾配プロファイルの欠損を理由にパターンを消さない。

一つの車両が経路の内外をまたぐ区間では、経路内の最小勾配と経路外の代用値を比較し、低い方を使う。各車両の最小値が異なる地点で発生する場合も、その最小値を合算して減速度を控えめに見積もる。

### 勾配から有効減速度への変換

車両質量を`m[j]`、その車両がサンプル区間を通る間の最小勾配[‰]を`gradient[j]`として、常用・非常のそれぞれで次の式を使う。重力加速度は`TrainAtcPatternHelper`の定数`GravityMps2 = 9.80665f`とする。

```text
勾配補正減速度 = 9.80665 × Σ(m[j] × gradient[j]) / (1000 × Σm[j])
```

上りでは補正が正となり、下りでは負となる。全車両が同じ質量なら、各車両の勾配の平均を使った計算と一致する。

各車両の区間内最小勾配から求めた補正を、そのサンプル区間の補正として使う。両端だけの確認で区間内の急な下り勾配を取りこぼさない。

```text
常用の有効減速度 = Settings.serviceDecelerationMps2 + 区間の常用勾配補正
非常の有効減速度 = Settings.emergencyDecelerationMps2 + 区間の非常勾配補正
```

### 後方からの速度積分

基本式は`v² − v₀² = 2aΔx`とする。後方から前方への計算は、次の関係を使う。

```text
v[i - 1] = min(その位置の速度上限, sqrt(v[i]² + 2 × a × Δx))
```

ここで`a`は前項で求めた有効減速度とする。常用と非常の配列を別々に積分する。非常側の速度余裕は既に初期化・常設制限・ORPの段階で反映済みとし、積分時に再加算しない。

25km/hなどの低速条件が常設速度制限より高い場合も、低い速度上限を優先する。低速条件を理由に許容速度を引き上げない。

質量入力が取得できない、質量・車両中心位置・受電器の取り付け距離が不正、経路内の勾配プロファイルが不正、減速度設定が非正または不正、最大下り勾配が負数または不正、有効減速度が0以下または不正の場合は生成に失敗する。共通入力・基本設定と方式別設定は`TrainAtcValidationLogic.UpdateValidation()`でまとめて確認し、生成中に判明した失敗にも無信号猶予を適用しない。`UpdatePattern()`は`ClearPattern()`で使用不可にする。キー切・中立の場合もパターンを残さない。計算失敗時の非常要求とTIMSへの受け渡しは第6節に記載する。

### 減速区間・予告区間と目標速度

積分成功後に`UpdatePatternSections()`を常用・非常・生成済み独立ORPそれぞれへ適用し、終端から始点へ区間情報を設定する。終端サンプルは目標速度を自身の許容速度、両方の区間フラグをfalseとする。

進行方向に見て許容速度が下がる区間を`isDecelerationSection = true`とする。下がり切った地点から「その速度[m/s] × 1秒」の距離まで、降下・接近区間を延長する。境界はサンプル区間単位で切り上げ、目標速度を延長区間にも保持する。速度曲線は変更せず、停止目標0m/sでは延長距離も0mとする。それ以外の一定速度・上昇区間は降下区間としない。

予告時間は`Settings.patternApproachWarningTimeSeconds`で設定し、初期値を5秒とする。減速区間を検出したら、次サンプルの許容速度[m/s]に予告時間[s]を掛け、手前へ延ばす距離[m]とする。減速区間以外では残り距離からサンプル間隔を引き、残り距離が正のサンプルにも`isApproachSection = true`を設定する。TD-ATCと同様の距離換算であり、サンプル単位の近似とする。予告時間が0なら、予告区間は減速区間だけになる。予告時間が負数・NaN・無限大なら初期化に失敗し、パターンを消す。

減速・予告区間では次サンプルの目標速度を引き継ぎ、自身の許容速度を超えないよう低い方を採用する。それ以外では、自身の許容速度を目標速度とする。停止へ向かう区間では0、ORP常用へ向かう区間では設定したORP速度を保持する。

`TrainAtcOutputLogic.UpdateAtcPatternOutput()`は、`State.pattern.normalPattern.isApproachSection`がtrueで、測定速度の絶対値が`State.pattern.normalPattern.currentTargetSpeedMps`以上なら`Output.pattern.isPatternApproaching`をtrueにする。同じ速度も予告対象に含める。表示の成立条件と消去は[全体処理仕様の工程8](TrainAtcProcessingFlow.md#8-outputを生成)に従う。

TIMSのMasterBusには機器名`ATC`、項目名`IsPatternApproaching`でBoolを公開する。更新周期は既存のMaster収集周期に従う。予告音・インジケーターとブレーキ出力への接続は後続で実装する。

### Signalの表示出力

`TrainAtcOutputLogic.UpdateAtcPatternOutput()`は`Output.pattern.signal`を設定する。最新の表示条件は[全体処理仕様の工程8](TrainAtcProcessingFlow.md#8-outputを生成)に従う。

| 条件 | Signal |
| --- | --- |
| ATC異常または無効 | `None` |
| ATC正常・有効で、常用目標速度が0より大きくORP非作動 | `Green` |
| ATC正常・有効で、上記以外 | `Red` |

パターンを使用できない場合は全体の正常性へ反映し、前回の現示を残さない。TIMSのMasterBusには`ATC/Signal`としてIntを公開し、`None=0`、`Red=1`、`Green=2`とする。Signalの公開も既存のMaster収集周期に従う。

## 5. 停止目標とORPの扱い（実装済み）

`TrainAtcLogic.Calculate()`は工程4の`UpdateValidation()`で入力・電文・猶予・保持可否を判定し、`State.validation.result`へAdopt・Retain・Unusableを保存する。工程5で防護方式を確定し、工程6の`TrainAtcPatternLogic.UpdatePattern()`が判定結果を読む。新規生成は`PreparePath()` → `InitializePatterns()` → `ApplyPermanentSpeedLimits()` → `ApplyStopTargets()` → `CalculateGradientCorrections()` → `IntegratePatterns()` → `UpdatePatternSections()`の順に実行し、成功した内容を採用して現在値を更新する。失敗時は一括消去する。無信号猶予中は速度配列を再生成せず、防護方式はProtectionModeStateで維持する。

ORPは電文の`overrunProtectionMode`が`Restricted`の場合だけ有効とする。各モードでは、後続の積分に用いる速度上限と停止目標を次のように設定する。

| 過走防護モード | 常用パターン | 非常パターン |
| --- | --- | --- |
| `Restricted` | 終端側の設定距離内を、設定したORP速度以下に制限する | 同じ区間をORP速度＋共通の速度余裕以下に制限し、最後の2サンプルを0にする |
| `Normal` | 最後の2サンプルを0にする | 最後の2サンプルを0にする |
| `None` | 設定した停止余裕距離手前から終端までを0にする | 最後の2サンプルを0にする |

### 独立ORPパターンの生成（実装済み）

独立ORPはRestrictedの場合だけ`State.pattern.orpPattern`へ生成する。配列の準備は`InitializePatterns()`、低速上限と停止目標の設定は`ApplyStopTargets()`、専用減速度での積分は`IntegratePatterns()`で行う。常用・非常と同じ経路・サンプル数・間隔を使う。Restricted以外では空の配列とし、消去時はORPも消す。無信号猶予中は他のパターンとともに保持する。

初期速度上限は次のとおりとし、共通の`EmergencySpeedMarginKmh`を使用する。

| 区間 | 初期速度上限 |
| --- | --- |
| 終端から`orpMinimumTargetMarginM`以内 | `orpSpeedLimitKmh`＋10km/h。ただし線区最高速度＋10km/hを上限とする |
| それより手前 | 線区最高速度＋10km/h |
| 最後の2サンプル | 停止目標として0km/h |

境界は既存の過走防護と同じく手前側のサンプルへ丸める。設定距離が経路より長い場合は原点から制限する。設定距離が0や1サンプル未満でも、最後の2サンプルの停止目標を優先する。Graphの区間別常設速度制限は独立ORPへ追加しない。

計算用減速度は`Settings.orpDecelerationMps2`で独立して設定し、初期値を0.7m/s²とする。各サンプルには勾配補正前の値を保存する。非常側と同じ質量加重の勾配補正を加え、設定距離以内だけ終端側から逆向きに積分する。範囲外の初期上限は維持する。勾配データの全件確認は経路準備で先に行い、ORPで繰り返さない。減速度が0以下・NaN・Infinityの場合や、補正後の有効減速度が非正・不正の場合は生成に失敗する。

生成後は`UpdatePatternSections()`で目標速度・降下区間・接近区間を常用・非常と同じ方法で求める。接近の予告は既存の`patternApproachWarningTimeSeconds`を使用する。

現在位置のORP許容速度は`UpdateCurrentPattern()`で前後サンプルの速度の二乗を補間し、`State.pattern.orpPattern.currentAllowSpeedMps`に保存する。ORPがない場合とパターン消去時は0に戻す。無信号猶予中も現在位置から毎tick更新する。通常非常と別の配列で保持し、非常照査では通常非常と独立ORPの両方を確認する。

独立ORPの目標速度は`State.pattern.orpPattern.currentTargetSpeedMps`、接近状態は`isApproachSection`、降下状態は`isDecelerationSection`に保存する。ORPがない場合とパターン消去時は現在値と区間状態を初期値に戻す。保持中も現在位置から更新する。表示用許容速度の比較とORP表示はOutputLogicで行い、パターンの配列を合成しない。

### ORP表示

ORPのモニター表示は`TrainAtcOutputLogic.UpdateAtcPatternOutput()`が`Output.pattern.isOrpOperating`へ生成する。現在はRestrictedかつ独立ORPの降下区間内で有効とする。最新の条件は[全体処理仕様の工程8](TrainAtcProcessingFlow.md#8-outputを生成)を参照する。

ORP表示中は`Output.pattern.isSpeedIndicated`をfalseにする。TIMSへ現示速度と`ATC/IsSpeedIndicated`を転送し、速度計UIがフラグに従って消灯する。StateとOutputの計算用許容速度は維持し、消灯のために0へ書き換えない。

### 設定値と単位

- ORPの距離は`Settings.orpMinimumTargetMarginM`から取得し、初期値を100mとする。
- ORPの常用速度は`Settings.orpSpeedLimitKmh`から取得し、初期値を25km/hとする。
- `None`の常用停止余裕は`Settings.serviceStopMarginM`から取得し、初期値を100mとする。
- 非常の速度余裕は`TrainAtcPatternLogic`の`private const float EmergencySpeedMarginKmh = 10f`で設定する。ORPの非常上限は初期値で35km/hとなる。
- 配列に書く際は、km/hを3.6で割りm/sへ換算する。

既存の常設制限や線区最高速度に対応する上限が低い場合は、その低い値を維持する。ORPの低速制限によって速度を引き上げない。停止目標の0m/sには速度余裕を加算しない。

### 設定距離とサンプルの対応

経路長を`L`、サンプル間隔を`Δx`、最終サンプルのindexを`N`とする。ORPと`None`の常用では、設定距離を`marginM`として次のindexから終端までを埋める。

```text
開始位置 = max(0, L − marginM)
開始index = clamp(floor(開始位置 / Δx), 0, N)
```

境界がサンプル間にある場合は、その手前のサンプルから制限する。ORPでは設定距離以上手前で低速制限が始まる。経路が設定距離より短い場合は、経路の原点から制限する。

「停止限界−1サンプル目」は`N − 1`とし、`N − 1`と`N`の両方を0にする。この停止位置は実際のサンプル間隔から決めるため、停止余裕は`Δx`となる。最大サンプル間隔の初期値5mに対して、実際の余裕は5m以下となる。`emergencyStopMarginM`はこの処理では参照しない。

`None`の100mは、地上が送る停止限界から車上で一度だけ求める。地上で補正済みの距離と重複して加算しない。

### 距離の例

経路長500m、サンプル間隔5m、各設定が初期値の場合は次の位置を使う。

| モード | 常用 | 非常 |
| --- | --- | --- |
| `Restricted` | 400mから終端まで25km/h以下 | 400mから35km/h以下、495m・500mは0km/h |
| `Normal` | 495m・500mを0km/h | 495m・500mを0km/h |
| `None` | 400mから終端まで0km/h | 495m・500mを0km/h |

ここで設定する値は積分前の上限であり、手前からの滑らかな降下は第4節の制動曲線の積分で作る。連動装置が停止限界の先に確保する過走防護区間の長さとは別の値として扱う。

不正なモード、使用する設定値に負数・NaN・無限大がある場合や、サンプル数などが不正な場合は失敗とする。`Normal`ではORPと`None`用の設定値を参照しない。

## 6. ブレーキ出力（常用制御とTIMS接続は実装済み）

常用ブレーキ要求は、ATCが有効で、現在速度と常用パターンを取得できる場合を対象とする。キー入中の非常ブレーキ要求と入力不成立時の扱いも、本節に記録する。

### ブレーキ状態と出力の基盤（実装済み）

ドア全閉未確認による転動防止は`State.brake.isRollingPreventing`へ毎tick保存する。TIMSの編成全体のドア状態が未全閉、または取得できない間は常用最大刻み段の半分（整数除算で端数切り捨て）へ即時切替する。停車中だけに限定せず、全閉確認後は通常の常用介入・緩解判定へ戻る。キー切・レバーサ中立では転動防止も解除する。ドア情報の取得失敗だけでATC故障や非常要求を追加しない。

`Output.brake.isRollingPreventing`へStateの判定を反映し、TIMS MasterBusの`ATC/IsRollingPreventing`にも転送する。他の原因による非常保持中は、従来どおり非常指令を優先して常用段を0とする。

`State.brake`に`TrainAtcBrakeState`を保持する。`isEmergencyBrakeRequired`は今回の非常作動原因、`isEmergencyHold`は解除まで引き継ぐ非常保持、`isNormalBrakeRequired`は常用介入を表す。目標段・現在段は`targetBrakeStep`・`currentBrakeStep`、待ち時間は`brakeChangeElapsedSeconds`、選択行は`brakeStepTableIndex`に保持する。常用介入・緩解は`UpdateNormalBrakeState()`、ヒステリシスによる目標段の選択は`UpdateTargetBrakeStep()`、刻み切替は`UpdateCurrentBrakeStep()`で行う。入口の`UpdateBrakeState()`から毎tick更新する。

`Output.brake`に`TrainAtcBrakeOutput`を保持する。各工程の後に`TrainAtcOutputLogic.UpdateAtcOutput(context)`が表示とブレーキ指令を生成する。Stateは変更しない。`UpdateAtcBrakeOutput()`は非常保持を`isEmergencyBrakeRequired`へ反映し、非常中は常用段を0、それ以外はStateの`currentBrakeStep`を出力する。非常と常用の指令は別の項目で扱う。

`Calculate()`は前工程が使用不可でも`UpdateBrakeState()`を呼ぶ。ValidationLogicが入力・電文・保持可否、PatternLogicが生成・現在値の更新を担当し、親Logicが全体の`State.isAtcHealthy`を集約する。`HasEmergencyBrakeCause()`は確定結果と`State.pattern.isValid`を参照し、不成立なら非常を保持する。原因消失後も停止・マスコン非常位置を確認するまで保持する。確認済みキー切・中立では`ClearBrakeState()`で全ATCブレーキ状態を解除し、最後のOutput生成へ反映する。

保持可否はValidationLogicで確認し、生成・保持後の現在距離とサンプル参照はPatternLogicの`UpdateCurrentPattern()`で更新する。現在の許容速度・目標速度・減速度・区間状態をStateに残し、BrakeLogicとOutputLogicはその結果を使用する。基準段を選ぶために電文やパターン使用可否を判定し直さず、TIMSへ転送済みの表示値にも依存しない。

常用パターン超過の介入とTIMSへの受け渡し、通常非常の速度超過照査、Restricted時の独立ORPの速度超過照査を実装する。非常作動後は保持・解除条件に従う。

### TIMSへの受け渡し（実装済み）

`TimsNotchController.CollectInput()`が、同じTrainRoot配下の`TrainAtcController.Context.Output.brake`を毎tick読み、`Input.atcBrakeStep`と`Input.isAtcEmergency`へ設定する。Inspectorの`Atc Controller`が未指定なら同じ編成のATCを自動取得する。ATC未搭載の編成では要求なし、参照先が別編成または自動取得時に複数ある場合はTIMSの入力不成立、取得済みのATCが無効なら非常要求とする。

TIMSはATC段と手動段の大きい方を採用する。非常要求があれば常用より優先する。採用した段は既存の力行・制動計算から各車のVVVF・BrakeControlDeviceへ渡り、ATCの常用介入中は力行を遮断する。

現在の実行順序では、TIMSの制御計算はATCより前に行うため、前tickに確定したATC指令を使う。ATCの計算から各車の制動指令への反映は1tick後となる。ブレーキ指令にはMasterの表示収集周期を適用しない。

### レバーサ中立の場合

有効運転台のレバーサが中立であることを確認できた場合は、キー入中でもATCを有効にしない。`isAtcEnabled`をfalse、`currentTravelDirection`を`Unspecified`とし、前回のブレーキパターンを消す。速度照査による新たな常用・非常要求と停止保持は行わない。表示上のSignalは`None`、パターン接近はfalseとする。

中立による進行方向の未指定やパターン未生成を、計算失敗による非常作動原因として扱わない。中立を確認できた場合は、キー切と同じくATCの常用・非常要求を即時に解除し、非常要求の保持状態もクリアする。TIMSの手動ブレーキ要求は継続する。次にレバーサを前進または後退にしたときは、その時点の入力から新たに介入を判定する。

中立を確認できている場合は、ATC要求の解除を優先する。運転台・マスコン状態の取得失敗と、確認済みの中立は区別する。

### 速度偏差と基準ノッチ

比較には現在速度の大きさと、現在位置の常用パターン速度を使う。目標速度との偏差ではない。

```text
速度偏差[km/h] = 現在速度の大きさ[km/h] − 常用パターン速度[km/h]
```

常用要求の緩解幅は`Settings.normalBrakeReleaseMarginKmh`で設定する。初期値を3km/hとし、0以上の値を指定する。緩解速度偏差はこの値に負号を付けたものとし、3km/hなら−3km/h、1km/hなら−1km/hで緩解する。パターンが降下中の場合と降下していない場合の両方に適用し、停止保持条件を優先する。

降下中の基準ノッチは、勾配補正前の常用減速度に最も近いTIMSの常用ブレーキ段を選ぶ。`TrainAtcBrakeLogic.TryGetBaseBrakeStep()`が`TrainAtcBrakeHelper.TryGetNearestBrakeStep()`へ`State.pattern.normalPattern.currentDecelerationMps2`と`Input.brakeSettings`の減速度表・刻み数・常用最大段を渡す。TIMSと同じ補間式で各段を比較し、緩解0stepも候補とする。差が同じ場合は小さい段を採用する。取得した基準段は同じクラス内の`UpdateTargetBrakeStep()`で使い、選択失敗時は常用最大へ切り替える。受け渡しの詳細は[車上ATCの入力・状態・出力](TrainAtcInput.md)を参照する。

### パターンが降下していない場合

- 速度偏差が0km/hを超えたら、常用最大ブレーキを要求する。
- 介入後は、速度偏差が緩解速度偏差以下になったらATCの常用要求を解除する。
- 介入後の速度偏差が緩解速度偏差より大きい間は、常用最大を維持する。

### パターンが降下中の場合

- 常用パターン速度を超過したら、速度偏差テーブルに応じて常用ブレーキを要求する。
- ブレーキを強める速度偏差と、弱める速度偏差を別々に定義する。
- 増段・減段の閾値に差を設け、その間ではテーブルで選択した段を維持してヒステリシスを持たせる。
- 介入後は、速度偏差が緩解速度偏差以下になったらATCの常用要求を解除する。ただし、共通の停止保持条件を満たしている場合は常用最大刻み段の半分を保持する。
- テーブルの初期値は、速度偏差0.5km/hごとに1step増減する。同じ2段の境界では、弱める閾値を強める閾値より0.5km/h低くする。

速度偏差テーブルの各行には、次の3項目を持たせる。

| 項目 | 内容 |
| --- | --- |
| 基準ノッチからの刻み増減数 | 整数。正は増段、負は減段、0は基準ノッチ。TIMSの刻み1段を単位とする |
| 強める速度偏差[km/h] | この段へブレーキを強めるときに使う閾値 |
| 弱める速度偏差[km/h] | 一つ上の段から、この段へブレーキを弱めるときに使う閾値 |

設定は`Settings.brakeStepTable`に`List<TrainAtcBrakeStepCondition>`として保存する。各行のフィールドは`brakeStepOffset`・`increaseToDeviationKmh`・`decreaseToDeviationKmh`とし、基準段からの増減数の小さい順に並べる。設定の保存とContextへのコピーは実装済みとする。目標段を強める判定では一つ上の行の`increaseToDeviationKmh`、弱める判定では一つ下の行の`decreaseToDeviationKmh`を使い、どちらも満たさない場合は前回の目標段を保持する。

基準ノッチからの刻み増減数を`n`としたとき、初期値は次の関係とする。

```text
強める速度偏差[km/h] = n × 0.5
弱める速度偏差[km/h] = n × 0.5
```

初期設定は次の5行とし、Inspectorから行を追加・変更できる。正の偏差はパターン速度より速い状態、負の偏差はパターン速度より遅い状態を表す。

| 刻み増減数 | 強める速度偏差[km/h] | 弱める速度偏差[km/h] |
| --- | --- | --- |
| −2 | −1.0 | −1.0 |
| −1 | −0.5 | −0.5 |
| 0 | 0.0 | 0.0 |
| ＋1 | ＋0.5 | ＋0.5 |
| ＋2 | ＋1.0 | ＋1.0 |

例えば基準＋1から基準＋2へ強める条件は＋1.0km/h以上、基準＋2から基準＋1へ弱める条件は＋0.5km/h以下となる。同じ行の二つの閾値が同値でも、隣り合う行を参照するため、0.5km/h幅のヒステリシスになる。

`UpdateTargetBrakeStep()`では、未選択時に増減数0の行から判定を始める。次の行の強める閾値以上であれば、条件を満たす間、行を進める。増段しなかった場合は、一つ下の行の弱める閾値以下である間、行を戻す。急な偏差変化では同じ呼び出し内で複数行を移動し、条件を満たさなくなった行またはテーブル末端で止める。前回の行は`brakeStepTableIndex`へ保持する。基準段と行の増減数を合計し、`targetBrakeStep`を0から常用最大の範囲へ収める。基準段が変わった場合も選択済みの行の増減数を使う。

停止保持、常用要求の解除、降下していない分岐、`ClearBrakeState()`では選択行を−1へ戻す。ブレーキ設定が未取得、現在位置のパターンや基準段を取得できない、テーブルが空・不正・基準行がない場合は、`SetMaximumServiceBrakeStep()`で現在段・目標段を入力された常用最大へ即時に切り替える。常用要求をtrue、選択行を−1、待ち時間を0へ戻す。テーブルは有限の閾値、昇順で重複のない刻み増減数（連番でなくてもよい）、単調に増える閾値を要求し、隣り合う2段の弱める閾値を強める閾値より低く設定する。降下中の分岐では目標段を設定した直後に`UpdateCurrentBrakeStep()`を呼び、指定間隔ごとに実出力段を目標へ近づける。初回の基準段は即時に設定する。

常用パターン速度を超過して介入した時点では、基準ノッチを即時に設定する。その後は、増段・減段それぞれの閾値と前回選択した段から目標段を決め、指定秒数ごとの1step切り替えを行う。速度偏差が緩解速度偏差以下になった場合の完全緩解は、テーブルとは別に即時判定する。

### 出力の評価と段の切り替え

ブレーキ要求は毎tick評価・出力する。そのtickの測定速度と現在位置のパターンを使い、常用要求の介入・緩解、速度偏差テーブルによる目標段、停止保持を判定する。ヒステリシスの閾値に達するまではテーブルで選択した目標段を維持する。

速度偏差テーブルによる段の切り替えは、指定した秒数ごとに1stepずつ行う。1stepはTIMSの刻み1段とする。目標段が現在の出力段より強ければ1step増段し、弱ければ1step減段する。目標段と出力段が同じなら切り替えない。速度偏差が一度に複数の閾値を跨いでも、段の切り替え間隔ごとに1stepずつ目標段へ近づける。テーブルの目標段が常用最大や0stepとなった場合も、テーブルによる切り替えである限りこの間隔を適用する。

速度偏差テーブルを使わない切り替えは即時に行う。降下中の介入開始時は基準ノッチを即時に設定し、その後のテーブルによる増減に切り替え間隔を適用する。降下していない場合の超過による常用最大、速度偏差が緩解速度偏差以下になった場合の完全緩解、共通の停止保持による常用最大刻み段の半分も即時に設定する。

切り替え間隔は`Settings.brakeStepChangeIntervalSeconds`から読み、初期値を0.1秒、Inspectorの最小値を0.01秒とする。`UpdateCurrentBrakeStep()`は`Input.deltaTimeSeconds`を`State.brake.brakeChangeElapsedSeconds`へ加算し、間隔を経過するごとに目標へ1step近づける。複数の間隔を経過した呼び出しでは、その回数分の切り替えを行う。目標に到達した場合、停止保持・完全緩解・降下していない場合の即時切り替えでは経過時間を0へ戻す。目標変更中も経過時間を引き継ぐ。間隔が非正・不正、経過時間入力が負数・不正の場合は、`SetMaximumServiceBrakeStep()`で現在段・目標段を常用最大へ即時に切り替え、常用要求をtrue、選択行を−1、経過時間を0へ戻す。出力段を変更しないtickも現在の段を出力する。

TIMSへ渡す制御用のブレーキ要求も毎tick更新する。`TimsNotchController`がATCのOutputを直接読む。表示用のMaster収集周期とは別に扱う。

### 共通の停止保持

常用パターン速度が5km/h未満で、測定速度の大きさが0.1km/h以下の場合は停止中と判定し、パターンの降下状態によらず常用最大刻み段の半分（整数除算で端数切り捨て）を保持する。この条件を満たす間は、通常の緩解条件より停止保持を優先する。停止保持中は`isNormalBrakeRequired`をtrueにする。`SetHalfServiceBrakeStep()`で現在段・目標段を即時に設定し、選択行を−1、待ち時間を0へ戻す。

停止保持を終えた後の要求の扱いはまだ確定していない。

### 非常ブレーキの作動と解除

キー入中でレバーサが前進または後退の場合に、現在速度の大きさが現在位置の非常パターン速度を超えたら、ATCの非常ブレーキ要求を即時に出す。 Restrictedの場合は独立ORPの許容速度超過も非常作動原因とする。非常要求は保持し、非常パターン速度以下になっただけでは解除しない。

作動した非常要求は、次の3条件をすべて満たしたときに解除する。

1. 測定速度の大きさが0.1km/h以下で、停止中である。
2. すべての非常作動原因が解消している。
3. 有効運転台のマスコンが非常ブレーキ位置である。

マスコンの非常位置は、有効運転台の入力から取得した`State.operation.cab.isEmergencyBrake`で確認する。ここで解除するのはATCの非常要求であり、マスコンが非常位置の間はTIMSの手動非常ブレーキ要求が継続する。

キー切またはレバーサ中立を確認できた場合は、上記の3条件を待たずにATCの非常要求と保持状態をクリアする。

非常要求は常用要求より優先し、速度偏差テーブルの切り替え間隔を待たずに作動・解除を判定する。ブレーキ出力と同様に毎tick評価する。

### 入力やパターン計算が成立しない場合

キー入中でレバーサが前進または後退の場合、電文がnull、不正、または現在のEdge IDと進行方向に対応するキーがないときは、連続する無信号時間を`State.validation.noSignalElapsedSeconds`へ毎tick積算する。

前回採用した有効なパターンがあり、無信号時間が`Settings.noSignalTimeoutSeconds`以下の間は、その経路・速度配列・過走防護モードを保持する。現在位置と方向に対応するpath内距離を求め、許容速度・降下状態・パターン接近状態を毎tick更新し、保持したパターンで常用ブレーキの照査を続ける。受信結果を前回電文で置き換えたり、直前の許容速度に固定したりしない。

無信号でなくなった場合は、後続のパターン生成の成功を待たず`UpdateNoSignalTime()`で積算時間を0へ戻す。受信回復の判定は電文の有無・有効性・現在Edgeと照査方向のキー取得で行う。運転台側の受電器を切り替えた場合は保持しない。キー切・中立では受電器選択を解除し、パターンと無信号時間を消す。

次のいずれかが発生した場合は、ATCの非常ブレーキ要求を即時に出す。

- 無信号時間が許容時間を超えた、または許容時間が0である。
- 無信号時に保持できるパターンがない、または現在位置・方向で保持経路を使用できない。
- ATCの保持位置が不明である。
- 測定速度を取得できない、または測定速度が不正である。
- TIMSの常用ブレーキ設定を取得できない。
- 質量などの計算用入力や設定値が不正である。
- パターン計算に失敗し、有効なパターンを取得できない。

有効運転台やマスコンの状態を取得できない場合も、ATCの非常ブレーキ要求を即時に出す。キー状態を確認できない場合に、入力の初期値falseをキー切として扱って要求を解除しない。

これらによる非常要求も、前項と同じ解除条件を使う。キー入かつレバーサが前進または後退の場合は、作動原因が残っている間、停止してマスコンを非常位置にしても解除しない。キー切またはレバーサ中立を確認できた場合は、ATC要求と非常の保持状態を即時にクリアする。

キー入かつレバーサが前進または後退であることを確認できている場合や、運転台・マスコン状態を取得できない場合は、全体の正常性がfalseでも非常判定を省略しない。確認済みの中立による無効化では、無信号・位置不明・パターン未生成などを理由とする新たな非常要求は出さない。

### キー切の場合

マスコンキーが切であることを確認できた場合は、ATCの常用・非常要求を即時に解除し、非常要求の保持状態もクリアする。これはATC要求の解除であり、TIMSの手動ブレーキ要求は継続する。次にキー入となったときは、その時点の入力から新たに介入を判定する。

キー切を確認できている場合は、ATC要求の解除を優先する。運転台・マスコン状態の取得失敗と、確認済みのキー切は区別する。

## 7. 未決事項

以下はまだ確定していない。旧プロジェクトの動作や値を、そのまま採用済みとして扱わない。

| 項目 | 決める内容 |
| --- | --- |
| 計算用減速度の調整 | 常用0.5m/s²・非常1.2m/s²を暫定値とし、実際の制動性能に合わせた値を別途決める |
| 降下中の速度偏差テーブル | 使用する行の範囲。初期設定の5行、目標段を0から常用最大へ収める処理、閾値と同じ場合の切り替え、設定した速度偏差での緩解は第6節に記載済み |
| 常用要求の切り替え | 区間移行時の要求の引き継ぎ。毎tickでの評価・出力、テーブルによる指定秒数ごとの1step切り替え（初期値0.1秒）、それ以外の即時切り替えは第6節に確定済み |
| 停止保持 | 保持条件を満たさなくなった後の常用要求。停止判定は測定速度の大きさが0.1km/h以下として第6節に確定済み |
| サンプル間の扱い | 現在位置の許容速度の補間方法。停止目標・ORP開始位置の反映方法は第5節に確定済み |
| 列車長と受信機位置 | 後端が速度制限区間を抜けるまでの制限保持、停止照査に用いる受信機と車両先頭の位置差の扱い。勾配補正用の車両中心位置は第4節に確定済み |
| 目標通過後 | 現在位置が停止目標を越えている場合の照査・ブレーキ出力。短い経路の上限設定は第5節に確定済み |
| 入力や計算の不成立 | 非常作動原因の保持と通知の形式。無信号・不正電文の連続時間による猶予、猶予超過または保持パターンなしの場合の非常、位置不明・速度未取得・計算失敗、および運転台・マスコン状態の取得失敗を即時非常とする条件は第6節に確定済み |

地上側は過走防護モードを電文に設定し、車上側がそのモードに従って通常パターンまたはORPを適用する。

## 8. TD-ATCの参考実装

参考元は前プロジェクトの次のファイルとする。

- `TD-ATC/Assets/Scripts/Train/Safety/Atc/Onboard/AtcOnboardLogic.cs`：`UpdateBrakePattern()`、`ApplyResolveEndProtection()`
- `TD-ATC/Assets/Scripts/Train/Safety/Atc/Onboard/AtcOnboardController.cs`：計算設定の初期値
- `TD-ATC/Assets/Scripts/Train/Safety/Atc/Onboard/AtcSpeedPatternOutput.cs`：サンプルの保持・許容速度の参照

前作から採用することが確定したのは、最初のEdgeの進入端を原点とする経路表現と、最大5mで終端に一致する等間隔サンプリングである。後方から制動曲線を計算する基本方針も今回のメモと共通する。

前作の設定初期値には、常用減速度0.5m/s²、非常減速度1.2m/s²、非常側の速度許容差約10km/hがある。今回のユーザー指定により、減速度は常用0.5m/s²・非常1.2m/s²を暫定初期値として採用する。非常側の速度余裕10km/hも共通定数として採用する。

前作は非常パターンの終端を速度許容差の値へ上書きしているが、今回は非常の停止目標以降を0m/sとする指定に従う。今回のORPは`Restricted`使用時だけ適用し、前作のORP処理の具体的な計算設定は未決のまま引き継がない。

## 関連資料

- [ブレーキパターンの元メモ](../../Track/Atc/TrackAtcBrakePatternMemo.md)
- [地上側ATC 基本仕様](../../Track/Atc/TrackAtcGroundSpecification.md)
- [車上ATCの入力](TrainAtcInput.md)
