# 線路基盤の移植計画

作成日：2026-09-07。対象は旧 `TD-ATC` の線路基盤と、それを利用する車両位置計算。
この文書は全体の実装計画。走行基準GuideLineの先行移植を実施した。実装済みの型・検証・使い方は [GuideLine移植記録](GuideLineMigration.md) を参照。以下の全段階が実装済みという意味ではない。

**採用方針：ユーザーの決定により、オフセット方式を基本にする。** まず走行基準GuideLineを移し、次にオフセット評価、グラフ接続を移す。独立線形の検討は調査案として残すが、実装の前提にはしない。分岐付帯曲線の描きやすさは作成補助で改善する。

参考調査：[BVE/OpenBVEの線形入力](TrackAuthoringResearch.md)、[線路グラフを持つシミュレーター](TrackGraphSimulatorResearch.md)。独立線形に関する提案より、上記の採用方針を優先する。

**GuideLineの責務変更：カントとその計算用軌間を廃止した。** 基準線は平面線形・高さ・勾配のみを評価する。下記の旧実装のカント記述は調査記録であり、新GuideLineの仕様ではない。将来の実線路側でのカントは別機能として検討し、旧基準線への非ゼロカント適用との一致は要求しない。

## 1. 最初に何を作るか

**旧プロジェクトの線形計算を活かして「直線1本に車両1両を置き、距離を変えると動く」状態を最初の到達点にする。**

ブレーキ・VVVF・TIMS・駅・信号・レールメッシュをすべて揃える必要はない。仮の一定速度と箱の表示で、位置計算を先に確認する。ただし、将来捨てる独自の直線移動を別に作るのではなく、旧方式の Geometry → Edge → 距離評価を最小のデータで通す。

実装順は次のとおり。

1. 線形・グラフの最小データと距離から位置・姿勢を求める計算。
2. 直線上の1両の位置更新と表示。
3. 複数Edgeの移動と複数車両の配置。
4. 旧JSONの読み込み、曲線・勾配・カント・オフセット線形の比較。
5. 分岐の選択状態と、編成が通った経路の保持。
6. 運動・装置・TIMS、閉塞・地上設備、景観との接続。

## 2. 調査対象と現在地

| 項目 | 確認結果 |
| --- | --- |
| 旧プロジェクト | `/Users/yudai/TD-ATC`。参照のみ |
| 新プロジェクト | `/Users/yudai/Documents/Unity/Nakatetsu` |
| 旧HEAD | `0913d6eeb74f4743664b3603de2564110fe86b07` |
| 新側の調査時ブランチ | `migration/Tims`。線路用ブランチへの切り替えはこの作業では行っていない |
| 新側のTrack | `Assets/Nakatetsu/Track/Geometry/Scripts/Nakatetsu.Track.asmdef` とGuideLine実装がある |
| 新側の車両定義 | `CarDefinitionAsset` と `ConsistDefinitionAsset` がある |
| TIMS | Logic・Contextと、編成両数からLocalBusを作る通信Controllerがある |

旧HEADだけでは作業ツリーの内容を特定できないため、調査対象のハッシュを [TrackSourceHashes.json](TrackSourceHashes.json) に記録する。これは調査時の参照資料で、旧コードの動作保証ではない。

現在の車両定義には `lengthM`、`bogieCenterDistanceM`、質量、車種、機器Prefab参照がある。編成は `List<CarDefinitionAsset>`。旧定義の `TryGetCar`、`HasCars`、車体Prefab、運転台定義などをそのまま使える状態ではない。

## 3. 旧実装の仕組み

### 3.1 GeometryとEdgeは別のもの

