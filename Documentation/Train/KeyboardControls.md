# キーボード運転入力仕様

合意日：2026-09-24。W1-04に向けて確定した仕様であり、実装完了を示すものではない。

## キー割り当て

| キー | 操作 | 挙動・条件 |
| --- | --- | --- |
| ↑ | ノッチを制動側へ | 1回押すごとに1段。常用最大の次は非常 |
| ↓ | ノッチを力行側へ | 1回押すごとに1段。非常からは常用最大へ戻る |
| ← | 惰行 | ノッチを直接Nへ |
| W | レバーサーを前進側へ | 後進→中立→前進の順に1段。非常ノッチ時のみ操作可能。速度による制限なし |
| S | レバーサーを後進側へ | 前進→中立→後進の順に1段。非常ノッチ時のみ操作可能。速度による制限なし |
| Space | EBリセット | 押した瞬間に1回リセット操作 |
| B | ブザー | 押している間だけ鳴る |
| G | 勾配起動 | 押している間だけ有効 |
| Enter | 警笛 | 押している間だけ鳴る |

## ノッチとレバーサー

- ノッチはワンハンドル方式とし、Nを挟んで力行側と制動側を行き来する。例：P2で↑を押すたびにP1→N→B1となる。
- ノッチの長押しによる連続操作は行わない。
- 非常ノッチを戻し、ほかの非常要求がなければ、TIMSの通信更新後に走行中でも非常制動を解除する。EB装置が作動している場合は、装置側が停車・力行なしまで非常要求を保持する。
- レバーサーの操作条件は「非常ノッチであること」とする。MasterControllerLogicとデバッグ操作へ反映済み。
- 前進・後進は指定された運転台を基準とする。

## EBリセット

- Spaceの押下を`TrainKeyboardInputController.OnEbReset` → 注目編成の`TrainIntegrationController.Input.ResetEb()` → 有効運転台の`EbDevice.RequestReset()`へ渡す。前側は車両index 0、後側は最後尾のEB装置を対象とする。
- 要求は次の入力収集で取り込み、1 tickだけ有効。作動前（60秒の監視中・5秒のブザー猶予中）の無操作時間を0秒に戻し、ブザーを止める。長押し・キーを離したときには再リセットしない。
- 作動後の非常要求はSpaceでは解除しない。既存の「停車（絶対速度0.01 m/s未満）かつ力行なし」で自動解除する。
- 有効運転台なし、対象EBの欠損・無効・重複では要求を受け付けない。他編成のEBには渡さない。

## 注目対象と開始時の状態

- 注目対象は、編成と前側／後側の運転台の組で指定する。
- 前側／後側は編成定義上の固定された前後とし、後退しても入れ替えない。
- 注目する編成と運転台はゲーム側が決める。プレイヤーによる切り替え操作は設けない。
- 指定された運転台はゲーム開始時に自動で有効化する。有効化用のキー操作は設けない。
- 開始時は非常ノッチ・レバーサー中立とする。Wで前進へ入れた後、ノッチを操作して運転を始める。

## 乗務方式とドア操作

- ツーマン運転とし、プレイヤーは運転士を担当する。
- ドア開閉と発車合図はゲーム側の車掌処理が担当する。プレイヤー用のドア開閉キーは設けない。
- 提案段階のQ／A／E／Dによるドア操作は採用しない。
- 車掌処理による戸扱い・発車合図のタイミングや条件は、駅での運転フローを決める際に具体化する。

## 未確定事項

- 一時停止・再開のキー。
- 一時停止中の運転入力の扱い。

## 関連資料

- [実装計画（W1-04）](../implementation-plan-2026-11-17.md)
- [既存のInspectorによる運転デバッグ](DebugControls.md)

## Input System接続

