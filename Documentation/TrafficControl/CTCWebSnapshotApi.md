# CTC向けスナップショット取得API

実装日：2026年10月11日。Unityから受信・保存した世界時刻と連動状態を、ブラウザーからHTTPで取得する最初のAPI。

## URLと確認手順

```http
GET /api/ctc/snapshot
```

現在の`appsettings.json`ではブラウザー向けHTTP/1のポートは5187、Unity向けMagicOnionのHTTP/2ポートは5188。

1. 更新したサーバーを起動する。既に以前のコードで起動中なら、そのサーバーを再起動する。

   ```bash
   dotnet run --project Server/Nakatetsu.TrafficControl.Server --launch-profile http
   ```

2. ブラウザーで`http://localhost:5187/api/ctc/snapshot`を開く。未受信なら503が返る。
3. Unityを実行してスナップショットを送信し、ブラウザーを再読み込みする。受信したメッセージがJSONで表示される。

ターミナルでも確認できる。

```bash
curl -i http://localhost:5187/api/ctc/snapshot
```

現段階では要求した時点の最新値を返す。ブラウザーの自動更新・表示画面は今後追加する。

## 応答仕様

| 条件 | HTTPステータス | 内容 |
| --- | --- | --- |
| まだ一度もスナップショットを受信していない | 503 Service Unavailable | `application/problem+json`。`status`は503、`detail`は「Unityの状態をまだ受信していません。」 |
| 保存済みスナップショットがある | 200 OK | `application/json`。`Message<UnitySnapshot>`全体を返す。 |

成功時はASP.NET Coreの既定のJSON設定を使うため、プロパティ名は`header`・`payload`・`worldTime`などcamelCaseとなる。駅ID・進路IDなど辞書のキーは元のIDを維持する。進路状態のenumは数値で返す。

Tick 0・空連動状態を受信した場合の応答例：

```json
{
  "header": {
    "schemaVersion": 1,
    "sessionId": "example-session",
    "messageType": "UnitySnapshot"
  },
  "payload": {
    "worldTime": {
      "tickCount": 0,
      "worldTimeSeconds": 43200,
      "tickDurationSeconds": 0.01,
      "isPaused": true,
      "playbackSpeed": 1
    },
    "interlocking": {
      "stationsById": {}
    }
  }
}
```

空の連動状態を受信した場合も200であり、未受信の503とは区別する。切断後も保存値は残るため、200は現在接続中であることを示さない。通信の失効判定・接続状態の表示は別途追加する。

## 変更ファイルと処理

| ファイル | 変更内容 |
| --- | --- |
| [Ctc/Scripts/CTCContext.cs](../../Server/Nakatetsu.TrafficControl.Server/Ctc/Scripts/CTCContext.cs) | CTC本体の入力と出力を保持する。どちらも`Message<UnitySnapshot>?`で、未受信はnull。setterはinternal。 |
| [Ctc/Scripts/CTCLogic.cs](../../Server/Nakatetsu.TrafficControl.Server/Ctc/Scripts/CTCLogic.cs) | `Calculate()`で収集済み入力を出力へ公開する最小処理。通信・Store・HTTPへ依存しない。 |
| [Ctc/Scripts/CTCController.cs](../../Server/Nakatetsu.TrafficControl.Server/Ctc/Scripts/CTCController.cs) | Contextを非公開で所有し、`GetSnapshot()`の同じlock内で入力取得・Logic実行・出力コピーを行う。UnityのMonoBehaviourを継承しない普通のC#クラス。 |
| [Communication/Web/Scripts/CTCEndpoint.cs](../../Server/Nakatetsu.TrafficControl.Server/Communication/Web/Scripts/CTCEndpoint.cs) | `MapRoutes()`でURLを登録する。`GetSnapshot(CTCController)`から本体を呼び、未受信なら`Results.Problem()`、受信済みなら`Results.Ok(message)`を返す。 |
| [Communication/Unity/Scripts/UnitySnapshotStore.cs](../../Server/Nakatetsu.TrafficControl.Server/Communication/Unity/Scripts/UnitySnapshotStore.cs) | 既存の深いコピー関数`Copy()`をprivateからinternalへ変更し、Controllerの返却用コピーにも再利用する。コピー項目と保存処理は維持。 |
| [Program.cs](../../Server/Nakatetsu.TrafficControl.Server/Program.cs) | StoreとCTCControllerをSingleton登録し、`CTCEndpoint.MapRoutes(app)`を呼ぶ。個別URLはEndpoint側で登録する。 |

### ルート登録の分離

追加指示に合わせ、`Program.cs`へCTCの全URLを列挙する構成をやめ、`CTCEndpoint`が自分のルート登録を持つようにした。

```csharp
// Program.cs：CTCのルートをまとめて登録する。
CTCEndpoint.MapRoutes(app);
```

```csharp
// CTCEndpoint.cs：CTCのURLと処理を対応させる。
public static void MapRoutes(IEndpointRouteBuilder endpoints)
{
    var group = endpoints.MapGroup("/api/ctc");
    group.MapGet("/snapshot", GetSnapshot);
}
```

