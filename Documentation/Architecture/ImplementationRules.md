# Nakatetsu実装ルール

この文書は、人間とAIが実装・移植・レビューを行うときの共通ルールである。新しいコードを書く前に本書と、対象機能にある既存コードを確認する。ディレクトリの詳細は [DirectoryStructure.md](DirectoryStructure.md)、移植固有の判断は `Documentation/Migration` を参照する。

## 1. 作業とGit

- 作業前に現在のブランチと `git status` を確認する。
- ユーザーの未コミット変更、未保存Scene、Prefab、制作元ファイルを上書き・破棄しない。
- `/Users/yudai/TD-ATC` は移植元として読み取り、明示的な依頼なしに変更しない。
- Unityアセットの追加・移動・削除では対応する `.meta` も同時に扱う。
- 対象外のファイルを一括整形、一括ステージ、ついで修正しない。
- 旧挙動の移植と仕様改善は分け、互換性を変える場合は理由と比較テストを残す。

## 2. ディレクトリ

- `Assets/Nakatetsu` 以下は機能・ドメインを先に分ける。
- ファイル種別は機能階層の末端に `Scripts`、`Interfaces`、`Data`、`Prefabs`、`Tests`、`Sprites` などとして置く。
- 機能の実装と公開契約を分ける場合は、同じ機能ディレクトリ内で `Scripts` と `Interfaces` を同じ階層に置く。すべての機能のInterfaceを1つの共通ディレクトリへ集めない。
- `Logic`、`Context`、`Controller` はファイル種別ではない。ファイルが少ない段階で同名の空フォルダを作らない。
- Unityで使う完成アセットは `Assets`、`.blend`、`.ai`、`.psd` などの制作元は `SourceAssets`、設計・参考資料は `Documentation` に置く。
- 具体的な車種・路線データと、再利用可能な仕組みを分ける。

## 3. モジュールと依存方向

基本の依存方向は次のとおりとする。

```text
Core
↑
├── Track
├── Train
│   ↑
│   └── Tims
│       ↑
│       └── Presentation
├── World
└── Application / 統合用Simulation
```

- `Core` はTrain、TIMS、Trackなどの具体的な機能を参照しない。
- 一般的な車両機器はTIMSの型を参照しない。
- TIMSと車両機器の接続は、TIMS側または両方へ依存できる統合AssemblyのAdapterで行う。
- 相互参照で解決しない。値、利用側が所有するインターフェース、Adapter、上位Controllerで境界を作る。
- 上位のOrchestrationは下位Controllerを直接参照してよいが、下位ControllerからOrchestration Controllerを参照しない。必要な入力データまたは専用Interfaceを上位から接続する。
- 車載装置同士の情報伝達はTIMS Busを介する。Simulationによる装置出力の収集、Physicsへの力の入力、PhysicsからDynamicsへの物理結果の受け渡しは車載通信ではないため直接接続を許可する。
- `Train/Simulation` からTIMS型を参照する場合は、`Nakatetsu.Train`へ逆参照を追加しない。TrainとTimsの両方を参照する独立asmdefにするか、接続を`Application`へ置く。
- Presentationは表示だけを担当し、ブレーキ配分、力行力、運動などのシミュレーション計算を持たない。

## 4. Context・Logic・Controller

| 種類 | 責務 |
| --- | --- |
| `Input` | 1ステップ分の入力スナップショット |
| `Settings` | 計算中に変化しない設定値 |
| `State` | ステップをまたいで保持する状態 |
| `Workspace` | 再利用する配列、キャッシュなどの内部作業領域 |
| `Output` | 1ステップの計算結果 |
| `Context` | 上記の実行時データを所有する |
| `Logic` | 計算、判定、State更新。`static`を基本とする |
| `Controller` | Unity参照、入力収集、Logic呼び出し、結果反映 |

