# CTCの実装順と完成目標

> 2026年10月15日：CTCサーバー再構築に向け、旧C#サーバー・共有通信パッケージ・UnityのCTC通信処理を削除した。以下は削除前の実装計画・検証記録であり、記載の通信/APIは現構成には存在しない。Unityの連動・ATC・世界tickは維持し、進路要求と結果の型はUnity内へ移した。

2026年10月11日更新。連動管理部の要求振り分け・駅更新・地上ATC接続、Unityからサーバーへの世界時刻・連動状態の一括送信・保存と、Web向けの一括取得APIまで実装した。初版の完成目標は、既存のNtLine試験線で設備状態を監視し、ブラウザーから進路を設定・取消して、Unityの連動・ATCへの反映を確認できることとする。

既存の[全体実装計画](../implementation-plan-2026-11-17.md)を具体化する。[連動管理部の設計](../Track/Interlocking/TrackInterlockingAggregationDesign.md)と[システム構成図](../References/中鉄運行管理システム構成図（仮）.drawio)に合わせ、CTCは設備監視・手動操作・要求中継、PRCは列車追跡・ダイヤ照合・自動進路判断を担当する。CTCに表示する列番・位置の対応は、PRCへ渡せる独立した追跡機能として作る。

## 現在の実装

| 対象 | 現状 | 次に必要な接続 |
| --- | --- | --- |
| 駅連動装置 | 設定・取消、競合照査、予約・鎖錠、接近鎖錠、過走防護、公開進路状態に加え、固定理由コードと関連設備IDを含む要求結果を返す | 通信経由の要求・結果との接続 |
| 連動管理部 | 駅IDとControllerの登録検証、要求窓口TryRouteRequest、駅更新の取りまとめ、公開状態の集約がある | 複数駅での進路ID重複検証と通信窓口への接続 |
| 世界tickと地上ATC | Applicationが管理部を更新し、ATCが管理部の公開進路状態を読む。NtLine Sceneも接続済み | 実Sceneで進路・転轍機・ATC電文を通した試運転 |
| 共通通信型 | Message/MessageHeader、WorldTimeInformation、InterlockingSnapshotをMessagePack対応済み。同じ完了Tickの時刻・連動をUnitySnapshotにまとめ、IUnityHubのUpdateSnapshotAsyncで送る。Web APIでも同じメッセージをJSONで取得できる | 在線・転轍機・列車の通信型、通信失効の表現 |
| Unity通信 | MagicOnion接続と自動再試行、Inspector指定間隔の定期送信がある。完了Tickの出力を保持し、LateUpdateで時刻と連動を1回送信する | 実Sceneで新しい一括送信を確認、表示用設備情報の追加 |
| 運行管理サーバー | ASP.NET Core・.NET 10。UnityHubで受信してUnitySnapshotStoreへ一括保存。CTCContext/Logic/Controllerを最小構成で追加し、共有Contextをlockで保護。CTCEndpointからControllerを通じてGET /api/ctc/snapshotを取得できる。未受信は503 | 通信失効判定、要求配送、結果保持、CTC画面 |
| 試験線と列車 | 保存済みNtLineに10001F・10002Fを世界tickへ登録済み | 列番・番線対応、列車別の在線回路、追跡状態の送信 |
| PRCとダイヤ | 専用の自動進路判断・試験ダイヤ・列番追跡は未実装 | 手動CTC完成後に追跡と試験ダイヤから実装する |

主な実装根拠は、[駅Controller](../../Assets/Nakatetsu/Track/Interlocking/Station/Scripts/TrackStationInterlockingController.cs)、[管理部Controller](../../Assets/Nakatetsu/Track/Interlocking/Management/Scripts/TrackInterlockingManagementController.cs)、[世界tick](../../Assets/Nakatetsu/Application/Simulation/Scripts/ApplicationSimulationController.cs)、[地上ATC Controller](../../Assets/Nakatetsu/Track/Atc/Scripts/TrackAtcController.cs)、[通信型](../../Shared/Contracts/Interlocking/Scripts)、[サーバー入口](../../Server/Nakatetsu.TrafficControl.Server/Program.cs)、[保存済みScene](../../Assets/Scenes/NtLine.unity)にある。

