# 連動装置の全体処理仕様

2026-10-07の定義見直しとリファクタリング後の実装を記述する。2026-10-09にStationへ移設し、駅側の型名を`TrackStationInterlocking`へ変更した。処理内容は変更していない。連動装置は車上ATC・地上ATCと同じく、Input・Settings・工程別State・Logic・Outputで構成する。

基本ルールは[実装ルール](../../Architecture/ImplementationRules.md)と[機器Logicの分割と実装ルール](../../Architecture/EquipmentLogicImplementationRules.md)に従う。地上ATCの処理は[地上ATC全体処理仕様](../Atc/TrackAtcProcessingFlow.md)、車上側は[車上ATC全体処理仕様](../../Train/Atc/TrainAtcProcessingFlow.md)を参照する。

## 1. 構成と更新担当

名前空間は`Nakatetsu.Track.Interlocking`。編集用の[TrackStationInterlockingAsset](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/TrackStationInterlockingAsset.cs)と[Controller](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/TrackStationInterlockingController.cs)を維持し、Definition・Context・Logicは`Assets/Nakatetsu/Track/Interlocking/Station/Scripts`へ置く。

| 要素 | 内容・担当 |
| --- | --- |
| Input | Controllerが収集した軌道回路の在線値と転轍機の実位置・転換中フラグ |
| Settings | 初期化時にDefinitionを値コピーした進路・設備・時素の設定 |
| State | 全体状態と各工程の内部状態 |
| Output | 公開進路状態、通過履歴、転換要求をコピーした読み取り専用スナップショット |
| 親Logic | 外部API・tickの入口、工程の呼出順、全体正常性と公開世代の集約 |
| 子Logic | 自分の工程Stateだけを初期化・登録・更新・削除 |
| OutputLogic | 確定済みStateから新しいOutputを生成 |

[TrackStationInterlockingContext](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/Context/TrackStationInterlockingContext.cs)を全工程で共有する。子Logic同士は呼び合わず、Controllerは親[TrackStationInterlockingLogic](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/Logic/TrackStationInterlockingLogic.cs)を呼ぶ。API処理もtick処理も、同じStateの所有者を通す。

| State | Logic | 保持する情報 |
| --- | --- | --- |
| `validation` | `TrackStationInterlockingValidationLogic` | 定義の有効性、時間入力の有効性、進路ごとの入力取得可否 |
| `passage` | `TrackStationInterlockingPassageLogic` | 回路別の通過履歴、当該予約への進入済みラッチ |
| `approachLock` | `TrackStationInterlockingApproachLockLogic` | 接近鎖錠と取消時素の残り時間 |
| `routeLock` | `TrackStationInterlockingRouteLockLogic` | 本進路鎖錠、取消要求、解錠理由 |
| `overrunProtection` | `TrackStationInterlockingOverrunProtectionLogic` | 選択方式、防護phase、残り時間 |
| `reservation` | `TrackStationInterlockingReservationLogic` | 回路・転轍機の予約者、保持理由、要求位置 |
| `signal` | `TrackStationInterlockingSignalLogic` | 公開可否、開通、新規進入許可、転換要求 |

工程の予約レコードは`routeId`で管理する。全保持が終了して削除された後の再設定では、新しいレコードを登録し、前の通過履歴・時素を引き継がない。

## 2. Definition

[全体定義](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/Definition/TrackStationInterlockingDefinition.cs)は`interlockingId`、`memberTrackCircuitIds`、`memberConnectionIds`、`routes`、`turnoutLocks`を持つ。SettingsはDefinitionのリストと子レコードをコピーし、実行中のAsset編集を取り込まない。

[進路定義](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/Definition/TrackStationInterlockingRouteDefinition.cs)の役割は次のとおり。

