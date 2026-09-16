# TIMSノッチ表示

`Assets/Nakatetsu/Train/Equipment/Tims/Display/Notch/Prefabs/TimsNotchDisplay.prefab` をCanvas配下へ配置し、ルートの `TimsNotchDisplay` の **Tims** に対象編成の `TimsCommunicationController` を割り当てる。実行時に生成する場合は `Configure(source)` で接続する。

接続先には設定済みの `TimsRoot` と、通常のTIMS制御パイプラインが必要。`NtLine` のTIMS Prefabには含まれている。既存の `Prototype/Tims` は表示の試作シーンであり、通信Controllerだけを割り当てても制御値は生成されない。

## 表示

- 上から非常、B7〜B1、N、P1〜P4。既存セルの色・フォントを使用する。標準サイズは50×732で、各セルは50×50。
- P3ならP1・P2は文字なしで点灯し、P3に段数を表示する。B段も同じ積み上げ方式。
- P/Bの消灯段は段数をグレーで表示する。点灯中の下位段だけ文字を隠す。
- 力行0・制動0かつ非常でなければNを点灯する。
- 非常要求または非常保持中は非常セルだけを点灯する。
- 入力欠損・型不一致・範囲外・通信Controller未設定／無効は全セルを消灯し、下部に `--` を表示する。既知の非常要求は、通常ノッチの欠損より優先する。

Prefab内のセルは既存Prefabのインスタンス。配置・配色はPrefab Variant等で調整できる。段数を変える場合は、対応するP/Bセルを追加し、各セルの `Notch` を設定する。表示対象の段のセルがなければ欠損扱いにする。制御設定の段数は表示側で変更しない。

## 読み取る値

| MasterBusタグ | 用途 |
| --- | --- |
| `Notch.ResolvedPowerNotch` | 確定力行ノッチ |
| `Notch.ResolvedBrakeStep` | 確定ブレーキStep |
| `Notch.IsEmergencyBrakeRequested` | ノッチ側の非常要求 |
| `Brake.IsEmergency` | EB・入力欠損等を含むTIMSの非常保持 |

ブレーキStepは同じTIMSの `TimsRoot.Settings.brakeSubstepCount` で段数へ変換する。刻み4ならStep1〜4はB1、5〜8はB2、25はB7。補間中の細分ステップの文字表示は行わない。

表示は `LateUpdate` でBusを読むだけで、装置や制御Logicを進めない。編成の全体検索や、マスコン・Physicsへの直接接続は行わない。Bus自体に鮮度情報がないため、送信が止まってもタグが残る場合のタイムアウト検知は含まない。

## 旧プロジェクトとの対応

参照元は `TD-ATC` のコミット `0913d6eeb74f4743664b3603de2564110fe86b07`。

- `Assets/Scripts/UI/PowerDisplayBuilder.cs` / `BrakeDisplayBuilder.cs`：現在段まで点灯する挙動を継承。文字は現在段と消灯段に表示し、点灯中の下位段だけ隠す（消灯段のグレー表示は現行側の仕様）。
- `NeutralDisplay.cs` / `EBDisplay.cs`：N・非常を別表示にする構成を継承。
- 旧 `Main.unity` のP4/B7とP/B読み取り遅延0に合わせる。N・非常も同じスナップショットへ即時反映し、旧側の個別遅延キューは移植しない。
- 旧TrainController直接参照を、現在の確定ノッチ・非常保持のBus参照へ変更。欠損をNと区別し、古い点灯を残さない。
- 配色と形状は現在のセルPrefabを使用する。

`Display/Notch/Tests` でPrefab構成、積み上げ点灯、ブレーキStep境界、設定変更、非常保持、欠損・不正値・切断後の表示を検証する。
