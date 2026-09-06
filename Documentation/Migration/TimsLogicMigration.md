# TIMS Context・Logic移植

## 対象と移植元

- 対象：通信タイミング、ノッチ決定、ブレーキ配分、力行力と定速状態の計算。
- 移植元：`/Users/yudai/TD-ATC/Assets/Scripts/Train/Ntims` の作業ツリー。
- 読み取り時のHEAD：`0913d6eeb74f4743664b3603de2564110fe86b07`。
- 移植元には未追跡ファイルがあるため、HEADだけでは同じソースを再現できない。読み取り時の各C#ファイルのSHA-256を `TimsSourceHashes.json` に記録した。
- 旧プロジェクトは変更していない。

MonoBehaviour、車両Controller、Prefab、ScriptableObject、シーンは移植していない。
新側にあった空の `TimsCommunicationController.cs` は変更していない。
既存 `TimsBusState.cs` の動作も変更していない。

## 配置とnamespace

すべて既存の `Nakatetsu.Train.Tims` アセンブリ内に置く。
移植した実装はUnityEngine・車両アセンブリへの型参照を持たず、値の入出力だけで実行できる。

| Runtime以下 | namespace | 内容 |
| --- | --- | --- |
| `Bus` | `Nakatetsu.Train.Tims.Bus` | 値・キー・型・バス状態 |
| `Communication` | `Nakatetsu.Train.Tims.Communication` | 通信Context・送信時刻判定・各車の値の収集 |
| `Notch` | `Nakatetsu.Train.Tims.Notch` | 運転台選択・手動/ATCノッチの統合・方向判定 |
| `Brake` | `Nakatetsu.Train.Tims.Brake` | ブレーキ目標・最低空気圧・回生/空気ブレーキ配分 |
| `Traction` | `Nakatetsu.Train.Tims.Traction` | 定加速/定出力・BCインターロック・定速状態・VVVF配分 |
| `Internal` | `Nakatetsu.Train.Tims.Internal` | 純粋なClampと旧Mathf.Approximately相当の内部ヘルパー |

各機能の `Context.cs` に、その機能専用のInput・Settings・Output・State・Workspaceをまとめた。
フレーム間の状態を必要としないノッチなどには空のStateを追加していない。
配列・リストのOutputは次回計算で更新する。履歴として保持する場合は呼び出し側でコピーする。

## 旧実装からの対応

| 移植元 | 新側 |
| --- | --- |
| `TimsController` の送信時刻判定・スケジュール | `TimsCommunicationLogic` |
| `TimsController` の `CollectFloatFromCars` | 同名の純粋なデータ収集メソッド |
| Controller/端末のState・Workspace | `TimsCommunicationContext.cs` 内の専用データ型 |
| `TimsNotchController` の運転台選択・方向・ノッチ決定・表示文字列 | `TimsNotchLogic` |
| `TimsNotchCalculator` | 同名で移植 |
| `TimsBrakeController` の計算メソッド群 | `TimsBrakeLogic` |
| `TimsBrakeCalculator` | 同名で移植 |
| `TimsTractionForceController` の計算・状態更新・指令の均等配分 | `TimsTractionLogic` |
| `TimsTractionState` | 速度をMps、時間をSeconds表記へ統一して移植 |

## 入力と呼び出し方

ノッチ・ブレーキ・力行は `Logic.Calculate(context)` で計算する。
将来のControllerが入力収集と結果の反映を行う。Logicはバスや車両を自動検索しない。

```csharp
var context = new TimsBrakeContext();
context.Settings.brakeTargetDecelerationsMps2.Add(1f);
context.Settings.minimumServiceBrakePressureKPa = 0f;
context.Input.canReleaseEmergencyBrake = true;
context.Input.brakeStep = 1;
context.Input.cars.Add(new TimsBrakeCarInput
{
    massKg = 40000f,
    isTrailerCar = true,
    airCapN = 60000f
});

TimsBrakeLogic.Calculate(context);
// context.Output.carCommands[0].targetAirForceN == 40000f
```

### 通信

- `State.terminals` に各車の純粋な端末Stateを、車両順に用意する。未接続の車両位置はnullで保持できる。
- 有効な端末の `carIndex` は編成内で一意にする。
- `Input.sources` に送信元の一意な整数ID、車両インデックス、送信間隔を渡す。IDは登録中に変えない。
- `Input.timeSeconds` は呼び出し側から渡す現在時刻。Logic内で `Time.time` を参照しない。
- `Calculate` は今回送信可能な `Output.dueSourceIds` を返すだけで、通信自体は実行しない。
- 呼び出し側が送信に成功した後で `MarkTransmitted(context, sourceId)` を呼ぶ。失敗時は次回も送信候補になる。
- 送信元を再登録したときは `ResetSchedule` を呼ぶ。旧 `RefreshDataSources` と同じく送信時刻をリセットする。
- 最低送信間隔は旧実装と同じ0.001秒。
- `CollectFloatFromCars` は、値と取得成否を車両順に返す。質量未取得時の車両定義からの補完は呼び出し側で行う。

既存 `ITimsBusSource.WriteTimsBus` は、未移植の `TimsCarTerminal` の代わりに `TimsBusState localBus` を受け取るように変更した。
これによりインターフェースはBus内に置いたまま、Controllerに依存しない。
送信元との接続・実装クラスの収集・車両インデックスの解決は、Controllerを実装する段階で追加する。

