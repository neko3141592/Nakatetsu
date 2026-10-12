# Unityの同一Tickスナップショット送信・保存

更新日：2026年10月11日。世界時刻と連動状態を別々に送信・保存する実装を、同じ完了Tickの情報として一括送信・保存する実装へ変更した記録。

## 変更の目的

従来は`WorldTimeSender`と`InterlockingSnapshotSender`が別々のRPCを呼び、サーバーも`WorldTimeStore`と`InterlockingSnapshotStore`へ個別に保存していた。例えば時刻の受信だけ先に完了すると、サーバーから「Tick 101の時刻」と「Tick 100の連動状態」が取得される余地があった。

今回の変更では、Unityが`UnitySnapshot`に時刻と連動状態をまとめ、`UpdateSnapshotAsync()`で1回送信する。サーバーはメッセージ全体を1つのStoreへ置き換える。送信間隔とシミュレーションのTick幅は引き続き独立している。

```text
ApplicationSimulationController.ExecuteTick()
  列車 → 軌道回路 → 連動管理部 → 地上ATC
  → CompletedInterlockingOutputを保持
  → CompletedTickCountを加算

ServerMessageSenderController.LateUpdate()：送信間隔に達したとき
  → UnitySnapshotSenderが時刻と完了済み連動出力を同期コピー
  → Message<UnitySnapshot>を1回送信
  → UnityHub.UpdateSnapshotAsync()
  → UnitySnapshotStore.Update()で全体を一括保存

サーバー側の利用者
  → UnitySnapshotStore.GetLatest()を1回呼ぶ
  → 返された同じメッセージから時刻と連動状態を読む
```

サーバー自身のタイマーをUnityのTickと一致させる処理は追加していない。受信した`WorldTime.TickCount`が、そのメッセージ内の連動状態に対応する完了Tickを表す。

## ファイルごとの変更

| ファイル | 変更内容と理由 |
| --- | --- |
| [Shared/Contracts/Communication/Scripts/ToServer/UnitySnapshot.cs](../../Shared/Contracts/Communication/Scripts/ToServer/UnitySnapshot.cs) | 新規追加。`WorldTimeInformation`と`InterlockingSnapshot`を1つにまとめるMessagePack対応DTO。 |
| [Shared/Contracts/Communication/Scripts/ToServer/IUnityHub.cs](../../Shared/Contracts/Communication/Scripts/ToServer/IUnityHub.cs) | `UpdateWorldTimeAsync()`と`UpdateInterlockingSnapshotAsync()`を削除し、`UpdateSnapshotAsync(Message<UnitySnapshot>)`へ統一。別々の更新経路をなくした。 |
| [Assets/Nakatetsu/Application/Simulation/Scripts/ApplicationSimulationController.cs](../../Assets/Nakatetsu/Application/Simulation/Scripts/ApplicationSimulationController.cs) | 読み取り用の`CompletedInterlockingOutput`を追加。Tick完了時に連動管理部の不変な出力を保持し、続けてTick数を加算する。 |
| [Assets/Nakatetsu/Application/Communication/Shared/Scripts/UnitySnapshotSender.cs](../../Assets/Nakatetsu/Application/Communication/Shared/Scripts/UnitySnapshotSender.cs) | 旧2 Senderを統合。時刻と完了済み連動出力を同期コピーし、新RPCを1回呼ぶ。通信は親階層、Simulationはシーンから自動取得。 |
| [Assets/Nakatetsu/Application/Communication/Shared/Scripts/ServerMessageSenderController.cs](../../Assets/Nakatetsu/Application/Communication/Shared/Scripts/ServerMessageSenderController.cs) | 定期送信入口を`Update()`から`LateUpdate()`へ変更。通常のSimulationの`Update()`が終わった後に送信データを作る。 |
| [Assets/Scenes/NtLine.unity](../../Assets/Scenes/NtLine.unity) | 旧WorldTimeSenderのGameObjectとコンポーネントをUnitySnapshotSenderへ変更。旧InterlockingSnapshotSenderのGameObjectと親の子参照を削除し、Senderを1つにした。 |
| [Server/Nakatetsu.TrafficControl.Server/Communication/Unity/Scripts/UnityHub.cs](../../Server/Nakatetsu.TrafficControl.Server/Communication/Unity/Scripts/UnityHub.cs) | 注入するStoreと受信RPCを1つへ統一。受信ごとにTick・運行時刻・一時停止・駅数を出力する。 |
| [Server/Nakatetsu.TrafficControl.Server/Communication/Unity/Scripts/UnitySnapshotStore.cs](../../Server/Nakatetsu.TrafficControl.Server/Communication/Unity/Scripts/UnitySnapshotStore.cs) | 新規追加。ヘッダー・時刻・連動状態を含むメッセージ全体を、1つのロックで保存・取得する。ユーザーの配置指定に合わせて`Communication/Unity/Scripts`へ配置。 |
| [Server/Nakatetsu.TrafficControl.Server/Program.cs](../../Server/Nakatetsu.TrafficControl.Server/Program.cs) | `UnitySnapshotStore`をSingleton登録。旧2 Storeの登録を置き換えた。 |
| [Documentation/TrafficControl/CTCImplementationPlan.md](CTCImplementationPlan.md) | 現状、採用済み通信方式、M1の残作業を更新。今回の詳細記録へリンクした。 |