| 型 | 役割 |
| --- | --- |
| `TrackGeometry` | 基準線の原点、方位、水平線形、勾配、カント、軌間 |
| `TrackEdge` | 実際に走る区間。両端Node、基準線ID、横オフセット、区間長、閉塞など |
| `TrackNode` | Edge同士の接続点 |
| `TrackOffsetSegment` | 基準線に対してどれだけ横にずれるかを表す区間 |
| `TrackOffsetDistanceMap` | 実際の線路上の距離を、基準線上の距離へ変換する表 |
| `TrackRuntimeResolver` | 上記を使って位置・接線・姿勢、勾配・カントを取得する |

複線や渡り線では、基準線と実際の線路の長さは一致しない。旧方式は次の順に位置を求める。

```text
EdgeのID + Edge上の距離[m]
          ↓ 距離変換表
基準GeometryのID + 基準線上の距離[m]
          ↓ 水平線形、勾配、カントの評価
基準位置・姿勢
          ↓ 姿勢の右方向 × 横オフセット
実際の線路の位置・姿勢
```

`TryResolvePose` はオフセットEdgeを評価する作りになっている。**オフセット0の直線でも `baseGeometryId` と有効な距離変換表が必要**。単に `lengthM` だけを入れたEdgeでは旧Resolverを使えない。

`TrackOffsetDistanceMapBuilder` は位置を小刻みに評価し、3D位置間の距離を積算する。末尾には等間隔より短いサンプル区間ができるので、表の要素数だけから長さを決め直してはいけない。

### 3.2 線形と単位

基準線の水平線形は Straight / Curve / TransitionIn / TransitionOut。緩和曲線は旧コードの三次式による近似であり、移植と同時に別の厳密な線形へ置き換えない。

オフセットには Constant / Linear / Cubic / Arc / Transition がある。基準線のTransitionとオフセットのTransitionは別実装なので、同じ計算へ統合しない。

| 値 | 旧方式での意味 |
| --- | --- |
| 座標 | UnityのY上向き、初期方位0でZ前方、X右方向 |
| `distanceOnEdgeM` | Node Aからの距離。逆走中もBからの距離へ変更しない |
| `baseDistanceM` | 基準線のパラメーター距離。Edgeの実長と区別する |
| 速度 | Runtimeではm/s。JSONの`speedLimitKmH`を読み込み時に3.6で割る |
| 勾配 | ‰。高さ計算では1000で割る |
| カント | mm。姿勢計算ではmへ変換し、軌間で角度にする |
| 半径 | 符号付き。正負の曲線を比較して向きを維持する |

オフセットは水平なX方向固定ではなく、基準姿勢の `Vector3.right` 方向に加算される。カントがある場合の高さへの影響も旧比較に含める。

### 3.3 JSONからグラフを作る流れ

```text
TrackGraphEditor
  → TrackJsonLoader（ファイルを読む／JsonUtilityで変換）
  → TrackJsonCompiler
      → TrackGeometryJsonCompiler
      → TrackEdgeJsonCompiler（距離表、端点、閉塞データ）
      → 接続点の統合、分岐接続の作成、グラフ検証
  → TrackGraph.assetを保存
```

主なデータは旧 `Assets/Data/Track/Track.json`、スキーマは `Assets/Data/Schemas/Track.schema.json`。調査時のJSONはversion `2.0`、基準Geometryは1件。`trackGroups` の中に線路・接続が入っている。`Assets/Data/Legacy/TrackLegacy.json` は今回の基準入力にしない。

JSONのGeometryの `name` がRuntimeの `geometryId` になる。空なら `GEO_i` を生成する。既存の路線ID・Geometry名・Edge ID・接続IDは最初の移植では保持する。

旧コンパイラーは `CompileInto` で既存グラフを変更し、失敗をログに出しながら処理を続ける箇所がある。新側では一時データへコンパイルし、検証成功時に保存先へ反映する形を推奨する。

### 3.4 車両位置と向き

旧 `TrainTrackState` は `currentEdgeId`、`distanceOnEdgeM`、`currentDirection` を保持する。

**旧コードのheadは、先頭車の中心を基準にしている。** 先頭車前端ではない。`TrainConsistResolver` は次のように配置する。

