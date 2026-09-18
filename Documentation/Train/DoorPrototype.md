# ドア試作：片側4か所・TIMS監視

2026-09-17。1車両につき片側4か所、左右合計8か所を持つ試作。
1か所は1組の乗降用ドアを表し、両開き戸の左右の戸板を別々には数えない。

## 試し方

既存の試作Sceneは変更していない。運転用のTrainRoot、TrainSimulationController、速度センサー、TimsCommunicationControllerがある編成で使用する。

### Scene・車種Assetを変更せずに試す

1. Unityで既存の車両試作Sceneを開いてPlayに入る。
2. 編成のTrainRootに `DoorDebugPanel` を追加する。
3. Gameビューのパネルで `Install prototype on all cars` を押す。
4. 停車中に `Open left` / `Open right`、続いて `Close both` を操作する。
5. パネルの各車のL/Rの4個の状態と、Data / All closed / Tractionを確認する。

インストールはPlay中の編成インスタンスにだけ追加し、既存ドアのある車両には重複追加しない。Play終了で消える。
パネルのボタンは試験用の直接入力。正式な運転台の操作権限やホーム側照合、TIMSのボタン画面遷移は実装していない。

### 継続使用するPrefab

`Assets/Nakatetsu/Train/Equipment/Door/Prefabs/DoorPrototype.prefab` を各CarDefinitionの `additionalEquipmentPrefabs` に追加する。
各車1個を前提とする。既存の速度センサーPrefabは残す。
Prefabには以下が含まれる。

- DoorController：左右別の指令受付、速度/側別許可による開扉判定。
- TrainDoorSimulation：8か所それぞれの開き具合と閉接点。開閉時間の初期値は3秒。
- TrainEquipmentAssignment：Builderが車両Indexを割り当てる。
- DoorTimsInputAdapter：MasterBusの測定速度を機器へ渡す。
- DoorTimsBusSource：閉接点と各ドアの状態をLocalBusへ公開する。

通信Controllerはドア送信元を検出すると同じGameObjectへTimsDoorControllerを追加する。以後、全車のドア情報が必要となる。ドア未搭載の既存編成には力行条件を追加しない。

## 動作の分担

```text
試験操作 → DoorController → 左右のOpen / Close / Hold指令
                                ↓
TrainSimulationController → TrainDoorSimulation（時間進行はここから1回）
                                ↓
                  実開度と閉接点・故障状態
                                ↓
DoorTimsBusSource → 各車LocalBus → TimsDoorController → MasterBus
                                                       ├─ 監視表示
                                                       └─ TIMS力行処理
```

`TrainDoorSimulation.TryGetDoor()` の開き具合を将来の3D表示が読む。今回のPrefabに3Dモデルやドア板のアニメーションは含めない。
閉接点はドア内で生成し、専用センサーGameObjectを増やしていない。現在は全閉位置でオン、故障時はオフ。機械的な鎖錠の時間応答は未実装なので、「閉接点＝鎖錠確認」とは扱わない。

## 更新順

1. 速度センサー測定。
2. 各車ドアの前ステップの接点/状態、現在の開指令受付状態をLocalBusへ収集。
3. TIMSが速度・ドア状態を集約し、力行/制動を計算。
4. DoorControllerが測定速度を読み、開閉の許可と出力を計算。
5. 編成Simulationが各車ドアの物理モデルを1回進める。
6. 次ステップの通信で更新結果を公開。

閉検知表示には最大1シミュレーションステップの遅れがある。開操作と力行操作が同時でも動力を出さないよう、開指令受付中は閉接点がまだオンでもドア由来の力行許可を落とす。
ドア由来の力行禁止は非常ブレーキ指令ではない。坂道の停止保持は既存の物理/ブレーキ側が担当する。

## 試作の許可条件

- 開扉は有効な測定速度が必要。初期値は `0.1 m/s以下`。これは試作用の設定であり、特定実車の回路仕様ではない。
- 左右の `OpenPermitted` は独立して設定する。現在は両側true。駅・地上子・ホームドアとの接続は今後行う。
- 速度欠損・非有限値・速度超過・側別許可なしでは開指令を拒否する。
- 開動作中に許可を失った場合はHoldにし、許可が戻っても再操作まで開かない。
- 閉指令は開扉用の速度/側別許可を必要としない。欠損時も閉動作を進められる。
- 途中での再開扉は、再度開扉許可を満たした場合だけ受け付ける。
- Faultチェックは試験用の車両単位故障入力。故障中は動かず、閉接点をオフにしてFaultを公開。
- 個別戸挟み、非常コック、ドア締切、電源/空気圧モデル、半自動扱い、ワンマンの操作権限は未実装。

## TIMSタグ

| Bus | Device / Item | 型・意味 |
| --- | --- | --- |
| Local | Door / LeftStatus、RightStatus | int配列、車体前方から後方へ4個。0=Closed、1=Opening、2=Open、3=Closing、4=Stopped、5=Fault |
| Local | Door / AllClosed | bool、当該車両の全ドア閉接点成立 |
| Local | Door / HasFault | bool、当該車両のいずれかのドアに故障 |
| Local | Door / HasOpenCommand | bool、開指令受付中または開方向の指令を保持中 |
| Master | Door / HasValidState | bool、全車のドア集約情報を取得できた |
| Master | Door / AllClosed | bool、全車の情報が有効かつ全閉・故障なし |
| Master | Door / HasFault | bool、取得済み車両に故障あり。falseでもHasValidState=falseなら正常とは判断しない |
| Master | Door / CarClosedStates | int配列、編成順。-1=不明、0=未閉/故障、1=閉 |
| Master | Door / TractionPermitted | bool、有効な全扉閉かつ開指令なし。列車全体の発車許可とは別 |

送信元・物理モデル・ドア制御が無効な場合は、LocalBusの対応タグを削除する。未取得を正常な閉として扱わない。
既存TimsBoolIndicatorにMasterのDoor/AllClosed、Door/HasValidState、Door/HasFaultを設定すれば表示できる。試作用パネルも値を直接推測せずTIMS Busから読む。

## 確認内容

DoorPrototypeTestsで、Prefab/Builder/編成Simulation/速度測定/LocalBus/MasterBusを通して確認する。
対象：片側4か所、左右独立、途中の再開扉、開閉時間、正逆走での開扉禁止、速度欠損、拒否指令の非再生、1両の情報欠損、故障、監視無効化、不正な時間入力。

### 実行結果（2026-09-17）

- Unity 6000.4.0f1の別検証プロジェクトでEditModeテストを実行。ドア関連17件は全成功。既存分を含む全166件中165件成功。
- 残る1件は既存の `BrakeControlDeviceTests.TargetForceIsConvertedToCommonCylinderPressure`。期待250に対して250.000015となる厳密比較の失敗。同じコミット `cbc9e03` の変更前コードだけを別プロジェクトへ展開しても同じ失敗を再現した。今回ブレーキやそのテストは変更していない。
- 試験対象C#と作業フォルダの内容一致、追加Assetのmeta/Prefab参照、`git diff --check` を確認。
- Gameビューでの操作パネルの目視確認、実車両3Dとの接続、PlayMode/実ビルドの確認は未実施。