削除・置き換えた旧ファイル：

- `Assets/Nakatetsu/Application/Communication/World/Scripts/WorldTimeSender.cs`：新Senderへ置き換え。`.meta`を新Senderへ移動した。
- `Assets/Nakatetsu/Application/Communication/Interlocking/Scripts/InterlockingSnapshotSender.cs`：新Senderへコピー処理を統合し、スクリプトと`.meta`を削除した。
- 上記削除で空になったUnity側`Communication/World`・`Communication/Interlocking`と、そのフォルダーの`.meta`。
- `Server/Nakatetsu.TrafficControl.Server/World/Scripts/WorldTimeStore.cs`と`Interlocking/Scripts/InterlockingSnapshotStore.cs`：新Storeへ置き換えた。
- 新Storeの作成時の配置`World/Scripts/UnitySnapshotStore.cs`：追加指示により`Communication/Unity/Scripts/UnitySnapshotStore.cs`へ移動。名前空間も`Nakatetsu.TrafficControl.Server.Communication.Unity`へ統一した。

`UnityCommunicationController`の接続・切断・自動再試行処理、世界Tick内の更新順、既存の通信DTOのフィールドは変更していない。

## 通信型と送る値

既存の`Message<TPayload>`を使い、`TPayload`を`UnitySnapshot`に変更した。

```text
Message<UnitySnapshot>
├── Header
│   ├── SchemaVersion = 1
│   ├── SessionId = UnityCommunicationController.SessionId
│   └── MessageType = "UnitySnapshot"
└── Payload
    ├── WorldTime
    │   ├── TickCount
    │   ├── WorldTimeSeconds
    │   ├── TickDurationSeconds
    │   ├── IsPaused
    │   └── PlaybackSpeed
    └── Interlocking
        └── StationsById[StationId]
            ├── IsInitialized / HasOutput
            └── RoutesById[RouteId]
```

`UnitySnapshot`のMessagePackキーは`WorldTime = 0`、`Interlocking = 1`。ヘッダー・時刻・駅・進路の既存キーは維持している。

進路ごとにコピーする値は、`IsAvailable`、`IsRouteSet`、`PathEstablished`、`ProceedAllowed`、`RouteLocked`、`CancelPending`、`ApproachLocked`、`ApproachReleaseRemainingSeconds`、`OverrunMode`、`OverrunPhase`、`OverrunReleaseRemainingSeconds`。内部enumは従来と同じくContracts側enumへ変換する。

## Unity側で同じTickを保つ仕組み

### 完了済みの出力を保持する

連動管理部の現在の`Context.Output`は、初期化や無効化などTick外の処理でも差し替わる。そのためSenderがその時点の管理部を直接読むだけでは、`CompletedTickCount`が表すTickの出力とは限らない。

`ApplicationSimulationController`は列車・軌道回路・連動・ATCの処理が最後まで完了したときに、`interlockingManagement.Context.Output`を`CompletedInterlockingOutput`へ保持し、その直後に`CompletedTickCount`を加算する。内部出力は読み取り専用なので、以後管理部が別の出力へ差し替えても保持した内容は変化しない。この段階では通信DTOを作らず、通信処理も呼ばない。

まだTickを1回も完了していない場合は、`TickCount = 0`と明示的な空の連動出力を送る。初期化済み設備の状態をTick 0として推測して送る処理はない。サーバーの未受信`null`と、受信済みの空の連動状態は別の状態になる。

Tick途中で例外が発生して最後まで到達しなければ、新しい完了出力・完了Tick数は公開されない。

### 送信するデータはawait前に作る

`UnitySnapshotSender.SendAsync()`はメインスレッドで、時刻情報と保持された連動出力を新しいDTOへコピーする。このコピーの途中に`await`はないため、通常のUnityのSimulation更新が間に入ることはない。コピーを終えてから`GetConnectedHub().UpdateSnapshotAsync(message)`を1回呼ぶ。