- 先頭車の中心オフセットは0。
- 次の車両中心までは「前車の長さと自車の長さの半分ずつ」を加算する。
- 先頭車前端は基準点から先頭車長の半分だけ前。
- 編成後端は最後の車両中心オフセットに、その車両長の半分を加えた位置。

20m車を2両なら、中心は基準点と20m後方、編成の端は10m前方と30m後方になる。初期配置時は車両の全長が線路上に収まる距離を確保する。

`currentDirection` は編成前方がそのEdge上で向いている方向。速度の正負から求める実際の移動方向とは分ける。後退しただけで編成の並びを入れ替えない。

旧 `TrainTrackResolver` が経路移動・各車の位置・勾配・閉塞まで処理し、旧 `TrainCar.Update` が出力をTransformへ反映する。現状の旧車体表示は車両中心の線路姿勢を使っており、前後台車の2点から車体姿勢を作る方式ではない。

## 4. そのままコピーできない依存関係

| 旧コード | 問題 | 新側の方針 |
| --- | --- | --- |
| `TrackGraph : ScriptableObject` | 定義、探索、検証、分岐設定を同居 | Assetは定義の保管、検索用データと検証処理を分離 |
| `TrackGraphTraversal.ResolveTurnoutConnection` | `TurnoutController.TryGetActiveController` を呼ぶ | 分岐の検知状態を引数・Contextで渡す。LogicからControllerを探さない |
| `TrackRuntimeResolver` | 不要な`Unity.VisualScripting.FullSerializer`のusingがある | 使っていない参照を除去。線路のためだけに依存を追加しない |
| `TrainTrackContext` | `logContext`、`logName`、Unity Objectを保持 | 計算結果に失敗理由を持たせ、Controllerがログを出す |
| `TrainTrackResolver` | `TrainController.CabEnd`、旧編成定義、閉塞取得が混在 | 移動・配置を先に抽出。運転台問い合わせと閉塞取得は後段 |
| `TrainController` | TIMS・機器・運動・位置・表示をまとめて参照 | 線路移植のために丸ごと移さない |
| `BlockOccupancyController` | Trackから`TrainController`を直接参照・探索 | 後段でApplicationが車両の占有範囲をTrackへ渡す |
| `TrainFormationBuilder` | 車体、運転台、VVVF、Editor生成が混在 | 最初は手置きの箱。生成処理は別の段階で実装 |
| `TrackVisualizer`・景観生成 | PrefabやMaterial、生成設定を要求 | Worldへ移す候補。最初はGizmo等で線形を確認 |

`Train → Track` はC#の参照方向である。TrackはTrainやTIMSを参照しない。Trackに車両情報を渡す場合も、Track側で定義した位置・区間などのデータを使う。

## 5. 新しい配置案

以下は予定。必要な段階でファイルとフォルダを追加する。小さい機能の中にLogic/Context/Controllerという下位フォルダをさらに作る必要はない。

```text
Assets/Nakatetsu/
├── Track/
│   ├── Geometry/{Scripts,Data,Tests}/
│   ├── Graph/{Scripts,Data,Tests}/
│   ├── Route/{Scripts,Data,Tests}/
│   ├── Block/{Scripts,Data,Tests}/
│   └── Interlocking/{Scripts,Data,Tests}/
├── Train/
│   ├── Consist/Scripts/             # 現在の定義、編成の寸法・オフセット計算
│   └── TrackPosition/Scripts/       # TrainTrackContext、Logic、Controller、車体姿勢反映
├── Application/Sandbox/Scripts/     # 仮速度の入力、初期化・接続
├── World/Track/Scripts/             # 後段のレール・架線等の生成
└── Track/NtLine/
    ├── Import/Data/                 # 旧形式のTrack.jsonとスキーマ
    └── Graph/Data/                  # 再生成したTrackGraph.asset

Assets/Scenes/Sandbox/TrackSandbox.unity
```

