# 連動管理部の設計

2026-10-08時点の設計方針。複数の駅連動装置を連動管理部に登録し、進路要求と状態取得の窓口を集約する。最初の実装単位は、連動管理部の実装と、地上ATC・シミュレーション進行の接続変更とする。

全体構成は[中鉄運行管理システム構成図](../../References/中鉄運行管理システム構成図（仮）.drawio)に従う。各駅の連動処理は[連動装置の全体処理仕様](TrackInterlockingProcessingFlow.md)、地上ATCの処理は[地上ATC全体処理仕様](../Atc/TrackAtcProcessingFlow.md)を参照する。

## 役割分担

| 装置 | 担当 |
| --- | --- |
| PRC | 列車追跡、ダイヤ照合、自動進路要求の判断 |
| CTC中央装置 | 在線・設備情報の集約、監視・手動操作、要求と結果の中継 |
| 連動管理部 | 駅連動装置の登録、対象駅への要求の振り分け、進路状態の集約、各駅の更新の取りまとめ |
| 各駅連動装置 | 安全条件・設備競合の照査、予約・鎖錠・解錠、転轍機への転換要求と実位置・転換状態の照査 |
| 地上ATC | 在線情報と進路状態などを使用したATC電文の生成 |

安全判断と予約・鎖錠の状態は各駅連動装置が所有する。連動管理部は、各駅の操作APIと公開状態を通して要求・照会を取り次ぐ。

## ファイル構成と名前

`Interlocking`直下を`Management`と`Station`に分ける。`Management`は複数駅の集約・管理、`Station`は各駅の連動処理を担当し、それぞれの配下に`Scripts`を持たせる。このディレクトリ構成を確定方針とする。

2026-10-09に既存の駅連動処理を`Assets/Nakatetsu/Track/Interlocking/Station/Scripts`へ移設し、`TrackInterlocking`で始まる駅連動の型名を`TrackStationInterlocking`へ変更した。処理内容は変更していない。

| 既存の名前 | 移設後の名前 |
| --- | --- |
| `TrackInterlockingController` | `TrackStationInterlockingController` |
| `TrackInterlockingAsset` | `TrackStationInterlockingAsset` |
| `TrackInterlockingLogic` | `TrackStationInterlockingLogic` |
| `TrackInterlockingContext` | `TrackStationInterlockingContext` |

駅側のDefinition・Input・Settings・State・Outputと各工程のLogic、DebugDisplay・テスト型も、同じプレフィックスへそろえた。Testsは`Station/Tests`、既存の実行用asmdefは`Station`直下へ移設した。namespaceとassembly名は`Nakatetsu.Track.Interlocking`を維持し、移設したファイルのUnity GUIDも保持する。

連動管理部は`Assets/Nakatetsu/Track/Interlocking/Management/Scripts`へ配置する。2026-10-09にInput・State・Output・Context、初期化・状態集約のLogicと、駅から入力を収集するControllerを最小構成で実装した。管理部の型名は`TrackInterlockingManagement`で始め、Controller名は`TrackInterlockingManagementController`。駅の更新・要求の振り分け、地上ATC・シミュレーションへの接続は後続作業とする。

ディレクトリ構成は次のとおり。

```text
Assets/Nakatetsu/Track/Interlocking/
├── Management/
│   └── Scripts/             連動管理部
└── Station/
    ├── Nakatetsu.Track.Interlocking.asmdef
    ├── Scripts/             各駅連動装置
    └── Tests/               駅連動・地上ATCの既存テスト
```

## Input

[TrackInterlockingManagementInput](../../../Assets/Nakatetsu/Track/Interlocking/Management/Scripts/Context/TrackInterlockingManagerInput.cs)は、`StationsById`に「駅ID → 連動装置情報」の辞書を持つ。各駅の連動装置情報は`RoutesById`に「進路ID → 進路情報」の辞書を持つ。