管理部は独自のUpdateを持たず、Applicationの世界tickからCalculateとApplyOutputを呼ぶ。管理部が各駅を更新して状態を集約し、その後に地上ATCが公開状態を読み取る。TryRouteRequestはStationIdで担当駅を選び、設定・取消の受付結果を返す。サーバーやCTC画面からこの窓口へ要求を配送する通信処理はまだない。

ServerとSharedの土台はPR #77でmainへ取り込み済み。連動管理部接続時の過去の検証では、関連EditMode 163件とApplication統合PlayMode 4件が成功した。EditModeで自動通知されないOnDisableはテスト側で明示実行し、PlayModeでは実際の無効化通知による公開状態の撤去を確認した。

10月11日の一括送信・保存の変更では、Server/ContractsのビルドとUnity 6000.4.0f1の実参照を使ったContracts/Applicationコンパイルが警告0・エラー0で成功した。実MagicOnion通信、MessagePack往復、深いコピー、並行保存・読み取りの整合も確認した。今回Unity EditorのPlayModeで上記既存テストを再実行したわけではない。詳細な変更箇所・保証範囲・検証内容は[同一Tickスナップショットの実装記録](UnitySnapshotSynchronization.md)を参照する。実Sceneの試運転は残確認とする。

## 最初に完成させる連動管理部

最初の実装単位は、CTCの手動要求を渡せる管理部と、そこを使う世界tick・地上ATCの一式とする。処理の安全判断は駅連動装置に残す。

1. 管理部ControllerにInterlockingRouteRequestを受け取り、InterlockingRouteRequestResultを返すTryRouteRequestを追加する。既存のregisteredStationsByIdでStationIdから担当駅を取得し、RouteIdをその駅へ渡す。空ID・不正な操作種別・未知駅・未初期化を拒否する。要求にStationIdがあるため、振り分け用の「進路ID→担当駅」対応表は追加しない。進路IDは駅内で一意とし、異なる駅の同名進路を許容する。参照・保存・状態取得では`(StationId, RouteId)`を使う。
2. Operationに応じて担当駅の設定・取消APIへ同期転送し、結果に同じRequestIdと対象IDを付けて返す。各駅のLogicから受付可否・固定理由コード・関連する進路／回路／転轍機IDを返すように改修する。管理部のLogicは値の検証と状態集約を担当し、駅ControllerのAPIを呼ぶ処理は管理部Controllerに置く。エラー文の解析で理由コードを決めない。管理部でContractsを直接使うため、Nakatetsu.Track.InterlockingのAssembly参照へNakatetsu.Contractsを追加する。
3. Applicationの駅配列による更新を管理部の更新入口へ置き換える。管理部が各駅のCalculateを1回ずつ実行し、ApplyOutputの段階で計算した駅へ1回ずつ適用して公開状態を集約する。従来の駅更新を同時に残さない。
4. 地上ATCの進路状態取得先を管理部へ切り替える。正常取得した未設定進路は入力に含め、駅欠落・IsAvailable=falseは入力未取得として扱う。在線と物理経路の供給元は既存のものを使う。
5. NtLine Sceneへ管理部を登録し、実際の進路・転轍機・ATC電文の一連の動作を確認する。

管理部のAwake時に駅の初期化が終わっているとは限らない。駅ID辞書は現在のTryInitializeで構築する。今後追加する進路一覧の重複検証は各駅の初期化後に行う。管理部のAwakeが先でも正常に登録・検証されることを確認する。内部状態の取得や通信型への変換では、駅の更新・時素進行を行わない。

この段階の合格条件は、管理部経由の設定・取消が従来の駅直接操作と同じ結果になり、各駅の更新が1tickに1回、ATC電文と接近鎖錠の時素が従来どおり進むこと。複数駅と重複進路IDの試験は小さなテスト用定義で行い、新しい駅の制作を前提にしない。

## 通信と更新順

UnityとサーバーはMagicOnionのStreamingHubとMessagePackで接続する。世界時刻と連動状態は1つのUnitySnapshotとして定期送信し、サーバーは1つのStoreに一括保存する。ブラウザー向けにはCTCEndpointのGET /api/ctc/snapshotから同じメッセージをJSONで一括取得できる。応答と確認手順は[Web取得APIの実装記録](CTCWebSnapshotApi.md)を参照。サーバーからUnityへの操作要求の配送はまだ未実装であり、以下は要求配送を追加する際の更新順とする。