- 必要のない区分をクラス名合わせのために作らない。
- LogicはGameObject、MonoBehaviour、Scene、Prefab、ScriptableObject、Busを参照しない。
- Logicから `FindObjectOfType`、`GetComponent`、`Time.time`、`Time.deltaTime` を使わない。
- ContextにはController、GameObject、TransformなどのUnityオブジェクト参照を入れず、計算に必要な値を入れる。
- LogicはContextのStateとOutputを更新してよいが、外部オブジェクトへ副作用を起こさない。
- ControllerはContextを所有し、外部状態をInputへ写し、Logicを呼び、Outputを外部へ反映する。
- 時間進行へ参加するControllerは `Simulation/Orchestration/Interfaces/ISimulationController` を実装し、`CollectInput()`、`Calculate(deltaTimeSeconds)`、`ApplyOutput(deltaTimeSeconds)` に責務を分ける。
- `CollectInput()` は外部値をContext.Inputへ写す。`Calculate()` は収集済みInputだけを使ってLogicを呼ぶ。`ApplyOutput()` は計算済みOutputを子装置、Bus、公開状態などへ反映する。
- `ITractionEquipment` などの装置固有Interfaceには実行ライフサイクルを含めない。装置の能力・状態・装置固有操作だけを定義する。

## 5. Definitionと実行時設定

- ScriptableObjectのDefinition Assetは編集・保存用の定義とする。
- `ConsistDefinitionAsset` は編成GameObjectの `TrainRoot` だけが保持する。Builder、Simulation、TIMSなどは親の `TrainRoot` から同じ定義を参照し、個別にDefinition欄を持たない。
- LogicはDefinition Assetを直接参照せず、Controllerが値をContext.Settingsへコピーする。
- 実行中にDefinition Assetを書き換えない。
- 車種固有値は具体的なData Assetへ、計算方式は再利用可能なLogicへ置く。
- 同じ値を複数のDefinitionへ重複保持しない。所有者を1つ決める。

## 6. Adapter

- Adapterは異なる機能間のデータ形式、ID、車両Index、単位、欠損状態を変換する。
- Adapterは両側の型を知ってよいが、各機能のLogicへ相手側の型を持ち込まない。
- Adapterはブレーキ配分、圧力計算、牽引力計算などのドメイン計算を持たない。
- Adapterは自分の `Update` / `FixedUpdate` で時間を進めない。
- AdapterはControllerの `CollectInput`、`Calculate`、`ApplyOutput` を呼ばない。指令値の取得・変換または状態の公開までとする。
- 指令がない状態を暗黙に0へ変換せず、`Try...`、状態enum、結果型などで呼び出し側へ伝える。
- 入力Adapterと状態送信BusSourceの責務が大きくなる場合は別コンポーネントに分ける。

## 7. Simulationと実行ライフサイクル

- Unityの `Update` / `FixedUpdate` から時間進行を開始するのは、編成の最上位にあるSimulation Controllerだけとする。各機器が個別に時間を進めない。
- Simulation ControllerがUnityの時間を取得し、対象Controllerへ同じ `deltaTimeSeconds` を明示的に渡す。
- 各更新グループでは、対象Controller全体の `CollectInput()` を完了してから、全体の `Calculate()`、全体の `ApplyOutput()` を順番に呼ぶ。Controllerごとに三段階を連続実行して暗黙の順序依存を作らない。
- 装置内部の子要素は親装置の `ApplyOutput()` が進める。例としてBrakeControlDeviceがBrakeCylinderを、VVVFがMotorを更新する。Simulationは子要素を直接進めない。
- Physicsも `ISimulationController` を実装する。ただし装置の出力が確定した後に別の更新グループとして実行する。
- PhysicsはTrainSimulationControllerを直接参照しない。TrainSimulationControllerが所有する車両別入力Listを `SetInputSource()` で一度接続し、Physicsの `CollectInput()` が毎ステップ集計する。List自体を置き換えず中身を更新する。
- 指令欠損時も時間応答を止めない。前回指令維持、緩解、安全側制動などの方針を明示してControllerを1回進める。

車両シミュレーションの基本更新順は次のとおりとする。

```text
現在の線路状態・勾配を取得
→ 操作・保安・TIMSの指令を決定
→ Adapterで車両別の値へ変換
→ VVVF・ブレーキなどの装置をCollectInput / Calculate / ApplyOutput
→ 実際の力と車両質量をSimulationが収集
→ PhysicsをCollectInput / Calculate / ApplyOutputして加速度と符号付き速度を更新
→ Dynamicsが速度から符号付き移動距離を求めてグラフ内を進める
→ 移動結果をTrackへ適用
→ 各車位置・占有・表示・Bus状態を反映
```