| 項目 | 役割 |
| --- | --- |
| `routeId` | 全体で一意の進路ID |
| `startTrackCircuitId`・`destinationTrackCircuitId` | 進路の発点・着点 |
| `requiredTurnouts` | 防護方式を問わず本進路で保持する転轍機と要求位置 |
| `routeClearTrackCircuitIds` | 設定時の空き照査、新規進入許可、本進路の回路予約 |
| `routeReleaseTrackCircuitIds` | 後端通過による本進路解錠の対象。設定照査回路の部分集合で、空にしない |
| `approachLock.trackCircuitIds` | 取消時の接近照査回路 |
| `approachLock.releaseSeconds` | 接近あり・取得不能時の取消時素[s] |
| `conflictRouteIds` | 設定を同時に保持できない進路ID |
| `overrunProtection` | Normalで追加保持する転轍機と本進路解錠後の保持時間 |

例えば場内進路の設定照査を`[21T, 1RT]`、解錠照査を`[21T]`にする。設定時は入口区間と到着ホームの空きを要求するが、列車後端が21Tを抜ければ、1RTに停車中でも本進路を解錠できる。

[過走防護定義](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/Definition/TrackStationInterlockingOverrunProtectionDefinition.cs)は`isEnabled`、`turnoutRequirements`、`releaseSeconds`の3項目。Normal・Restricted共通で必要な転轍機は本進路の`requiredTurnouts`へ入れる。防護専用の軌道回路、到着トリガ回路、`common`・`normalAdditional`は設けない。

[轍査鎖錠定義](../../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/Definition/TrackStationInterlockingTurnoutLockDefinition.cs)は`connectionId`と`trackCircuitIds`を持つ。対象回路の在線・取得不能時はその転轍機の転換要求を抑止する。本進路・防護の解錠を永久停止するラッチには使わない。

初期化で参照ID、進路IDの重複、転轍機要求位置、轍査鎖錠の参照、解錠対象の部分集合、有限かつ非負の時素を検証する。本進路と防護が同じ転轍機へ異なる位置を要求する定義は受け付けない。

## 3. 外部API

Controllerは操作時・tick時に入力を収集し、親Logicを呼ぶ。入力は`Input.hasCircuitSource`、`hasConnectionSource`、`OccupiedByCircuitId`、`ConnectionsById`へ値としてコピーする。供給元やDictionaryのキーがない状態を空き・確定位置に置き換えない。

| API | 処理と成功の意味 |
| --- | --- |
| `TryInitialize` | Definitionをコピー・検証し、各工程を初期化してOutputを公開する |
| `TryRequestRoute` | 設備競合と現在入力を照査し、本進路と選択した防護設備を予約する |
| `TryCancelRoute` | 取消を記録し、必要なら接近鎖錠を開始して新規進入許可を撤回する |
| `TryGetRouteStatus` | 公開済みOutputの進路状態を返す |
| `TryGetCircuitPassage` | 設定済み進路の公開済み通過履歴を返す |
| `CanRequestTurnoutPosition` | 現在入力・予約・轍査鎖錠から対象転轍機の転換可否を照会する |

設定成功は予約受付までを意味する。新規予約の`PathEstablished`・`ProceedAllowed`はfalseで、開通確認と転換要求の生成は次tickで行う。

操作APIでは通過観測・解錠・時素減算を行わない。入力収集後の設定要求と予約済み進路の初回取消では、設定拒否や回路供給元を取得できない取消失敗でも、全定義進路の公開値を現在入力に従って更新する。この更新は前の開通・許可・転換要求を撤回できるが、新しく引き上げない。

同じ予約への再取消は成功を返し、時素・Outputを更新し直さない。進路の保持レコードが残る間は再初期化を拒否する。設定待ちキューや自動再要求は持たない。

## 4. 毎tickの処理順

`Controller.Calculate(deltaTimeSeconds)`で入力を収集し、親Logicは次の順で処理する。各工程で全進路を更新してから、次の工程へ進む。