namespaceは `Nakatetsu.Track.Geometry`、`.Graph`、`.Route`、`.Configuration`、`Nakatetsu.Train.Track`、`Nakatetsu.Train.Presentation.Track` など。Runtimeはnamespaceに入れない。Editorのインポート処理は `Nakatetsu.Track.Editor.Import` とする。

既存のasmdef名と参照は維持する。Editor追加時は対象機能の末端にEditor専用フォルダとasmdefを置き、Trackを参照させる。テストも機能ごとの末端にある `Tests` へ置く。

現在の参照方向を維持する。

```text
Application → Train.Presentation → Train → Track → Core
Application → Train.Tims → Train
World → Track
Track.Editor → Track
```

これは主要な依存のみを示す。今回Coreへ新しい型を集める必要はない。TrackはすでにUnityEngine参照を許可しているため、位置計算でVector3/Quaternionを使ってよい。Logic分離の目的は、Transform操作・シーン探索・アセット保存などを計算から外すこと。すべての計算をUnityEngine非依存へ書き直すことではない。

### 5.1 旧ファイルと移植先

旧パスは `Assets/Scripts/` 基準、新パスは `Assets/Nakatetsu/` 基準。

| 旧ファイル・機能 | 新しい置き場所／処理 | 段階 |
| --- | --- | --- |
| `Track/Geometry/TrackCurveDefinitions.cs`、`TrackGeometry.cs` | `Track/Geometry/Scripts`。データ型を保持 | A |
| `TrackGradientCalculator.cs` | 同上。単位・境界の計算を保持 | A・Dで検証 |
| `TrackOffsetSegment.cs`、`TrackOffsetCalculator.cs` | 同上。Segment内の計算を必要に応じCalculatorへ抽出 | A・D |
| `TrackOffsetDistanceMap.cs`、Builder | 同上。末端補間・生成条件を保持 | A・D |
| `TrackRuntimeResolver.cs` | 同上。まず計算を保持し、純粋なグラフデータを引数にする | A |
| `Track/Graph/TrackNode.cs`、`TrackEdge.cs`、`EdgeTravelDirection.cs` | `Track/Graph/Scripts` | A |
| `TrackGraph.cs` | `Graph/TrackGraphData.cs`、検索用`TrackGraphContext.cs`、`TrackGraphValidator.cs`、`Configuration/TrackGraphAsset.cs`へ責務分割 | A・D |
| `TrackGraphTraversal.cs` | `Track/Route/Scripts`。Controller探索を除去 | C・E |
| `Track/Route/TrackRouteTracer.cs`、`TrackTraceSegment.cs` | `Track/Route/Scripts`。追跡距離と終了理由を返す | C |
| `Track/Block/BlockDefinition.cs` | `Track/Block/Data`。`BlockSection`等のデータだけ先行可能 | A・D |
| `Track/Interlocking/Turnout/TurnoutDefinition.cs`、`TurnoutConnection.cs` | `Track/Interlocking/Turnout/Data`。定義と可変状態を分離 | D・E |
| `Serialization/Json/TrackLayoutJson.cs`とTrack系コンパイラー・Loader | `Track/Import/Editor`。読み込みとデータ変換を分離 | D |
| `Editor/Track/TrackGraphEditor.cs` | `Track/Import/Editor`。成功時のみAsset保存 | D |
| `Train/Track/TrainTrackContext.cs`、Resolver | `Train/Track/Scripts`。Context・Logicへ抽出 | B・C |
| `Train/Track/CarTrackOutput.cs` | `Train/Track/Scripts`。各車の有効性を追加 | B・C |
| `Train/Consist/TrainConsistResolver.cs` | `Train/Consist/Scripts`。現在の定義から寸法Inputを作る | C |
| `Train/Consist/TrainCar.cs`のTransform反映 | `Train/Track/Visuals/Scripts` | B |
| `Track/Scenery/*` | `World/Scenery/Scripts`へ段階的に移す | F以降 |