```text
ブラウザーの手動操作
  → CTC中央装置の要求受付
  → Unityの通信受信と要求の保管
  → 世界tick内で連動管理部へ同期転送
  → 駅連動装置

完了Tickの時刻・連動状態（実装済み）
  → UnitySnapshotSenderで通信型へ同期コピー
  → MagicOnionで1回送信
  → UnitySnapshotStoreでメッセージ全体を保存
  → CTCの表示
```

軌道回路・転轍機・列車状態と、CTCの表示画面は今後追加する。Unity側はTick完了時の連動出力を保持し、LateUpdateで時刻と一緒にコピーする。Tick内で通信を待たず、送信周期ごとに最新の完了Tickを送る。サーバーで時刻と連動を使う際は、GetLatest()を1回呼んで返された同じメッセージから読む。

通信待ちで世界tickを止めない。通信で受けた要求はUnityのメインスレッドへ渡し、全列車の移動と在線確定の後、駅連動更新の前に適用する。管理部の要求転送自体は同期処理とし、競合要求の再要求判断は手動操作側または将来のPRCが行う。

```text
初回のみ初期配置の在線生成
  → 全列車更新
  → 全列車の在線確定
  → 受信済み要求の適用
  → 管理部を通した各駅更新と状態集約
  → 地上ATCの電文生成・配信
  → tick数・世界時刻の進行
  → 同じtickの設備・列車状態を通信へ公開
```

停止中は要求を保留し、再開またはStepOnceで適用する。通信と表示は停止中も動かし、同じtickの更新通知と停止状態を送る。接近鎖錠などの時素を通信周期で進めない。

最終的な通信契約には次を含める。現在実装済みなのは、SessionId・SchemaVersion・世界時間情報と、駅・進路の状態を一括で送信・保存する部分。通信鮮度、要求の配送・重複防止、設備定義一覧などは未実装である。

- シミュレーションのSessionId、SchemaVersion、TickCount、WorldTimeSeconds、停止状態。サーバー受信時刻による通信の鮮度と、シミュレーション時刻を分ける。
- RequestIdと要求対象のSessionId。同じID・同じ内容の再配送は1回だけ実行し、同じ結果を返す。同じIDで内容が違う要求と、以前のSessionIdの要求は拒否する。
- 「サーバーが受信」「Unityの処理待ち」「連動が受付／拒否」「結果未確認」の区別。受付後の開通・進行可・取消待ち・解錠は公開進路状態で確認する。
- 登録される駅・進路・回路・転轍機の定義一覧。取得不能の駅が最新スナップショットから消えても、画面はその設備を取得不能として表示する。
- 軌道回路の取得可否、在線、転轍機の実位置・転換中。現在の回路boolは位置取得失敗時の安全側占有も含むため、CTC向けには取得失敗と実際の在線を区別する情報を追加する。

Unity向け共通DTOにはMessagePackObject/Keyを付け、時刻・Dictionary・enumを含む実通信の往復を検証した。ブラウザー向けHTTPでは、未受信は503のproblem JSON、受信済みは200のMessage<UnitySnapshot>を返す。接続状態・通信失効の表現は今後追加する。

## 段階別の完成目標

| 段階 | 作る部分 | 合格条件 |
| --- | --- | --- |
| M0 | 連動管理部の要求振り分け・駅更新・ATC接続 | 管理部経由の設定・取消、競合拒否、鎖錠、ATC電文が従来と一致する。駅の二重更新がない |
| M1 | Unity通信、状態変換、サーバーの最新状態保持、設備一覧 | NtLineの実状態をブラウザーで読める。接続中・停止中・取得不能・情報失効を区別できる |
| M2 | 手動設定・取消、要求配送、結果表示、重複防止 | ブラウザーの要求が連動・ATCへ反映され、受付と成立を別に確認できる。取消待ちと解錠を追える |
| M3 | 列車情報送信、列車IDと列番の対応、番線・回路への対応、簡易配線表示 | 10001Fと10002Fを区別し、位置・在線回路・番線・仮列番を表示できる。2列車の競合操作でも取り違えがない |
| M4 | 試験ダイヤ、PRCの自動要求・待機・再要求・手動抑止 | 自動要求が同じ連動APIを通る。競合解消後に復帰し、手動取消した進路を自動で戻さない |

CTC初版の完成範囲はM0〜M3とする。M4の2列車自動運行は、NPC・停車・戸扱い・発車条件の接続を含む別の統合目標として判定する。