| 順番 | 呼出し | 更新内容 |
| --- | --- | --- |
| 1 | `ValidationLogic.Update` | 時間入力・進路ごとの入力取得可否 |
| 2 | `PassageLogic.Update` | 本進路鎖錠中の通過履歴 |
| 3 | `ApproachLockLogic.Update` | 接近時素の減算・接近鎖錠の解除 |
| 4 | `RouteLockLogic.Update` | 本進路の通過解錠・未進入取消解錠 |
| 5 | `OverrunProtectionLogic.Update` | 防護成立、本進路解錠後の時素開始・減算・解放 |
| 6 | `ReservationLogic.Update` | 終了した保持理由の除去 |
| 7 | 親Logicから各ownerの削除入口 | 本進路・接近・防護・設備保持がすべて終了した予約の削除 |
| 8 | `SignalLogic.Update` | 開通、新規進入許可、転換要求 |
| 9 | 親Logicの`Publish` → `OutputLogic.UpdateOutput` | 全体正常性・`stateRevision`の更新とOutput生成 |

経過時間が負・NaN・無限大なら、工程2〜7を進めない。入力検証・Signal・Outputは更新し、前の許可を放置しない。正常な転換待ち、在線による停止、取消、予約競合はそれ自体では入力取得不能を意味しない。

## 5. 本進路と取消

Passageは設定照査回路ごとに`NotEntered → Occupied → Passed`を観測する。一度でも在線すれば`hasEntered`を保持する。入力欠損では通過を生成せず、本進路解錠後は履歴を凍結する。

本進路は接近鎖錠がなく、解錠照査回路がすべて既知の空きかつ`Passed`になったとき、通過理由で一括解錠する。設定照査回路全体の通過は要求しない。

未進入取消では次の条件を待って本進路を解錠する。

- 接近鎖錠なし。
- 設定照査回路がすべて既知の空き。
- 本進路の転轍機がNormalまたはReverseの確定位置で停止。

取消時に接近回路が在線または取得不能なら、接近鎖錠と満額の時素を設定する。接近鎖錠は有効なtick時間で減算し、0秒で解除する。初回取消で回路供給元そのものがない場合は、取消を受け付けず予約を保持する。

未進入取消では、転轍機を要求位置へ転換し直さない。進入後の取消では、本進路の後端通過を待つ。いずれも取消受付直後から新規進入許可・未送信転換要求を撤回する。

## 6. 過走防護

| 方式 | 選択条件・保持設備 |
| --- | --- |
| `None` | 防護定義がnullまたは`isEnabled=false`。本進路だけを予約 |
| `Normal` | 本進路と防護用転轍機の要求位置を確保できる。防護用転轍機を追加予約 |
| `Restricted` | 本進路は確保できるが、防護用転轍機が他の保持と位置競合する。防護用転轍機を追加予約しない |

必要転轍機の入力欠損をRestrictedへの切替で補わない。本進路設備の競合は設定要求を拒否する。選択方式はその予約の間固定し、設備が空いても自動昇格・降格しない。

Normalは`Setting`で登録する。未進入・非取消・本進路鎖錠中で設定照査回路が空き、防護用転轍機が要求位置で停止すると`Established`になる。

本進路が通過理由で解錠されたtickに、保持中の防護を`ReleaseTiming`へ移し、`releaseSeconds`を満額で設定する。開始tickでは減算せず、次tickから減算する。防護がまだSettingでも、予期しない進入後に本進路が解錠された場合は同じ時素を開始する。

時素満了後、本進路解錠済み・接近鎖錠なし・防護用転轍機が要求位置で停止なら`Released`にする。転換中・位置不一致・転轍機入力欠損では保持を続け、正常に戻ったら満了済みの条件を再照査する。時素は再開始しない。`releaseSeconds=0`なら、設備照査を通れば本進路解錠と同tickで解放する。

未進入取消では追加の防護時素を課さない。本進路・接近鎖錠の解除と防護用転轍機の確定位置での停止を待ち、要求位置との一致は要求しない。進入後取消は通過解錠後の通常時素を使う。