上記の新しいファイル名は提案。`TrackRuntimeResolver`のような意味の通る名前を、統一のためだけにすべてLogicへ改名する必要はない。

## 6. Logic・Context・Controllerの境界

### 6.1 線路側

`TrackGraphAsset` は固定データを保存する。初期化時に実行用のデータとID検索用辞書を組み立てる。実行中は読み取り専用として扱い、列車位置や分岐の転換中状態を書き込まない。可変の入れ子Listを共有したまま編集するとAssetに影響するので、コピーするか、変更を許さないAPIで扱う。

`TrackGraphContext` のWorkspaceはID検索等のキャッシュを持つ。キャッシュは読込・再コンパイル時に再構築する。位置問い合わせのたびにグラフ全体をコピーしない。

線形のCalculatorは入力に対して位置・姿勢を返すだけなので、空のStateやControllerを追加しない。分岐の動的状態が必要になった時点でContextを用意する。

### 6.2 車両側

| 分類 | 入れるもの |
| --- | --- |
| State | 基準車両中心のEdge ID、Aからの距離、編成前方向 |
| Input | 今回の符号付き移動距離、編成各車の寸法、必要な線路参照 |
| Settings | 初期Edge・初期距離・初期方向など |
| Output | 各車の位置・姿勢・有効性、移動できた距離、終端到達、失敗理由 |
| Workspace | 経路追跡の再利用List。分岐対応時は通過経路の記録 |
| Logic | 移動距離を適用し、Edge遷移と各車位置を計算 |
| Controller | 定義からInputを作る、Logicを呼ぶ、結果を公開 |
| Presentation | Outputの位置・姿勢を箱や車体のTransformに反映 |

最初の一定速度入力はSandbox用の呼び出し側が `速度[m/s] × deltaTime[秒]` で移動距離を作る。線路Logic内でTimeやTIMSを直接読まない。

将来は運動計算が移動距離を渡す。時間と距離を両方渡して線路側でも積分すると二重更新になるため、位置更新の責任は一本化する。

旧コードはEdge遷移の向きに速度の符号を使っている。新APIでは移動距離の符号と整合させ、停止直前・微小速度・後退時に逆方向の端を調べないようテストする。

## 7. 段階別の実装と完了条件

### A：定義と位置評価

作業：最小グラフデータ、線形定義、Resolver、距離表を移植する。Graphから駅・動的な分岐Controllerへの依存を外す。Edgeが持つ閉塞データ型は、機器実装なしで先行移植してよい。

確認データ：原点(0,0,0)、方位0、100m直線、勾配0、カント0、オフセット0。Node A/BとEdgeを1本用意する。最初はテスト内やSandbox初期化で定義し、JSON importerはまだ不要。

完了条件：距離0/25/100mで位置(0,0,0)/(0,0,25)/(0,0,100)、前向き接線、期待姿勢。未知ID・空データで失敗を返す。長さと距離表が一致する。

最初のコミットはここまででよい。車両Controller、TIMS設定、機器Prefabには依存させない。

### B：直線で1両を動かす

作業：TrainTrackContext/Logic、薄いController、出力を反映するPresentationを用意する。旧TrainControllerをコピーしない。

例：20m車の中心を20mに置き、仮速度5m/sで2秒進めると中心は30mになる。最初は車両中心の姿勢を箱へ適用する。

```text
TrackSandbox
├── SandboxRunner             # 定義読込、初期化、仮の移動距離、更新順
├── TrackDebug                # 線形の可視化
├── PlayerTrain
│   ├── Simulation            # TrainTrackController
│   └── Presentation
│       └── Car01             # 箱と姿勢反映
├── Camera
└── Light
```

完了条件：停止・前進・後退できる。フレームレートを変えても同じ総移動距離になる。終端で無限に距離を増やさず結果を返す。無効なOutputで原点へワープさせない。