- フィードバックに前ステップ値を使う場合は明記する。暗黙の更新順依存にしない。
- Presentationの描画更新はSimulation結果を読むだけにし、シミュレーション状態を進めない。

## 8. VVVF・ブレーキ・Physics・Dynamics・Trackの境界

- VVVFは目標力、車速、時間を受け、実モーター力・回生力・電気状態を返す。
- Brakeは目標空制力と時間を受け、実空制力を返す。
- 将来のAdhesion層は機器が発生した力を、レールへ伝達できる力へ制限する。
- Physicsは車両別の牽引力、非負のブレーキ力、外力、質量を集計し、編成の加速度と符号付き速度を計算する。物理状態の所有者はPhysicsとする。
- PhysicsのStateを書き換えるのはPhysicsのLogicだけとする。外部は計算結果としてOutputを読み、Stateを直接変更しない。現段階では専用のPhysics Output Interfaceを必須としない。
- DynamicsはPhysicsの符号付き速度から移動距離を求め、グラフ内を進む処理を担当する。装置の力計算や速度更新を持たない。
- Trackは符号付き移動距離を受け、Edge上の位置、実移動距離、未消費距離、終了理由、各車姿勢を返す。
- PhysicsはVVVF、Brake、Track、TrainSimulationControllerを参照しない。Contextへ集計済みの値を渡す。
- TrackはDynamicsを参照せず、時間や速度から距離を再計算しない。
- 速度更新はPhysics、距離積分はDynamicsでそれぞれ1回だけ行い、Trackは渡された距離だけを適用する。
- 車載装置が利用する速度は、最終的には速度発電機などの物理センサーからLocalBus、TIMSのMasterBusを経由して配る。車載装置はPhysicsを直接参照しない。センサー完成前のSimulationから装置への速度Setterは暫定接続として扱う。
- 編成の向きと移動方向を分ける。後退だけで車両順やEdge上の編成前方を反転させない。

## 9. 単位と符号

- Runtimeの基本単位はSIとする。距離m、時間s、速度m/s、加速度m/s²、質量kg、力N、圧力kPaを使う。
- 単位を持つ変数名には `M`、`Seconds`、`Mps`、`Mps2`、`Kg`、`N`、`KPa` などを付ける。
- 配列とListに車両順・装置順の意味がある場合は定義箇所へ明記する。
- 編成前方へのモーター力を正、反対を負とする。
- 空気ブレーキ力は非負の大きさとして渡し、Physicsが走行中は現在速度の逆向き、停止付近では動こうとする非ブレーキ力の逆向きに作用させる。
- 停止付近で非ブレーキ力の絶対値がブレーキ保持力以下なら、Physicsは速度と加速度を0にする。保持力を超える場合は差分の力で動き始める。
- 0付近でブレーキや抵抗により速度の符号を飛び越えない。

## 10. エラーと状態

- null、無効なID、車両Index範囲外、タグ欠損を暗黙に0やfalseへ読み替えない。
- 失敗し得る処理はboolだけで情報が不足する場合、終了理由や診断を結果型へ含める。
- Track終端では実移動距離、未消費距離、終了理由を返す。
- Logicはログを出さず結果を返し、Controllerが対象Unity Objectとともにログを出す。
- 無効なOutputをTransformへ適用して原点へワープさせない。

## 11. 命名とコード

- namespaceは `Nakatetsu` から始め、担当ドメインに合わせる。
- 型、publicプロパティ、publicメソッドはPascalCase、privateフィールドは既存コードに合わせてcamelCaseを使う。
- Inspector参照は `[SerializeField] private` を基本とする。
- `Controller`はUnity接続、`Logic`は計算、`Context`は実行時データ、`Adapter`は機能間変換を表す。
- 名前をそろえるためだけの空クラスや空フォルダを作らない。
- public APIは責務を表す小さな単位にし、Context全体の公開は必要な場合に限定する。

## 12. 確認

- 変更ファイルが意図したasmdef配下にあり、参照が循環していないことを確認する。
- Unityアセットと `.meta`、Prefab・ScriptableObjectのGUID参照を確認する。
- Logicは単体テスト、Controller・Prefab・更新順は必要に応じてPlayModeまたはSandboxで確認する。
- `git diff --check` と `git status` で、対象外の変更が混ざっていないことを確認する。
- Unityを操作する場合も、ユーザーの未保存Sceneを保存・破棄しない。