送信中に次のTickが進んでも、送信中のDTOは新しいTickへ書き換わらない。`IsPaused`と`PlaybackSpeed`は送信時点の操作設定であり、停止・速度変更時には同じTick数でも変化し得る。時刻と連動出力は最後に完了したTickを表す。

### 定期送信はTickとは独立

`ServerMessageSenderController`は引き続き`IServerMessageSender`を子から自動収集し、`SendAllAsync()`で呼ぶ。保存済みNtLineには新Senderが1つだけある。

- Inspectorの`sendIntervalSeconds`で送信間隔を指定する。保存済みSceneは従来の0.1秒を維持。
- `LateUpdate()`で`Time.unscaledDeltaTime`を使うため、シミュレーションが一時停止中でも同じ完了Tickの情報を送信できる。
- `isSending`で送信中の重複開始を防ぐ。送信が間隔より長引けば、実際の送信頻度も下がる。
- 未接続時は送信しない。送信エラーの処理と接続再試行は従来の仕組みを使う。
- 全Tickを送る履歴配信ではなく、送信時点で最新の完了Tickを送る。早送り時は途中のTickが省略される。
- Tick外で連動管理部の現在出力が変わっても、送信する連動状態は次のTickが完了するまで前の完了出力を保持する。

## サーバー側で一括保存する仕組み

`UnityHub`は`UnitySnapshotStore`をコンストラクターで受け取り、受信RPCから`Update(message)`を呼ぶ。`Program.cs`のSingleton登録により、HubとHTTP窓口は同じStoreを参照できる。

Storeは`Message<UnitySnapshot>? latest`を1つだけ保持する。`Update()`と`GetLatest()`で同じ`syncRoot`をロックし、ヘッダー・時刻・駅辞書・進路辞書・各状態を深いコピーする。

- 読み取りは更新前のセット、または更新後のセットのどちらかを返す。時刻だけ新しく連動だけ古いセットは返さない。
- 呼び出し側が入力DTOや`GetLatest()`の返却DTOを変更しても、保存した内容は変わらない。
- 受信ごとに全体を置換する。前のメッセージにだけあった駅・進路を残さない。
- 未受信は`null`。受信済みで駅数0なら、空の`InterlockingSnapshot`を含むメッセージを返す。

`CTCEndpoint`などの利用側では、1回の取得結果から両方を読む。

```csharp
var message = snapshotStore.GetLatest();
if (message is null)
{
    // Unityの状態をまだ受信していない。
    return;
}

var sessionId = message.Header.SessionId;
var worldTime = message.Payload.WorldTime;
var interlocking = message.Payload.Interlocking;
// このworldTime.TickCountに対応する連動状態を使用する。
```

時刻と連動を読むために`GetLatest()`をそれぞれ呼ぶと、その間に更新が入る可能性がある。1回返されたメッセージを同じレスポンスや処理に使う。

## SceneとGUIDの移行

```text
Communication（UnityCommunicationController）
└── Senders（ServerMessageSenderController）
    └── UnitySnapshotSender
```

新Senderには旧WorldTimeSenderのGUID `7377732c60cc3462687f9d2020ae2258`を引き継いだ。保存済みSceneのGameObject/Transform/MonoBehaviourのfileID `419374862 / 419374863 / 419374864`も維持し、名前とクラス識別子を更新した。

旧InterlockingSnapshotSenderのfileID `1204896495 / 1204896496 / 1204896497`と親の子参照を削除した。旧GUID `ff2163c8ae92f47699a27d5deb942c9f`へのScene参照は残していない。新DTOのGUIDは`f47f69b07fb945e099a7caca6d85410f`。

編集したのは保存済みSceneファイルであり、Unity Editorで開いている未保存のSceneを保存・破棄する操作は行っていない。再コンパイル・Scene再読み込み後、上記のSenderが1つであることを確認する。別のSceneやPrefabに旧InterlockingSnapshotSenderを配置している場合は、同様に削除して新Senderへ統合する。

## 接続・再起動時の挙動と互換性

旧2 RPCを新RPCへ置き換えたため、Unityとサーバーの両方を更新・再ビルドして使う。古いプロセスを起動したまま新しい相手へ接続する互換性は提供していない。`SchemaVersion`の既定値は1を維持しており、バージョンの拒否・交渉処理はまだない。