終端処理は、当初は基準点の終端Clampでよい。その場合、車体前端が線路外に出る制約が残ることをSandboxに明示する。編成全体を収める処理はCで追加する。

### C：複数Edgeと複数車両

作業：Traversal・Tracer・編成寸法計算を移植。非分岐の直列接続で、余った移動距離を次Edgeへ持ち越す。初期位置はEdge・距離・方向で明示する。

前方向・後方向の両方で、A→A、A→B、B→A、B→Bの端点接続を確認する。Edgeの向きと編成の向きを混同しない。

20m車2両で中心間20mを確かめる。線路不足の場合は追跡結果に必要距離未達を出し、末端に複数車両を重ねて配置しない。

完了条件：Edge境界を跨いでも位置が飛ばない。後退しても車両順序と車体前方が勝手に反転しない。大きな1ステップで複数Edgeを跨げる。空編成・null車両定義・長さ0は初期化エラーとして検知する。

台車中心間距離を使う2点評価は、この段階の追加項目にする。前後台車中心を経路上で求め、両点から車体の向きを作る。単純な中心接線方式との見た目の差が出るので、旧互換の移植とは別コミットで実装・比較する。カントの上方向も補間し、接線だけでロールを消さない。

### D：旧JSONと複雑な線形

作業：まず短い直線JSON、その後曲線・勾配・オフセット、最後に旧Track.jsonをコンパイルする。旧TrackGraph.assetの丸ごと流用は前提にしない。

旧の主コンパイラーは距離表生成にサンプル間隔0.05m、積分刻み0.005mを渡している。Builder単体のデフォルトとは異なる。旧比較では主コンパイラーの値を使い、性能改善は別に測定する。

旧データには分岐・閉塞があるため、この段階で定義と接続の読み込みは必要。ただし動的な転換や閉塞占有Controllerは不要。分岐の走行はEまで行わず、位置評価とデータ検証を先に通す。

完了条件：ID集合・Edge長・端点・境界周辺・勾配・カントが旧出力と比較できる。エラーのあるimportで既存Assetが半分だけ更新されない。既存JSONを読んだだけで、未対応項目を黙って消さない。

### E：分岐と通過経路

作業：定義の定位・反位接続と、現在の検知状態を分離する。Traversalへ検知状態を渡す。Sandboxでは明示的な固定選択から始める。

旧コードは分岐転換中・故障中で位置を検知できないと接続先なしを返す。この振る舞いを保持し、無条件に先頭の接続先へ進ませない。

複数車両の後方位置を毎回「現在の分岐状態」だけで追跡すると、先頭通過後の切り替えで後部車両が別経路へ移る可能性がある。編成が通ったEdge列と向きをWorkspaceに保持し、編成が跨いでいる区間の配置に使う。Sandboxでも編成通過中の切り替えを禁止する。後退時にも通過経路を辿れることを確認する。

完了条件：定位・反位で意図した経路に進む。未検知では進まない。前後移動と編成の分岐跨ぎで経路が変わらない。連動・進路鎖錠の本実装は別段階。

### F：運動・機器・TIMSへ戻る

線路から位置・各車の勾配を取得できたら、仮速度を車両の運動計算へ置き換える。その後ブレーキ／力行装置とTIMSを接続する。

更新順の提案：現在位置の勾配等を読む → 装置と制御を決めた順に更新 → 運動計算 → 移動距離を線路へ適用 → 各車位置と占有範囲を更新 → 表示へ反映。制御が前回値と今回値のどちらを使うかは、装置接続時に明示する。

閉塞占有へは先頭・後端のIDだけでなく、編成が占有する経路上の全区間を渡す。車両が複数閉塞に跨ぐケースを確認する。駅・地上ATC・景観はこの基盤を利用して順次移植する。

## 8. 移植時に明示的に扱う旧挙動