| 情報 | Inputの項目 |
| --- | --- |
| 駅連動装置の初期化状態・Output取得可否 | `IsInitialized`、`HasOutput` |
| 進路の情報取得可否 | `IsAvailable` |
| 進路構成状態 | `IsRouteSet`、`PathEstablished`、`ProceedAllowed`、`RouteLocked`、`CancelPending`、`ApproachLocked`、`ApproachReleaseRemainingSeconds` |
| 過走防護情報 | `OverrunMode`、`OverrunPhase`、`OverrunReleaseRemainingSeconds` |

入力収集時に各駅の公開状態から必要な値をコピーする。駅が未初期化、またはOutputを取得できない場合は、その駅の進路情報を利用しない。進路の`IsAvailable`は駅側の公開状態と同じ意味で、必要な設備情報の取得可否を表す。`IsAvailable = false`を確認済みの未設定に置き換えない。

軌道回路の在線値、転轍機の実位置・転換状態・転換指令、内部の設備予約・通過履歴、列車番号・ダイヤ、要求キューはInputに持たせない。Controller参照と各駅の更新順は登録側で管理する。

Management/Scriptsのasmrefは既存の`Nakatetsu.Track.Interlocking`を参照する。

### 駅からの入力収集

`TrackInterlockingManagementController`のInspectorに、駅IDと`TrackStationInterlockingController`参照の組を登録する。駅側の型には駅IDを追加せず、管理部が対応付けを所有する。`TryInitialize`で登録を確認し、空白の駅ID、重複した駅ID、未設定の参照、同じ連動装置の重複登録を拒否する。成功した登録は実行時の辞書として固定し、Inspectorの登録を変更した場合は再初期化する。駅の初期化完了は登録条件にしないため、Awakeの実行順には依存しない。

`CollectInput`は毎回`Input.StationsById`を空にして、登録済みの駅から情報を取り直す。駅の公開Outputを一度取得し、`TrackInterlockingManagementInputAdapter.CreateStationInput`で専用Inputへ値をコピーする。`TryGetRouteStatus`は`IsAvailable = false`の進路を返さないため使用せず、公開Outputの`RoutesById`から全状態を読む。

駅が未初期化、参照が破棄された、コンポーネントが無効、または公開Outputが取得できない場合は、正常取得の旗を立てず進路情報を空にする。その駅のInputは今回の取得結果を表し、集約LogicがOutputから駅IDごと除外する。取得失敗時に前回のInputやOutputを使い続けない。管理部自身が未初期化の場合はInputを空にする。

Controllerは既存の`ISimulationController`を実装する。`Calculate(deltaTimeSeconds)`で`CollectInput`→管理部Logicの`Calculate`を呼ぶ。集約結果は`Context.Output`から公開し、`ApplyOutput`で外部を更新しない。入力収集では駅の`Calculate`・`ApplyOutput`・進路操作APIを呼ばず、時素やStateRevisionを進めない。独自のUpdate/FixedUpdateも持たない。シミュレーションからの呼び出しとシーン上の参照設定は、接続作業で行う。

## State・Outputと最小の集約処理

`TrackInterlockingManagementState`は管理部自身の初期化状態`IsInitialized`だけを持つ。各駅の初期化状態とは区別し、駅の情報取得失敗で管理部自身を未初期化へ戻さない。

`TrackInterlockingManagementOutput.StationsById`は「駅ID → 連動装置情報」の読み取り専用辞書とする。各駅の情報は、駅の`IsInitialized`・`HasOutput`と、「進路ID → 進路情報」の読み取り専用辞書`RoutesById`を持つ。進路情報はInputの全項目を値としてコピーする。ContextがInput・State・Outputを所有し、Inputの入力収集は呼び出し側、Stateの初期化は親Logic、Outputの生成はOutputLogicが担当する。

処理の入口と順序は次のとおり。

