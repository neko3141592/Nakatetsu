# ATO・TASCの仕組みとNakatetsuへの実装方針

採用方針：最初にTASCの定位置停止だけを純粋C#の閉ループ制御として実装し、既存TIMSのノッチ・ブレーキ計算へ接続する。停止精度と異常系を検証した後、駅間の力行・惰行・速度追従、出発・停車中制御を加えてATOへ拡張する。

調査日：2026-09-08。公開されている行政資料、鉄道事業者・メーカー・研究機関の技術資料、論文を確認した。本文は実在設備の安全認証仕様ではなく、鉄道シミュレーターであるNakatetsuへ仕組みを再現するための設計提案である。

## 1. 用語と対象範囲

国土交通省の用語では、ATCは制限速度情報に基づいて列車速度を連続照査し、必要ならブレーキ制御を行う保安側の装置である。一方、ATOは発車、加減速、定時運転、定位置停止を自動的に行う装置とされる。[国土交通省 中部運輸局「主な鉄道用語」](https://wwwtb.mlit.go.jp/chubu/tetsudou/yougo.html)

TASC（Train Automatic Stop-position Control / Controller）は、ATOのうち駅の定位置へ停止させる機能だけを取り出したものと考える。東京都の資料は、車上子と地上子によって停止位置までの正確な距離を把握し、自動でブレーキ制御すると説明している。また、ATOもTASC機能を含むとしている。[東京都「ホームドア整備に関する現状・課題等」](https://www.toshiseibi.metro.tokyo.lg.jp/documents/d/toshiseibi/pdf_bunyabetsu_machizukuri_bfree_pdf_tetsudo_bfree_11)

この文書では名称を次のように統一する。

| 名称 | Nakatetsuでの責務 |
| --- | --- |
| ATC / ATP相当 | 許容速度を超えないよう監視し、必要ならATOより強いブレーキを要求する保安系 |
| TASC | 指定された停止目標までの残距離からサービスブレーキを自動選択し、定位置に停止する機能 |
| ATO | 出発条件確認、駅間の力行・惰行・速度制御、TASCへの移行、停車完了までを扱う運転系 |
| 運行・駅停車サービス | 次の停車駅、停止目標、予定時刻、停車時間をATOへ渡す上位系 |

今回の実装対象はGoA（自動化レベル）を再現・認証することではない。運転士が操作を監視できるATO/TASC相当をゲーム上で再現する。無人運転に必要な前方監視、避難、遠隔指令、ホーム安全確認などは別機能である。IEC 62290シリーズも都市鉄道の自動化をGoA 1から4までの広い範囲として扱っている。[IEC 62290-1:2025](https://webstore.iec.ch/en/publication/83773)

## 2. 実設備から確認できる基本構成

### 2.1 位置と速度を知る

TASCは、車輪回転等から得る速度・走行距離と、停止位置の手前に置かれた地上子等による絶対位置補正を組み合わせる。位置を速度だけから積算すると車輪径差や空転・滑走で誤差が蓄積するため、既知地点を通過した時点で補正する。

日暮里・舎人ライナーの事例では、停止位置の240m、85m、10m手前に地上子を置き、その距離情報から車上ATOが停止パターンを発生させる。停止域に入ったことも地上・車上間で確認し、ドア制御に利用している。[電気設備学会誌「日暮里・舎人ライナーの自動運転制御」](https://www.jstage.jst.go.jp/article/ieiej/35/8/35_588/_pdf)

Nakatetsuでも、内部の正解座標をATOへ直接渡し続けるのではなく、次の2層に分ける。

1. 車両運動の正解値として、経路上の位置と速度を更新する。
2. ATO/TASC入力として、オドメトリで積算した推定位置を渡し、地上子イベントで補正する。

最初のMVPでは両者を同じ値にしてよい。ただし型と入力口を分けておけば、後から位置誤差、地上子、車輪径補正、空転・滑走を追加できる。

### 2.2 停止パターンを作る

定位置停止では、列車位置に対する目標速度を定めた「位置―速度パターン」が一般的な考え方である。停止点で目標速度を0とし、残距離が短くなるほど目標速度を下げる。目標速度と実速度を比較して、ブレーキ力またはブレーキノッチを調整する。[木村彰「電気車の定位置停止制御システムのための加速度変化率を制限した非線形速度制御」](https://www.jstage.jst.go.jp/article/ieejias1987/120/12/120_12_1484/_pdf/-char/ja)

一定減速度を仮定した最小構成は次式で表せる。

```text
d       = 停止目標までの残距離 [m]
a_p     = 停止パターンの基準減速度 [m/s²]
v_ref   = sqrt(2 * a_p * max(d - d_margin, 0))
a_req   = v² / (2 * max(d, d_epsilon))
```

`v_ref` はその位置で許される目標速度、`a_req` は現在の速度から残距離内で止まるために必要な平均減速度である。実装では式だけでノッチを一意に決めず、速度偏差、実減速度、ブレーキ応答遅れ、勾配、ノッチ変更後の保持時間を含めて指令を選ぶ。

鉄道総研は、停止位置までの残距離を必要減速度へ変換し、実際の減速度をフィードバックして制御ブレーキノッチを選ぶ「距離基準減速度制御」を説明している。同じノッチでも天候、滑走、回生状態等によって実減速度が変わるため、指令表を読むだけの開ループ制御ではなく、結果を戻す閉ループ制御が停止精度に重要である。[鉄道総研「減速度フィードバックの機能追加によるブレーキ距離精度の向上」](https://www.rtri.or.jp/rd/news/vehicle/vehicle_202104.html)

### 2.3 乗り心地と低速安定性を両立する

ブレーキ指令を毎フレーム大きく変えると、加速度変化率（ジャーク）が大きくなり乗り心地が悪化する。一方で応答を遅くしすぎると、停止直前の誤差を修正できない。位置―速度パターン追従は特に低速域で不安定になりやすいという指摘があるため、以下を別々に設定する。

- 目標減速度または目標速度の変化率制限。
- ノッチを変更した後の最小保持時間。
- ノッチを上げる閾値と下げる閾値を分けたヒステリシス。
- 停止直前だけを扱う低速制御と、停止後の保持ブレーキ。
- オーバーラン回避を優先して制限を緩める非常寄りの分岐。

制御周期はUnityの描画フレームから切り離す。MVPでは `0.1 s` を初期値とするが、これはNakatetsuの採用値であり業界標準値という意味ではない。物理更新が細かい場合も、ATO判定は固定周期で実行し、同じ入力列ならフレームレートに依存せず同じ指令列になるようにする。

### 2.4 ATOは駅間走行とTASCを連続させる

ATOは駅間で、線路・ATCの速度制限を超えない目標速度に追従するよう力行、惰行、ブレーキを選択する。駅へ近づいたら、通常の速度制御からTASCの停止パターンへ移行する。

日暮里・舎人ライナーの事例では、ATC信号より3km/h低い速度を目標に駅間を走行し、駅接近時に定位置停止パターンへ移る。また出発には、ドア閉、進行方向、進路、ATCコード、車両正常、出発抑止なし等の条件成立を確認している。数値や具体条件をNakatetsuへ固定的にコピーするのではなく、「保安上限より運転目標を低くする」「出発インターロックをまとめて判定する」という構造を採用する。

## 3. Nakatetsuの現状との対応

現在のコードはATOそのものをまだ持たないが、接続先になる部品は既にある。

| 現在の要素 | 利用方法 | 不足しているもの |
| --- | --- | --- |
| `GuideLineDefinition` / `GuideLineSample` | 経路距離、位置、勾配の取得 | 分岐を跨ぐ経路距離、駅・停止目標、地上子 |
| `TimsNotchLogic` | 手動ブレーキとATCブレーキを統合 | ATO専用指令、運転モード、操作権限の調停 |
| `TimsBrakeCalculator` | 目標減速度と連続ブレーキステップの相互変換 | TASCが要求する減速度の生成、指令ヒステリシス |
| `TimsBrakeLogic` | 編成質量に応じた回生・空制配分 | 実減速度をATOへ戻す経路、応答遅れを含む車両運動 |
| `TimsTractionLogic` | 力行力計算とVVVFへの配分 | ATO力行ノッチ、目標速度追従、惰行選択 |
| `TimsBusState` | 指令・状態を表示や各車へ配る基盤 | ATO状態、目標速度、残距離、停止誤差等のタグ |

重要なのは、ATOブレーキを現在の `atcBrakeStep` に入れないことである。ATCは保安上の介入、ATOは通常運転の指令であり、原因、優先順位、表示、故障時の扱いが異なる。入力と出力を別フィールドにして、最後に調停する。

## 4. 推奨アーキテクチャ

### 4.1 配置

空のフォルダを先に大量に作らず、最初は以下の単位で追加する。

```text
Assets/Nakatetsu/
├── Train/Runtime/AutomaticOperation/
│   ├── AtoContext.cs
│   ├── AtoLogic.cs
│   ├── TascPatternCalculator.cs
│   └── AtoCommandArbiter.cs
├── Train/Tests/EditMode/AutomaticOperation/
│   ├── TascPatternTests.cs
│   └── AtoLogicTests.cs
├── Track/Runtime/Operations/
│   ├── TrackOperationalPoint.cs
│   └── TrackBeaconDefinition.cs
└── Simulation/Runtime/StationStop/       # 運行サービス実装時に追加
    ├── StationStopTarget.cs
    └── StationStopService.cs
```

責務は次の流れにする。

```text
運行・駅停車サービス
  次停車駅、停止目標、予定時刻、停車時間
        ↓
経路・位置推定 ── 地上子補正
  経路上位置、速度、加速度、勾配、残距離
        ↓
ATO状態機械 ── ATC許容速度・出発条件
  目標速度、力行/惰行/制動、TASC状態
        ↓
指令調停
  非常・ATC・手動・ATOの優先順位
        ↓
既存TIMS Notch → Brake / Traction → 車両運動
        └──────── 実速度・実減速度をフィードバック
```

`AtoLogic` と `TascPatternCalculator` はUnity API、シーン探索、ScriptableObject参照を持たない純粋C#にする。MonoBehaviourのControllerは、入力収集、固定周期呼び出し、TIMSへの反映だけを担当する。既存TIMSのContext・Logic方針と揃う。

### 4.2 データ契約

最初の `AtoContext` は概ね次の情報を持つ。

```text
AtoState
  mode                    Disabled / Standby / Automatic / Fault
  phase                   Ready / Departing / Power / Coast / SpeedControl /
                          Approach / FinalBrake / Stopped
  estimatedRoutePositionM
  filteredAccelerationMps2
  selectedPowerNotch
  selectedBrakeStep
  commandHoldTimerSeconds
  lastBeaconId

AtoInput
  deltaTimeSeconds
  controlEnabled
  departureRequested
  departureInterlocksSatisfied
  travelDirectionSign      # 経路距離の増加方向を+1、減少方向を-1
  routePositionM          # MVPでは正解値、後に推定位置へ置換
  speedMps
  accelerationMps2        # 進行方向を正とした符号付き加速度
  gradientPermille
  nextStopTargetPositionM
  hasNextStopTarget
  permittedSpeedMps       # ATC/保安系が許す上限
  routeSpeedLimitMps
  doorsClosedAndLocked
  brakeFeedbackAvailable

AtoSettings
  controlIntervalSeconds
  patternDecelerationMps2
  maximumServiceDecelerationMps2
  maximumJerkMps3
  atcSpeedMarginMps
  speedDeadbandMps
  decelerationDeadbandMps2
  notchMinimumHoldSeconds
  approachActivationDistanceM
  finalBrakeActivationSpeedMps
  stoppedSpeedThresholdMps
  stopPositionToleranceM
  positionValidityTimeoutSeconds

AtoOutput
  hasCommand
  powerNotch
  brakeStep
  targetSpeedMps
  targetDecelerationMps2
  distanceToStopM
  stopPositionErrorM
  phase
  reason
  isStoppedInPosition
  fault
```

設定値は車種と路線で変わる。共通ロジックにリテラルを埋め込まず、ATO車上設定、路線運転設定、駅停止目標へ分離する。停止目標は編成長・進行方向・ホームドア位置によって変えられる識別子を持たせる。

### 4.3 指令の優先順位

指令調停は少なくとも次の原則を満たす。

1. 非常ブレーキ要求は他の全指令より優先する。
2. ATCブレーキはATOの力行・弱いブレーキより優先する。
3. ブレーキが1段でも成立したら力行は0にする。
4. Automaticモードで出発条件が成立したときだけATOの力行を採用する。
5. 手動介入を検出した場合は、仕様で定めた方法でATOをStandbyへ戻す。手動とATOの力行値を加算しない。
6. 位置、次停止目標、速度、ブレーキ応答の必須入力が無効または古い場合、ATOは力行を出さず、設定したサービスブレーキで停止を要求する。非常ブレーキの判断は保安系へ残す。

現在の `TimsNotchOutput.resolvedBrakeStep` は、移行時には概ね `max(manualBrakeStep, atoBrakeStep, atcBrakeStep)` になる。ただし単純なフィールド追加だけで終わらせず、どの系統が指令権を持つかと `reason` を同時に出す。テストとTIMS表示で原因を追えるようにする。

## 5. 制御ロジック

### 5.1 TASC MVP

最初の実装は以下の順で十分である。

1. 進行方向に沿った停止目標までの符号付き残距離を求める。
2. 残距離と基準減速度から `v_ref`、現在速度から `a_req` を求める。
3. 速度がパターンを上回る場合は要求減速度を増やし、下回る場合は弱める。
4. 実減速度が要求値より不足していれば補正を加える。
5. `TimsBrakeCalculator.TryGetNearestBrakeStep` で減速度をブレーキステップへ量子化する。
6. ヒステリシスと最小保持時間を通して最終指令にする。
7. 低速かつ許容停止域内に入ったら停止保持ステップへ移る。

MVPの目標減速度例：

```text
speedError = max(0, speedMps - targetSpeedMps)
decelError = targetDecelerationMps2 - measuredDecelerationMps2

commandedDeceleration = clamp(
    requiredDecelerationMps2
      + speedKp * speedError
      + decelKi * integral(decelError),
    0,
    maximumServiceDecelerationMps2)
```

これは実装開始用の提案式であり、確認した外部装置の専有アルゴリズムを再現したものではない。ここで `measuredDecelerationMps2 = max(-filteredAccelerationMps2, 0)` として、減速度を正値に揃える。積分値には上限を設け、指令が飽和中またはブレーキが利用不能なときは積分を止める。実減速度は速度差分をそのまま使わず、固定周期で計算した後に低域通過フィルターを通す。

勾配は、線路方向を正としたときの符号を一か所で定義する。下り勾配では必要ブレーキを増やし、上りでは減らせる。ただし停止直前の保持や後退防止は勾配補正とは別に扱う。

### 5.2 停止パターンの発展

一定減速度式で基本動作を確認した後、経路を停止位置から逆向きに積分して速度パターン表を作る。

```text
停止位置 v = 0
  ↓ 経路を後方へ小区間ずつ走査
各区間で、サービスブレーキ性能、勾配、走行抵抗、速度制限、
ブレーキ応答余裕を使って一つ手前の許容速度を計算
  ↓
位置 → 目標速度のルックアップ表
```

この表は毎フレーム生成せず、停止目標または経路が変わったときに作る。経路上の勾配は `GuideLineSample.GradientPermille` を利用できる。分岐を跨ぐ経路が未実装の間は、同じGuideLine内の停止目標だけをサポート対象とする。

### 5.3 ATO駅間制御

ATOの運転目標速度は、少なくとも次の最小値とする。

```text
targetSpeed = min(
  routeSpeedLimit,
  permittedSpeed - atcSpeedMargin,
  tascPatternSpeed,       # 次停止目標がない区間では+Infinity
  temporarySpeedLimit,
  scheduleOrEcoProfileSpeed)
```

MVPでは最後の定時・省エネ速度を省略できる。目標速度との差から次の3択を行う。

- 十分低い：力行。ただしBC解放と出発条件を確認する。
- デッドバンド内：惰行または弱い定速制御。
- 高い：サービスブレーキ。

PowerとBrakeを細かく往復しないよう、Coastを積極的に使い、相ごとに遷移条件と最小滞在時間を持つ。停止目標が有効で、TASCパターンが他の速度目標以下になったら `Approach` へ入る。停車後は、位置許容、速度閾値、保持時間をすべて満たして `Stopped` とする。

### 5.4 停止判定とインチング

停止判定を `speed == 0` だけにしない。

```text
abs(speedMps) <= stoppedSpeedThreshold
abs(stopPositionErrorM) <= stopPositionTolerance
上記が stoppedConfirmationSeconds 継続
```

ショート（手前停止）またはオーバーラン時のインチングは初期TASCに含めない。通常停止ロジックと別の低速移動モードとして設計し、後方インチングを許す条件、ドア禁止、ATC・終端防護との関係を明示してから追加する。実設備にも停止域への位置合わせを行うインチング機能の例があるが、通常停止失敗を隠す自動リトライとして最初から有効にしない。

## 6. 異常時と安全側の振る舞い

このゲーム内実装でも、異常入力を0や前回値として黙って処理しない。

| 異常 | ATO/TASCの出力 |
| --- | --- |
| 次停止目標なし | 力行禁止。Standbyまたは制御された停止 |
| 停止目標が進行方向後方 | Fault。自動で逆転しない |
| 位置または速度がNaN / Infinity | Fault。力行0、停止要求 |
| 位置更新がタイムアウト | Fault。力行0、停止要求 |
| 地上子順序が不正・同じIDを逆順通過 | 位置を即座に飛ばさずFaultまたは補正拒否 |
| ATC許容速度が低下 | ATO目標よりATCを優先。必要ならATCブレーキ |
| 実減速度が指令後も不足 | サービスブレーキを段階的に増加。限界超過は保安系へ通知 |
| 逆走検知 | 力行0、停止要求、Fault |
| 手動操作 | ATO解除またはStandby。採用ルールを画面へ表示 |

自動運転では、信号線の意味、実際の進行方向、地上へ返す状態が一致しているかを独立に検証する必要がある。横浜シーサイドライン事故の調査では、モーター制御への入力とは別の信号から進行方向を認識し、逆走を検知できなかったことや、安全要件の抽出・検証不足が指摘された。[運輸安全委員会 事故概要](https://jtsb.mlit.go.jp/jtsb/railway/detail.php?id=1952)

そのためNakatetsuでは、`reverserPosition`、ATOが要求した方向、経路上速度の符号、実際の変位方向を比較するテストを設ける。表示用状態を安全判定の唯一の入力にしない。

## 7. 実装順序

### Phase 0：契約と最小データ

- `AutomaticOperation` のContext、状態enum、設定、出力理由を追加する。
- 停止目標を「経路ID + 経路上距離 + 進行方向 + stopTargetId」で表す。
- 既存TIMSへATO指令を入れる前に、指令優先順位を単体テストで固定する。
- `TimsSettingsAsset` のkm/h/s表現は接続時にm/s²へ変換し、ATOロジック内はSI単位へ統一する。

### Phase 1：純粋C#のTASC

- 一定減速度の位置―速度パターンを実装する。
- 残距離、速度、実減速度から連続ブレーキステップを返す。
- 固定周期、ヒステリシス、指令保持、停止保持を実装する。
- 車両やシーンなしでEditModeテストを通す。

### Phase 2：TIMS・車両運動への接続

- `manual`、`ato`、`atc` を別入力として `TimsNotchLogic` または専用Arbiterで統合する。
- `TimsBrakeLogic` が使う `brakeStep` へ採用済み指令を渡す。
- 実速度・実減速度・BC圧をATOへ戻す。
- TIMS表示へモード、相、目標速度、残距離、指令元、停止誤差を出す。

### Phase 3：駅・地上子・位置補正

- Trackへ運転上の地点データを置く。見た目のGameObject名を停止目標IDにしない。
- 編成・方向ごとの停止目標を定義する。
- 地上子通過イベントで推定位置を補正する。
- StationStop側で停車成功、ショート、オーバーランを判定する。

### Phase 4：駅間ATO

- Ready、Departing、Power、Coast、SpeedControl、Approach、FinalBrake、Stoppedを実装する。
- 出発インターロックと手動介入を実装する。
- 路線速度、ATC許容速度、TASCパターンの最小値へ追従する。
- 運行時刻と省エネ制御は基本走行の安定後に追加する。

### Phase 5：精度・外乱モデル

- 勾配を含む逆向き積分パターンへ置き換える。
- 荷重、回生失効、空制応答遅れ、粘着低下、位置誤差をシナリオ化する。
- 必要なら駅・車種ごとの学習補正を追加する。ただし学習値がなくても安全側に停止できる基準制御を残す。

## 8. テストと完了条件

### 8.1 単体テスト

- `v_ref` が残距離の減少に対して単調に下がり、停止点で0になる。
- 0m/s、0m、負距離、NaN、Infinityを決めた結果へ処理する。
- 同じ減速度差でノッチが毎周期往復しない。
- 制御周期へ異なる描画フレーム列を重ねても指令履歴が一致する。
- 手動、ATO、ATC、非常の全組合せで優先順位が一致する。
- 停止目標が後方になった場合に逆向きの力行を出さない。
- 地上子補正の前後で残距離と状態遷移が破綻しない。

### 8.2 シナリオテスト

| シナリオ | 変化させるもの | 確認値 |
| --- | --- | --- |
| 標準停止 | 初速、進入位置 | 停止誤差、最大減速度、最大ジャーク |
| 勾配 | 上り・平坦・下り | 停止誤差、ノッチ履歴 |
| 荷重 | 空車・満車 | 実減速度追従、停止誤差 |
| ブレーキ低下 | 回生失効、空制遅れ | 補正開始時間、オーバーラン |
| 位置誤差 | オドメトリ偏差、地上子欠落 | 補正量、Fault遷移 |
| ATC介入 | Approach中の許容速度低下 | 指令優先順位、力行遮断 |
| 操作介入 | 手動ブレーキ・非常 | ATO解除、表示理由 |
| 低FPS | 物理・描画周期の揺れ | 決定性、停止誤差 |

初期のゲーム上の合格基準は、特定実路線の数値を推測して固定せず、コンテンツ設定として定める。少なくとも停止許容内、オーバーランなし、最大サービス減速度以内、最大ジャーク以内、ATC上限超過なしを同時に満たすことを完了条件にする。平均誤差だけでなく、最悪値と失敗件数を記録する。

## 9. 最初の実装PRで行う範囲

次のコードPRはPhase 0とPhase 1に限定するのがよい。

- `AtoContext` とTASC用の状態・設定・出力。
- 一定減速度の `TascPatternCalculator`。
- 固定周期、ヒステリシス、最小保持時間を持つTASCロジック。
- EditMode単体テスト。
- Unityオブジェクトへ接続しない小さな使用例。

この段階ではTIMS本体、シーン、駅データ、地上子Prefabを変更しない。純粋ロジックの指令列と停止結果を確定してから、Phase 2の接続PRで `TimsNotchLogic` の入力契約を変更する。これにより、停止制御の問題と既存車両接続の問題を分けて検証できる。

## 10. 参照資料と確認範囲

- [国土交通省 中部運輸局「主な鉄道用語」](https://wwwtb.mlit.go.jp/chubu/tetsudou/yougo.html)：ATC、ATOの役割。
- [東京都「ホームドア整備に関する現状・課題等」](https://www.toshiseibi.metro.tokyo.lg.jp/documents/d/toshiseibi/pdf_bunyabetsu_machizukuri_bfree_pdf_tetsudo_bfree_11)：TASC、地上子・車上子、ATOとの関係。
- [電気設備学会誌「日暮里・舎人ライナーの自動運転制御」](https://www.jstage.jst.go.jp/article/ieiej/35/8/35_588/_pdf)：地上子、定位置停止、駅間速度制御、停車・出発条件、遠隔介入の実例。
- [鉄道総研「減速度フィードバックの機能追加によるブレーキ距離精度の向上」](https://www.rtri.or.jp/rd/news/vehicle/vehicle_202104.html)：距離基準減速度制御と実減速度フィードバック。
- [木村彰「電気車の定位置停止制御システムのための加速度変化率を制限した非線形速度制御」](https://www.jstage.jst.go.jp/article/ieejias1987/120/12/120_12_1484/_pdf/-char/ja)：位置―速度パターン、低速安定性、加速度変化率制限。
- [IEC 62290-1:2025](https://webstore.iec.ch/en/publication/83773)：都市鉄道運行・指令制御システムとGoAの範囲。
- [運輸安全委員会 事故概要](https://jtsb.mlit.go.jp/jtsb/railway/detail.php?id=1952)：進行方向検知、安全要件抽出、検証の教訓。

外部資料の数値は方式を理解するための実例であり、Series1000やNT線の仕様値とはみなしていない。公開資料から実装詳細を確認できない箇所は、Nakatetsu向けの提案であることを本文中に明示した。外部コードのコピーは行っていない。