| 確認した挙動・懸念 | 対応方針 |
| --- | --- |
| Tracerは行き止まりで必要距離に届かなくてもtrueを返す | `tracedDistanceM`と終了理由を返し、完全到達と部分追跡を分ける |
| 後方位置取得は最後に得たSegmentを使う | 車両配置では必要距離未達を有効な位置として使わない |
| 位置評価に失敗した車両の旧値が残り得る | 各車Outputに有効性を付け、更新ごとに状態を確定する |
| Resolverの姿勢は基本的にEdgeのA→B向き | B→Aの編成前方に合わせた姿勢変換を車両側で明示する。カントも確認 |
| Traversalは通常Nodeで最初に見つかった接続を採用 | 接続候補が複数なのに分岐定義がない場合は検証エラーにする |
| 移動・追跡に256回のガードがある | 維持した上で上限到達を失敗理由に含める。ゼロ長循環を許さない |
| 勾配取得は基準線の勾配を返す | 可変オフセットの実際の傾斜と厳密一致するとは限らない。初回は旧互換、物理精度改善は別件 |
| 全Constantオフセットでは基準姿勢を使う | 高さ・カント変化を含むケースを比較し、精度変更を移植に混ぜない |
| JSONの未知水平線形はstraightへフォールバック | 新importでは診断を返す。未知形式を成功扱いにしない方針 |
| JSONの`signals`はDTOでTODO | 読込成功を信号実装完了と扱わない |

これらはコード読解からの指摘であり、現段階でUnity上で再現試験を済ませた不具合一覧ではない。旧互換から変える項目は、旧値を保存した比較テストと変更理由を同じコミットに含める。

## 9. アセット・Scene・Prefabの扱い

旧Mainシーンを最初の検証シーンとして移植しない。旧TrainController・UI・音・機器等の参照が一度に必要になるため、独立したTrackSandboxで確認する。

旧JSONを新側のSourceへコピーし、`$schema`の相対パスを新しい配置に合わせる。固定入力の元データはJSON、実行時の読込対象はそこから作るGraph Assetを基本とし、二重に手修正しない。ゲーム実行中のJSON読込が必要になった時点で、DTOとコンパイラーをRuntimeへ移すことを検討する。

旧スクリプトを改名・分割した新Assetは、新しい型で再生成する。旧`TrackGraph.asset`は参照比較用に残す。Unityで使うPrefab・Material等を後からコピーする際は依存アセットと`.meta`を確認し、GUID衝突とMissing Scriptを検査する。既存の新プロジェクトの`.meta`を上書きしない。

実際のレールメッシュや架線を作る段階では、線形の評価APIをWorldが呼ぶ。TrackがWorldの生成Controllerを参照する形にはしない。

## 10. 検証計画

今回実施したのはソース・定義・JSON構造の調査と文書作成。以下は移植時に実施する検証であり、実行済みの結果ではない。

| 分類 | 主なケース |
| --- | --- |
| Geometry | 直線、正負の円曲線、緩和曲線の両端、原点移動・yaw |
| Vertical/Cant | 一定勾配、勾配変化、区間前後、正負カント、ゼロ長 |
| Offset | 0・一定・変化オフセット、全曲線種、開始基準距離が0以外 |
| DistanceMap | 単調性、始点・終点、間隔未満の短区間、末尾端数、空表 |
| Graph | 空・重複ID、参照切れ、端点接続、分岐未定義、長さ不一致 |
| Movement | 前進・後退・停止、全端点接続向き、余剰距離、複数Edge、終端 |
| Consist | 1両・異なる長さの複数両、全長不足、分岐跨ぎ、出力無効化 |
| Import | 旧JSON、最小JSON、不正JSON、未知種別、失敗時に旧Asset維持 |
| PlayMode | 初期化順、箱の連続移動、再起動で状態初期化、Assetが変化しない |

旧方式と同じ入力・サンプル間隔で、各Edgeの0%、25%、50%、75%、100%と全線形境界の直前・直後を比較する。位置・Edge長は初期目標1mm、姿勢は0.01度を目安にするが、これは達成済み精度ではない。近似や浮動小数点の差が出たら最大誤差と地点を記録し、根拠なく許容値だけを広げない。