試験対象は[既存のNtLine](../Track/NtLine/NtLineTestTrack.md)とする。

- 1駅2線、4進路：21T-1RT、21T-2RT、1RT-22T、2RT-22T。
- 6回路：20T、21T、1RT、2RT、22T、23T。
- 転轍機21・22、2編成10001F・10002F。
- 初期表示は進路・回路の一覧と操作ボタン。M3で配線と列番表示を加える。

列番はTrainIdと分け、試験用の固定対応から開始する。追跡登録では空・重複TrainIdを拒否する。列車別の在線回路は各列車の占有区間と回路定義の重なりから求める。全列車を合成した回路boolから列車IDを推定しない。駅・番線・進路・回路の対応は試験線用の明示データにし、欠損・複数候補は不明として表示する。

## CTC初版の受入試験

1. 起動時に6回路・4進路・転轍機2基の実状態が表示される。未知の状態を空線・未設定へ置き換えない。
2. 空いている進路を設定し、RequestIdに対応した受付結果、転換待ち、開通・進行可、ATC電文の変化まで確認できる。
3. 競合進路と在線区間への設定を拒否し、固定理由コードと原因設備を表示する。転轍機へ直接操作する経路をCTCに作らない。
4. 取消時は進行許可が落ち、必要な接近鎖錠・取消待ち・残時素を表示する。受付直後に解錠済みとは表示しない。
5. 同一要求の再送でも操作を重ねず、取消時素を再始動しない。手動取消をCTC自身が自動再設定しない。
6. 10001F・10002Fの位置と個別在線を区別する。2列車が関係する競合操作でも、要求対象・表示列番・設備状態が混ざらない。
7. 一時停止中は要求保留を表示し、StepOnceまたは再開で適用する。停止中に時素が進まず、通常倍率・早送り・手動tickで同じ入力に対する結果が一致する。
8. 切断時は状態を失効表示にし、再接続時は全状態を取り直す。Unity再起動後に前のセッションの要求を再実行しない。

## 着手時の既存契約と回帰確認

現在の占有区間は車体端までではなく先頭前台車から最後尾後台車までで、走行経路の履歴は保存していない。[現行仕様](../Track/Simulation/TrainTrackSampling.md)と[Sampling Logic](../../Assets/Nakatetsu/Train/Simulation/TrackPosition/TrainTrackSamplingLogic.cs)に合わせて扱い、全体計画の「全長占有・通過履歴実装済み」をそのまま前提にしない。

試験線でCTCを始める際は、列車が分岐を跨いでいる間の転換を連動で防ぐ契約を維持し、両方向の後端通過まで轍査回路・鎖錠が保持されることを確認する。将来の回路区分変更・全線展開では、車体端までの保護余裕と経路保持方式を再評価する。

試験線・2列車への変更に追従していない既存テストもある。[TrainTrackTurnoutTests](../../Assets/Nakatetsu/Train/Simulation/TrackPosition/Tests/TrainTrackTurnoutTests.cs)は現在のNtLineにないpreview系Edge IDを参照し、[TrainBaselineTests](../../Assets/Nakatetsu/Train/Debug/Tests/TrainBaselineTests.cs)は列車Debug Controllerが1個であることを期待している。M0の回帰確認時に現行定義と対象TrainIdに合わせ、成功判定の根拠を回復する。

## 確認日と次の着手

既存計画の10月11日・10月18日は進捗判定日として使う。10月11日はM0の接続と通信契約を確認し、M1以降の残工数を見積もる。10月18日はM0〜M3の手動CTCと列車表示を優先して判定し、PRCによる2列車運行はM4とNPCの準備が整った場合に追加判定する。現在の未接続範囲から、10月11日の手動CTC完成や10月18日の自動運行完成を確約しない。

M0の要求窓口、駅側の結果構造化、駅更新・ATC入力先の切替と、M1のUnity通信・連動状態変換・サーバー最新状態保持・Web API取得まで実装済み。次は実Sceneから送信した情報のブラウザー確認、実状態の一覧画面と定期取得、通信の失効判定へ進む。M0の実Scene試運転と複数駅の進路ID重複検証、M1の設備定義・在線・転轍機表示も残っているため、M0/M1全体の合格とはまだ判定しない。全線制作・汎用ダイヤ編集・snapshot保存復元・案内設備・指令卓の作り込みは、この初版の合格後に進める。