過走防護の回路予約・専用回路の空き照査・成立後在線による永久保持は行わない。到着・停車をタイマー開始条件にしない。本進路解錠後に列車が低速走行・途中停止しても、シミュレーション時間に従って防護時素を進める。

## 7. 設備保持・開通・転換

Reservationは設備ごとに進路ID・保持理由を持つ。本進路の回路予約は`routeClearTrackCircuitIds`から作る。本進路用転轍機は本進路・接近鎖錠の終了まで、防護用転轍機は防護終了まで保持する。同じ位置を要求する転轍機は共有でき、異なる位置の要求は競合する。

明示的な`conflictRouteIds`は双方を照査し、既存進路の本進路・接近鎖錠が残る間は拒否する。防護だけが残る状態では、本進路設備を解放済みとして扱い、防護用転轍機の保持だけを競合照査に残す。

`PathEstablished`は本進路鎖錠中で必要転轍機が要求位置で停止し、Normalなら防護も成立していることを表す。本進路へ進入しても、正常な在線だけを理由に開通状態を落とさない。

`ProceedAllowed`は新規進入許可であり、開通に加えて未進入・非取消・設定照査回路の空きを要求する。同じ予約への進入後は再点灯しない。取消中の物理的な開通状態が残っても、進行許可はfalseとする。

転換要求は本進路鎖錠中・未進入・非取消・設定照査回路が空きのときに作る。転轍機が転換中・すでに要求位置・未確定位置なら要求しない。対象転轍機の轍査鎖錠回路が在線・不明なら転換を抑止する。

Controllerの`ApplyOutput`は公開済み要求を読み、反映直前に最新入力で転換可否を再照査してConnection APIを呼ぶ。内部のConnection Stateを直接更新しない。操作APIで公開世代が変わった要求は反映しない。

## 8. 公開値と地上ATC

Outputは全定義進路の`RoutesById`、`TurnoutCommands`、`StateRevision`を持つ。内部StateのDictionary・可変レコードを共有せず、次回更新では新しいスナップショットへ差し替える。呼出側が保持した過去のOutput・進路状態・通過履歴は後続tickで変わらない。

`TryGetRouteStatus`は既知の正常な未設定も成功し、`IsRouteSet=false`を返す。未知の進路・未初期化・当該入力を取得不能な公開値は失敗する。必要な設定照査回路の欠損、必要転轍機の欠損・停止時の不確定位置、不正時間では公開値をUnavailableとし、許可を抑止する。既知の転換中は通常の待ちとして扱える。

公開進路状態は設定有無、本進路鎖錠、取消、接近鎖錠と残り時間、開通、進行許可、防護方式・phase・残り時間、回路別通過履歴を持つ。`TryGetCircuitPassage`は設定済みかつ公開可能で、対象回路の履歴がある場合だけ成功する。

地上ATCは旧実装を削除し、旧`NewAtc`を`Nakatetsu.Track.Atc`へ統合した。[TrackAtcController](../../../Assets/Nakatetsu/Track/Atc/Scripts/TrackAtcController.cs)は連動の公開APIから値をコピーする。

| 連動の公開状態 | 地上ATCへの入力・扱い |
| --- | --- |
| 確認済み未設定 | キーを登録し、`IsRouteSet=false`。正常な停止電文 |
| 設定受付・転換待ち | キーを登録し、未開通・進行不可 |
| 開通・未進入 | 本進路鎖錠・開通・進行許可・防護方式を登録 |
| 進入後 | 開通・本進路鎖錠は維持し、新規進入許可はfalse |
| 取消中 | `CancelPending=true`・進行不可。ATC経路へ使用しない |
| 本進路解錠後、防護だけ保持 | `IsRouteSet=true`でも`RouteLocked=false`・未開通。正常な停止として扱い、防護方式Noneを使う |
| 入力取得不能・未初期化 | キーを登録しない。確認済み未設定に置き換えない |

列車の停止限界・速度パターンは地上ATC・車上ATC側の担当とし、連動に車両の停車判定やドア入力を持たせない。