- `Application/Player/Data/TrainInputActions.inputactions`の`Driving` Mapに、↑／↓／←／W／S／Spaceの6操作を定義する。各ActionはButton、Press Only。
- `PlayerInput`のBehaviorは`Invoke Unity Events`、Default Mapは`Driving`とする。
- `TrainKeyboardInputController`が`performed`だけを受け取り、指定した`TrainIntegrationController.Input`へ操作を渡す。長押しによる連続操作や、離したときの再操作は行わない。
- `Application/Focus`の`TrainFocusController`が初期注目編成と注目先の切り替えを管理し、`Application/Input`の`TrainKeyboardInputController.SetTarget`で入力先も更新する。
- `TrainCabInitializer`が生成済みの前後の操作機器を取得し、指定した運転台を有効化する。初期状態は非常ノッチ・レバーサー中立。TIMSが判定した後に入力可能となる。
- 入力セットアップは1001FのIntegrationと、`Application/Input`への6つのイベント接続を行う。初期注目編成が未設定なら1001Fを設定する。未保存のシーンやPlay中には実行しない。
- 手動実行は `Nakatetsu > Setup > Connect NtLine Keyboard Input`。実行前にシーンを保存する。
- Play開始後にGameビューへフォーカスし、Wで前進、←でN、↓で力行する。非常要求が残っている場合はTIMSの非常理由を確認する。EB装置の作動後は停車・力行なしで装置が解除し、その結果が次の通信更新で反映される。↑で制動側へ戻す。レバーサー操作には非常ノッチが必要。
- B／G／Enterの接続は後続作業。

## Integrationの構成

`Train/Integration`は次の構成とする。Applicationは`TrainIntegrationController`を入口として参照する。

- `Orchestration/Scripts/TrainIntegrationController.cs`：Input・Statusの参照公開と初期化の取りまとめ。
- `Input/Scripts/TrainInputController.cs`：有効運転台へのノッチ・レバーサー操作とEBリセット。
- `Status/Scripts/TrainStatusController.cs`：有効運転台とマスコン位置の取得。
- `Status/Scripts/TrainCabControls.cs`：取得した操作位置の値。
- `Initialization/Scripts/TrainCabInitializer.cs`：開始時の運転台準備。IntegrationのStartから呼ぶ。
- `Shared/Scripts/TrainCabResolver.cs`：TIMSの有効運転台と対応マスコン・EB装置の取得。Integrationが生成し、Input・Statusへ渡す。

InputとStatusは互いを参照せず、下位ControllerからIntegrationへの参照も持たない。

```csharp
trainIntegration.Input.MoveNotchTowardBrake();
trainIntegration.Status.TryGetActiveCabControls(out var controls);
```

### GameObjectの配置

`TrainRoot`は編成ルート（例：1001F）に1つだけ置き、Integration・Input・Status・CabInitializerの4コンポーネントは子の`Integration`へまとめて配置できる。Integrationは親のTrainRootを取得し、機器の探索はTrainRootを起点として兄弟のEquipmentsまで含める。

既存コンポーネントを移す場合は、キーボード入力のTargetに移動先のTrainIntegrationControllerを設定する。セットアップメニューは配置済みのIntegrationを再利用し、未配置の場合は子のIntegrationへ作成する。

### ApplicationのGameObject配置

```text
Application
├── Simulation    ApplicationSimulationController
├── Focus         TrainFocusController
├── Input         PlayerInput・TrainKeyboardInputController
├── Audio         ApplicationAudioController
└── Main Camera   Camera・AudioListener・TrainCameraController
```

- `Focus.Keyboard Input`には`Input`の`TrainKeyboardInputController`を設定する。別GameObjectなので、同一GameObjectの`GetComponent`による自動取得には頼らない。
- `Audio.Focus Controller`には`Focus`の`TrainFocusController`を設定する。音声の再生許可はApplicationAudioControllerが更新する。
- `Main Camera.Focus Controller`には`Focus`の`TrainFocusController`を設定する。
- `PlayerInput`のイベントは同じ`Input`にある受信メソッドへ接続する。
- `ApplicationSimulationDebugPanel`を使う場合は`Simulation`に配置する。
- UI・CanvasはHUD実装時に追加する。

既存シーンの整理は`Nakatetsu > Setup > Organize NtLine Application`で実行する。シーンを保存し、Playモードを終了してから実行する。Simulationの設定、初期注目編成、入力イベント、カメラのワールド位置・回転を引き継いで保存する。初期注目編成が未設定の場合はそのまま維持する。再実行時は配置済みの子GameObjectとコンポーネントを再利用する。

`Connect NtLine Keyboard Input`もこの階層へ整理してから入力を接続する。