切断しただけならサーバーの最後のメッセージは残る。Unityを再起動して新しい`SessionId`・Tick 0のメッセージを受信すると、ヘッダー・時刻・連動状態をまとめて置き換える。サーバーを再起動するとメモリー内のStoreは未受信`null`へ戻る。

現時点は1つのUnityからの受信を前提とし、受信した最新メッセージを採用する。セッション所有権、古いTickの拒否、通信の失効判定、受信日時、履歴・ディスク保存は今回追加していない。最後のメッセージが存在することと、現在接続して最新であることは別に扱う必要がある。

## 検証結果

2026年10月11日に以下を確認した。

| 検証 | 結果 |
| --- | --- |
| `dotnet build Server/Nakatetsu.TrafficControl.slnx --no-restore` | Server・Contractsとも警告0・エラー0。Store移動後も確認。 |
| Unity 6000.4.0f1のRoslyn・実際のAssembly参照・Source Generatorを使ったContracts/Applicationコンパイル | 両Assemblyとも警告0・エラー0。出力先は一時ディレクトリとし、Unityの生成DLLを上書きしていない。 |
| MessagePack往復 | ヘッダー、時刻全項目、駅・進路辞書、進路全項目、enumが一致。 |
| Storeのコピーと置換 | 入力・返却DTOの変更が保存値へ影響しない。消えた駅・進路の除去、未受信と空の区別を確認。 |
| Storeの並行アクセス | 1000回更新・5000回読み取りで、SessionId・Tick・連動状態の識別値が常に一致。 |
| 実MagicOnion通信 | 一時HTTP/2サーバーへ実クライアントから送信。本番Hubを通じてTick 42/43/44、駅数2/2/0がSingleton Storeへ一括保存されることを確認。 |
| 切断・再接続 | 切断後に最新値を保持し、新SessionId・Tick 0の再接続後はセット全体が置き換わることを確認。 |
| Unityの送信・完了Tick公開 | 実ソースをリンクした一時.NETハーネスで、Tick 0、ATCまで完了後の公開、Tick外の出力差し替えからの隔離、全フィールドコピー、1回のRPC、送信中に次Tickが進んだ場合のDTO独立、未完了Tickを公開しないことを確認。 |
| 定期送信制御 | 同ハーネスでInspector間隔、一時停止中のunscaled時間、未接続停止、送信の重複防止、送信失敗後の復帰を確認。 |
| 保存済みScene・meta・差分 | Senderが1つ、GUID引き継ぎ、削除したID/GUIDの参照なし、fileID重複なし、`git diff --check`を確認。 |

通信テストは空きポートを使い、終了時にテストサーバーを停止・破棄した。既存のユーザーのサーバーには接続・停止していない。Unity側ハーネスではSimulation/Sender/定期送信とTrackの不変な出力型を実ソースで使用し、Unityライフサイクル・下位Controller・RPC先を代替した。Unity EditorのPlayModeによる実Scene試運転は今回の確認には含まない。

一時検証コード・コンパイルログの保存先（リポジトリ外のため、端末の一時ファイル整理で削除され得る）：

- サーバー実通信・Store検証：`/private/var/folders/ts/73r6m8953hb8jmkq6ty0wtz40000gn/T/nakatetsu-unity-snapshot-check-4v_pde2e/Validation.csproj`
- Unity側の意味の検証：`/var/folders/ts/73r6m8953hb8jmkq6ty0wtz40000gn/T/nakatetsu-unity-snapshot-validation-w3is3i4e/Validation.csproj`
- Unity実参照コンパイルのrsp・ログ：`/var/folders/ts/73r6m8953hb8jmkq6ty0wtz40000gn/T/nakatetsu-unity-snapshot-compile-c5xpkki4/`

再実行する場合はリポジトリのルートからサーバービルドを行う。上記ハーネスが残っている場合は、サーバー検証を`dotnet run --project <サーバー検証のcsproj> --no-restore`、Unity側検証を`dotnet run --project <Unity側検証のcsproj> --artifacts-path <一時ディレクトリ>/artifacts`で実行できる。

## Web取得への接続

ブラウザー向けの`CTCEndpoint`を追加し、`UnitySnapshotStore.GetLatest()`を1回呼んで時刻と連動状態を返す`GET /api/ctc/snapshot`を実装した。未受信なら503、受信済みなら200でメッセージ全体のJSONを返す。詳細は[Web取得APIの実装記録](CTCWebSnapshotApi.md)を参照。表示画面・切断・情報失効の表示は別途追加する。軌道回路・転轍機・列車状態を同じTickで公開したい場合も、完了Tickの出力を保持してから`UnitySnapshot`へ追加する。