1. `TrackInterlockingManagementLogic.Initialize(context)`で管理部を初期化済みにし、OutputLogicでOutputを空にする。再初期化でも前回Outputを消去し、Inputは保持する。
2. 管理部Controllerの`CollectInput`でInputに1ステップ分の駅情報を収集する。
3. `TrackInterlockingManagementLogic.Calculate(context)`からOutputLogicの`UpdateOutput`を呼び、入力スナップショットを集約する。

Controllerの再初期化では古い登録・Inputを消し、親Logicの`Reset`で管理部を未初期化にしてOutputも空にする。登録確認が失敗した場合はこの状態を維持し、成功した場合だけ`Initialize`して入力を収集する。

管理部が未初期化ならOutputは空にする。初期化済みなら、駅の`IsInitialized && HasOutput`を正常取得の条件として採用する。未初期化・Output取得失敗・nullの駅情報、空白の駅IDは採用しない。正常取得した駅は進路辞書が空でも含める。nullの進路情報や空白の進路IDは含めない。

進路の`IsAvailable`は駅Outputの取得可否ではなく、進路に必要な設備情報の取得可否を表す。そのため`false`も取得できた状態としてそのままコピーし、正常な未設定とは区別する。`IsRouteSet = false`や時素`0`、enumの`None`なども有効な状態として保持する。

Outputは毎回新しいスナップショットを生成し、今回取得できなかった駅の前回値を残さない。取得失敗した駅はキーごと含めず、登録済みの駅IDに対して「Outputにキーがない = 今回の取得失敗」として扱う。取得失敗を表すダミーの駅情報は作らない。Inputの辞書・駅情報・進路情報と参照を共有せず、次の入力収集や計算で過去の公開Outputが変わらないようにする。内部Outputとサーバー送受信型は分ける方針を維持する。

## ローカルサーバーとの連携

ローカルサーバーの実装言語はC#とする（2026-10-09決定）。送受信用の型はUnityに依存しない共通のC#データ型として定義し、ManagementのOutputとの対応を決める方針とする。

サーバー構成はASP.NET Core Minimal API、通信データ形式はJSONを候補とする。通信方式、共通型の共有方法、enum・取得不能・更新番号の通信上の表現は、Outputの設計時に確定する。

## 登録と要求の振り分け

連動管理部は、登録した駅連動装置から進路IDを収集し、次の対応表を持つ。

```text
進路ID → 担当する駅連動装置
```

進路IDは全駅を通じて一意とする。登録時に進路IDの重複を検出し、担当駅を曖昧にしない。

要求経路は次のとおり。PRCの自動要求とCTCの手動要求を、連動管理部の同じ窓口へ渡す。

```text
PRC → CTC中央装置 → 連動管理部 → 担当駅の連動装置
```

初版は要求を同期的に担当駅へ転送し、その場で受付・拒否の結果を返す。競合で拒否された要求の再要求は、PRCまたは手動操作側が判断する。

## 外部向けの窓口

| 窓口名 | 入力 | 処理 |
| --- | --- | --- |
| `TryRequestRoute` | 進路ID | 担当駅に進路設定要求を渡し、受付・拒否の結果を返す |
| `TryCancelRoute` | 進路ID | 担当駅に取消要求を渡し、受付・拒否の結果を返す |
| `TryGetRouteStatus` | 進路ID | 担当駅の公開進路状態を返す |

窓口名は既存の駅連動装置のAPIに合わせる。要求結果の共通型を使用する方針とし、具体的なメソッドシグネチャと型名は後続の設計で決める。

**設定要求の受付は、進路成立を意味しない。** 受付後の開通確認・転換処理は各駅のtick処理で進め、CTC・PRCは後続の進路状態で成立を確認する。取消要求の受付も、解錠完了とは区別する。

転換待ち・取消待ちは受付後の進路状態として扱い、要求の拒否理由と混同しない。

## 要求結果と理由コード

要求結果には次の情報を持たせる。CTC・PRCの処理分岐には固定のenumを使用し、説明文はログ・表示用とする。