Quaternionは成分の一致ではなく角度差を比較する。勾配・カントの単位変換も別に確認する。旧の曲線近似そのものの誤差と、新旧間で増えた誤差を分ける。

EditModeは計算とデータ検証、PlayModeはControllerとSceneの接続を確認する。旧コード比較用のコピーが必要な場合も、旧プロジェクトを変更せず別の検証領域で行う。

## 11. コミットの切り方と最初の作業一覧

調査時点では `migration/Tims`。線路用ブランチを使う場合は、現在のコミットを起点に `feature/track-foundation` 等を作成してから実装する。TIMS側の作業を巻き戻す必要はない。

推奨コミット単位：

1. `Add track geometry and graph data`：Aの型と位置評価、その検証。
2. `Add single-car track sandbox`：Bの最小接続とシーン。
3. `Add edge traversal and consist placement`：C。
4. `Import legacy track JSON`：D、旧データとの比較記録。
5. `Add turnout state and traversal history`：E。

次回の実装は以下の範囲から始める。

- [ ] Geometry・Node・Edge・距離表の最小型を移す。
- [ ] GraphのAssetと実行用データを分け、Controller参照を持ち込まない。
- [ ] 不要なusingを除去し、namespaceを設定する。
- [ ] 100m直線・オフセット0の位置評価を確認する。
- [ ] 不正な参照・距離表の欠落が検知できることを確認する。

このチェックが終わってから、車両の移動と表示を追加する。

## 12. 主な参照元

リンクは調査したローカルの旧プロジェクトを指す。移植先の予定は5.1節に記載した。

- [TrackRuntimeResolver.cs](/Users/yudai/TD-ATC/Assets/Scripts/Track/Geometry/TrackRuntimeResolver.cs)：位置評価、座標・勾配・カント。
- [TrackOffsetDistanceMapBuilder.cs](/Users/yudai/TD-ATC/Assets/Scripts/Track/Geometry/TrackOffsetDistanceMapBuilder.cs)：距離表の生成。
- [TrackGraph.cs](/Users/yudai/TD-ATC/Assets/Scripts/Track/Graph/TrackGraph.cs)：定義、検証、検索。
- [TrackGraphTraversal.cs](/Users/yudai/TD-ATC/Assets/Scripts/Track/Graph/TrackGraphTraversal.cs)：接続先、方向、分岐Controller参照。
- [TrackRouteTracer.cs](/Users/yudai/TD-ATC/Assets/Scripts/Track/Route/TrackRouteTracer.cs)：経路追跡と終端処理。
- [TrainTrackResolver.cs](/Users/yudai/TD-ATC/Assets/Scripts/Train/Track/TrainTrackResolver.cs)：移動、車両位置、閉塞。
- [TrainConsistResolver.cs](/Users/yudai/TD-ATC/Assets/Scripts/Train/Consist/TrainConsistResolver.cs)：中心間距離と編成の端。
- [TrainCar.cs](/Users/yudai/TD-ATC/Assets/Scripts/Train/Consist/TrainCar.cs)：旧表示への反映。
- [TrackJsonCompiler.cs](/Users/yudai/TD-ATC/Assets/Scripts/Serialization/Json/TrackJsonCompiler.cs)：import全体。
- [TrackJsonRuntimeConverter.cs](/Users/yudai/TD-ATC/Assets/Scripts/Serialization/Json/TrackJsonRuntimeConverter.cs)：区間・単位・閉塞の変換。
- [Track.json](/Users/yudai/TD-ATC/Assets/Data/Track/Track.json)：比較対象の既存路線。
- [ディレクトリ構成](../Architecture/DirectoryStructure.md)：新側の上位配置規則。
- [TIMSロジック移植記録](TimsLogicMigration.md)：後段で接続するTIMSの移植記録。