### ノッチ

- `Input.cars` は車両順。運転台選択が取得できない車両は `hasSelection = false` とする。
- `TimsCabSelection` は旧値を維持（Forward=0、Reverse=1）。
- `TimsReverserPosition` は旧値を維持（Reverse=-1、Neutral=0、Forward=1）。車両Controller内のenumは参照しない。
- バスからのデコードを終え、編成の入力が利用できる場合に `Input.isReady = true` とする。
- `Output.isEmergencyBrakeRequested` を将来の接続処理から非常ブレーキ回路へ反映する。
- Disable時の非常要求など、MonoBehaviourのライフサイクル動作は未移植。

### ブレーキ

- `Input.canReleaseEmergencyBrake` は、旧実装が調べていたTIMS・編成・設定・端末の利用可否を呼び出し側でまとめた値。
- falseのとき `Output.isEmergency = true`、`hasCommands = false`。前回指令は保持するため、このフレームは非常状態だけを反映し、保持された指令を新規指令として送信しない。
- `Input.cars` は車両順で、全車に非nullのレコードを用意する。
- `massKg` は荷重バスの値。取得できなければ呼び出し側が車両定義の質量で補完する。
- `isVvvfMotorCar` は、旧条件「Motor車・motorCount > 0・VVVF Prefabあり」を呼び出し側で判定した結果。
- `isTrailerCar` は、車両定義のTrailer判定結果。
- `regenForceN` はN。旧タグ `BrakeSystem/RegenForcekN` から受け取る場合は1000倍して入力する。
- 減速度テーブルは `Settings.brakeTargetDecelerationsMps2`。旧アセットのkm/h/sから移す場合は3.6で割る。
- BC圧、空制上限、圧力あたりの力などは、呼び出し側がタグからデコードして渡す。

### 力行

- `Input.isReady` は、旧実装で必要だったTrain・TrainSettings・TIMS・ControlConfigの利用可否に相当する。
- 車両質量、ノッチ、速度、BC圧、勾配起動フラグ、各VVVFのモーター情報を値として渡す。
- `Input.powerStepGain` は、旧設定の「選択ノッチのAnimationCurveを正規化速度で評価した値」。アセット評価は呼び出し側の担当。
- `Settings.launchAccelerationMps2` はm/s²。旧 `launchAccelerationKmhPerSec` からは3.6で割って設定する。
- `Input.deltaTimeSeconds` を渡して定速待機時間を更新する。
- `Output.unitTargetForcesN` は `Input.units` と同じ順序。
- `Output.hasForceCommand` がtrueのときだけ装置へ反映する。旧実装のブレーキ作動中の早期returnでは指令を配信していなかったため、その挙動を維持する。

## 維持した挙動と限定的な変更

- 要求回生力はモーター車質量比で配分し、実回生力の余剰でTrailer車の空制を均等に減らす。
- `TimsControlConfig.brakeDistributionPriority` は旧計算で参照されていない。新たな優先配分機能は実装していない。
- 旧 `ClampAdditionalAirForceN` は追加空制上限が0以下の場合、要求値を上限なしで返す。この挙動を変更せず、テストで明示した。
- `targetBrakeForceN` は旧タグと同様「最低空制分を除いた各車追加目標」であり、回生と空制の指令合計ではない。
- 定速はOff/Arming/Activeの状態、待機時間、目標速度保持までを移植。旧実装には目標速度との差から力行力を補正する処理と、OffからArmingへの開始処理はない。それらは新規実装していない。
- 編成の車両数を減らした場合、新側では余った指令レコードも削除する。旧実装で残り得た削除済み車両向けの指令は返さない。
- 通信入力の送信元ID重複やブレーキ入力のnull車両レコードは、呼び出し側のデータ不備として例外にする。
- バスの `ReplaceSnapshot` は既存動作を維持。自分自身の `Tags` を直接渡さず、`GetSnapshot()` の戻り値などの独立した一覧を渡す。

## 検証結果

- 純粋C#ビルド：成功（警告0・エラー0、C# 9）。
- Unity 6000.4.0f1 / Test Framework 1.6.0：EditModeテスト36件成功。
- Unityテストは新側の自作コードとasmdefをコピーした一時検証用プロジェクトで実行。起動中のEditorやシーンは操作していない。
- 検証用プロジェクトのログにC#コンパイルエラーなし。他の空のRuntime asmdefには「スクリプトがないためコンパイルしない」という通知がある。
- 元のノッチ・ブレーキ・力行Controllerのメソッドを実行する一時比較ハーネスで、同じ入力を各300ステップ、合計900ステップ与え、旧計算結果との一致を確認。Unityオブジェクト・アセット評価はテスト用の代替を使用しており、シーン上の更新順や実アセットとの接続を確認したものではない。
- Unityテストに使ったC#ソースと新側のC#ソースが一致すること、および記録した旧TIMSソースのハッシュが作業後も変わっていないことを確認。

テストは `Assets/Nakatetsu/Train/Tims/Tests/EditMode` に配置した。
新側をUnityで開いてコンパイル完了後、Test RunnerのEditModeで `Nakatetsu.Train.Tims.Tests` を実行できる。

次の段階は、Controller側でのアセット値の取得・タグのデコード・Logicへの入力・Outputの反映を接続すること。
今回の完了範囲はContext・Logicであり、TIMSをシーン内で動かす接続はまだ実装していない。