グループの`/api/ctc`と子ルートの`/snapshot`を合わせたURLが`/api/ctc/snapshot`になる。今後CTCのAPIを追加するときは、この`MapRoutes()`へ登録を追加する。別のAPI群を作るときも、そのEndpointが登録メソッドを持ち、`Program.cs`から呼ぶ。URL・JSON・ステータスの仕様は変更していない。

ASP.NET Coreは、Singleton登録済みの`CTCController`をハンドラーの引数へ渡す。ControllerのコンストラクターにはSingletonの`UnitySnapshotStore`が渡され、UnityHubとCTC本体は同じStoreを使う。

### CTC本体の最小構成と同期

2026年10月11日、共有Contextをlockで保護する方針に合わせ、CTC本体をContext・Logic・Controllerへ分離した。名前空間は`Nakatetsu.TrafficControl.Server.CTC`、配置は`Ctc/Scripts`。

```text
GET /api/ctc/snapshot
  → CTCEndpoint.GetSnapshot(CTCController)
  → CTCController.GetSnapshot()
      lock開始
      → Store.GetLatest()を1回呼び、Context.Inputへ収集
      → CTCLogic.Calculate(context)でContext.Outputへ公開
      → Outputを深いコピーにして返す
      lock終了
  → Endpointが200/503とJSONへ変換
```

現段階のLogicは、InputをそのままOutputへ公開する処理だけを持つ。未受信nullと受信済みメッセージを維持し、既存のURL・JSON・HTTPステータスを変えずに本体へ取得処理を移した。通信失効判定・操作要求・列車追跡・PRCは後続の機能。

ContextはControllerのprivateフィールドで保持し、外へ公開しない。InputとOutputは現在同じ内部DTOを参照するが、Controllerが独占して扱い、外へ返す際には深いコピーを作る。呼び出し元がヘッダー・時刻・駅・進路・辞書を変更しても、ControllerのContextには反映されない。

複数のHTTP要求は、Controllerの同じ`syncRoot`のlockで順番に処理する。入力取得から返却用コピーまでを1つの処理として保護し、Contextの入力・出力が別要求の途中状態に差し替わることを防ぐ。HTTPのJSON出力はlockを抜けてから、独立した返却用メッセージを使って行う。lock内に通信待ちやawaitはない。

今後Contextへ新しい状態や処理を追加する際も、Controllerの同じlock内から更新・読み取りを行い、外部には独立したデータを返す。現在ControllerからStoreの順にロックを取得しており、StoreからControllerへ呼び戻す処理はない。

Storeからの取得は1回だけで、同じメッセージに含まれるヘッダー・世界時刻・連動状態を使用する。取得後に新しい受信があっても、返却用メッセージの内容は変わらない。同じTickを維持する送信・保存の詳細は[Unityスナップショットの実装記録](UnitySnapshotSynchronization.md)を参照。

## 検証

- `dotnet build Server/Nakatetsu.TrafficControl.slnx --no-restore`：警告0・エラー0。
- 本番`Program.cs`から生成したサーバーを空きポートで起動し、未受信の503、problem JSON、既存ルート`/`の200、未定義パスの404を確認。
- 一時検証アプリで本番のHub・Store・Endpointを使用し、実MagicOnionでTick 57・2駅を送信後、HTTP GETで200と全ヘッダー・世界時刻・駅・進路フィールドの一致を確認。大文字小文字の異なる`Route`/`route`キーも維持される。
- 取得したJSONのデシリアライズ結果を変更しても、再取得した保存値に影響しないことを確認。
- 新SessionId・Tick 0・空連動状態の受信後、HTTP応答もセット全体が置き換わることを確認。
- `CTCEndpoint.MapRoutes(app)`経由のルート登録へ変更後も、上記の実HTTP検証がすべて成功。URLと応答は維持された。
- CTC本体追加後、本番ProgramのSingleton登録を使って起動し、Controllerを通じた未受信503、既存の200/404を確認。
- Controllerの返却DTOのヘッダー・時刻・進路・辞書を変更しても、Context.Output・Store・次の取得値が変わらないことを確認。
- 600回のStore更新と3000回の並行Controller取得で、SessionId・Tick・連動状態の識別値が一致し、例外がないことを確認。
- Controllerを経由した実HTTP/MagicOnionの受信済み200、JSON全項目、新セッションTick 0・空状態への置換も再確認。

検証サーバーはHTTP/1・HTTP/2の別空きポートを使い、終了時に停止・破棄した。ユーザーの既存サーバーは操作していない。

一時検証コードは`/private/var/folders/ts/73r6m8953hb8jmkq6ty0wtz40000gn/T/nakatetsu-ctc-http-check-fzn_u10u/Validation.csproj`、CTC本体追加後の本番起動の検証ログは`/var/folders/ts/73r6m8953hb8jmkq6ty0wtz40000gn/T/nakatetsu-ctc-core-program-check-q_ar9qk5/server.log`へ保存した。リポジトリ外の一時ファイルのため、端末の整理で削除され得る。本番ビルドと一時ハーネスのコンパイルはともに警告0・エラー0、実通信検証もすべて成功した。