| 情報 | 内容 |
| --- | --- |
| 受付結果 | `Accepted`または`Rejected` |
| 理由コード | 受付時は`None`、拒否時は拒否理由を示す固定コード |
| 関連するID | 対象進路IDと、原因になった競合進路・軌道回路・転轍機のID |

### 理由コード案

以下は現時点の分類案。コード名・数値・詳細条件の最終確定は、各駅連動装置の戻り値を共通型へ変更する設計時に行う。

| コード案 | 意味 |
| --- | --- |
| `None` | 要求を受付 |
| `InvalidRequest` | 要求形式が不正 |
| `UnknownRoute` | 未定義の進路 |
| `NotInitialized` | 要求の処理に必要な装置が未初期化 |
| `InputUnavailable` | 必要な設備情報を取得できない |
| `AlreadySet` | 設定対象の進路が設定済み |
| `NotSet` | 取消対象の進路が未設定 |
| `CircuitOccupied` | 設定照査に必要な軌道回路が在線 |
| `CircuitReserved` | 必要な軌道回路を他進路が予約中 |
| `RouteConflict` | 他進路と競合 |
| `TurnoutPositionConflict` | 転轍機の要求位置が他進路の保持位置と競合 |

現在の[連動Logic](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/Logic/TrackStationInterlockingLogic.cs)は`bool`とエラー文を返す。取消要求では未初期化・未知進路・未設定が同じエラー文になるため、共通型への変更時に区別する。軌道回路の在線と入力取得不能も区別して返す。

## 地上ATCへの接続変更

現在は[地上ATC Controller](../../../Assets/Nakatetsu/Track/Atc/Scripts/TrackAtcController.cs)が各駅連動装置の一覧を持ち、直接進路状態を集めている。集約後は連動管理部を参照し、登録進路の列挙と状態取得を行う。

地上ATCへ渡す既存の進路入力を維持する。

- `IsRouteSet`
- `ProceedAllowed`、`PathEstablished`
- `RouteLocked`、`CancelPending`
- `OverrunMode`

確認済みの未設定は`IsRouteSet = false`として渡す。状態取得不能・未初期化は入力未取得として扱い、正常な未設定に置き換えない。

軌道回路の在線情報と物理経路の接続情報は、既存の供給元から取得する。今回の接続変更では、連動進路の情報取得先を連動管理部へ集約する。

## シミュレーションの更新

現在は[ApplicationSimulationController](../../../Assets/Nakatetsu/Application/Simulation/Scripts/ApplicationSimulationController.cs)も駅連動装置の一覧を持ち、各駅を更新している。この更新窓口を連動管理部へ集約する。

```text
列車の更新
  → 軌道回路の在線更新
  → 連動管理部を通して各駅連動装置を更新
  → 地上ATCの計算・電文配信
  → tick数と世界時刻の進行
```

各駅は1tickに1回更新する。各駅の`Calculate`・`ApplyOutput`と、地上ATCまでの既存の処理順を維持する。初回tickの列車初期配置からの在線生成も維持する。

## 最初の実装単位

1. 駅連動装置の登録、進路IDの対応表、要求・状態取得の共通窓口を作る。
2. シミュレーション進行側の各駅更新を連動管理部へ接続する。
3. 地上ATCの進路状態取得先を連動管理部へ切り替える。
4. 複数駅を登録し、進路設定・取消、転轍機の転換、ATC電文が従来どおり動作することを確認する。

この単位を完成させてから、CTCの実装へ進む。

## 次に確定する項目

- 連動管理部と要求結果の具体的な型名・APIシグネチャ。
- 理由コードの最終名称・数値と、各拒否条件との対応。
- 同じ設定・取消要求を繰り返した場合の正式な応答規則。現行は設定済みへの再設定を拒否し、取消待ちへの再取消を受付として返す。
- 複数の拒否条件が成立する場合の返却優先順と、関連IDの選び方。
- 初期化・登録の失敗と、進路要求の拒否・状態照会の失敗を表す型の分け方。
- 連動管理部から各駅を更新する具体的な呼出方法。
