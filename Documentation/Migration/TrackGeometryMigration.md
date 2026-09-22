# 走行基準TrackGeometryの移植

命名履歴：2026-09-21に旧 `GuideLine` を `TrainGeometry` へ改名し、2026-09-22に型・ファイル・フィールド名を `TrackGeometry` に統一した。名前空間は `Nakatetsu.Track.Graph.Geometry`。Unityの `.meta` GUIDは維持し、旧 `guideLine`／`trainGeometry` フィールド名は `FormerlySerializedAs`、直前の型名は `MovedFrom` で移行する。

改名後はUnity 6000.4.0f1の隔離プロジェクトでEditModeテスト20件が成功した。既存の線形評価に加え、旧フィールド名のUnityアセット・Prefabを読み込み、IDとPreviewのアセット参照が保持されることを確認した。

2026-09-22の`TrackGeometry`への改名後も、Unity 6000.4.0f1の隔離プロジェクトでEditModeテスト22件が成功した。旧`guideLine`系に加え、`trainGeometryId`と`trainGeometry`の保存データからの読込みを確認した。検証結果は`/tmp/nakatetsu-geometry-rename.scimgz/results.xml`。本プロジェクト全体のScene・PlayMode検証は含まない。

実装日：2026-09-07。ブランチ：`feature/track-guideline`。

## 対象

旧 `TrackGeometry` を移植した。現在の名前空間は、配置先に合わせた `Nakatetsu.Track.Graph.Geometry`（2026-09-22更新）。Assembly名・`.meta` GUIDは変更していない。
景観用の `SceneryGuideLineRule` とは別の機能。旧プロジェクトは変更していない。

今回は基準線だけを扱う。オフセットEdge、距離変換表、Node接続、分岐、JSON読込、車両走行、レールメッシュ生成はまだ追加していない。
今後はこの基準線からオフセット線路を作る。独立線形方式へ変更する作業は含まない。

## カント廃止

TrackGeometryから `cantSegments`、`TrackCantSegment`、`CantMm`、カント補間・ロール適用を削除した。カント角計算にのみ使用していた `gaugeM` も削除した。基準線は平面線形と高さ・勾配を担当する。実線路側のカント機能は今回追加していない。

旧カントが非ゼロの姿勢とは意図的に異なる。既存の独自TrackGeometryアセットに保存したカント・軌間値は使われなくなる。付属サンプルからも該当フィールドを除去済み。

## ファイルと役割

現在のスクリプトは `Assets/Nakatetsu/Track/Graph/Geometry/Scripts/`、テストは `Assets/Nakatetsu/Track/Graph/Geometry/Tests/`。既存の `Nakatetsu.Track.asmdef` を使用する。

| ファイル | 内容 |
| --- | --- |
| `TrackGeometryDefinition.cs` | ID、名称、長さ、原点・方向、水平・勾配区間 |
| `TrackGeometryCurveDefinitions.cs` | 旧水平・勾配区間の読込み用データと曲線種別 |
| `TrackGeometryHorizontalSegment.cs` | 水平区間の抽象クラス。位置・既存の向き・未実装の微分関数の契約 |
| `TrackGeometryStraightSegment.cs` / `TrackGeometryCircularSegment.cs` | 直線・円曲線の具体型 |
| `TrackGeometryTransitionInSegment.cs` / `TrackGeometryTransitionOutSegment.cs` | 緩和曲線の入口・出口の具体型 |
| `TrackGeometryCalculator.cs` | 距離から位置・接線・姿勢を評価する静的処理 |
| `TrackGeometryVerticalSegment.cs` | 高さ変化量・微分を返す縦断区間の抽象クラス |
| `TrackGeometryConstantGradientSegment.cs` / `TrackGeometryLinearGradientSegment.cs` | 一定勾配・線形に変化する勾配の具体型 |
| `TrackGeometryProfileCalculator.cs` | 縦断区間の選択・高さの積算・区間外の勾配延長 |
| `Track/Shared/Scripts/TrackSample.cs` | Geometry・Edge共通の評価結果。距離、位置、接線、姿勢、勾配 |
| `TrackGeometryAsset.cs` | 定義をInspectorで編集・保存するScriptableObject |
| `TrackGeometryPreview.cs` | Sceneビューで基準線と指定地点の向きを描く確認用コンポーネント |

Definitionが固定入力、Sampleが出力で、Calculatorは状態を持たない。PreviewがUnityの描画接続を担当し、計算側からシーンやControllerを参照しない。

`TrackGeometryAsset`はUnityで保存・共有・Inspector編集するためのScriptableObjectで、`TrackGeometryDefinition`を保持する。Definitionは線形のID・寸法・区間などを持つ通常のC#クラスで、Calculatorへ直接渡せる。これにより計算やテストでScriptableObjectの生成が不要になる。DefinitionにはUnityの値型を使っており、Unity非依存や不変性を保証するものではない。AssetのDefinitionを計算中に変更しない。

## segment開始情報の保存（2026-09-08）

### 水平区間の抽象化（2026-09-22）

水平区間を`TrackGeometryHorizontalSegment`の派生型に分けた。`EvaluatePosition`はGeometry起点からの基準線距離を受け、区間始点のローカル座標と既存の近似角を返す。CalculatorとCompilerはこの共通関数を呼ぶ。既存の直線・円曲線・三次緩和曲線の数式は変更していない。

`EvaluateDerivative`は宣言と未実装の枠のみで、全4形状が`NotImplementedException`を送出する。解析微分・解析接線はまだ実装しておらず、現在の位置・姿勢評価からは呼び出さない。勾配側は今回抽象化していない。

`horizontalSegments`はリストを公開するプロパティになり、保存先は`[SerializeReference]`付きの`horizontalSegmentDefinitions`。旧インライン形式は隠しフィールドで読み込み、逆シリアライズ時に派生型へ変換する。新形式での保存後も型・順序・区間・半径を維持する。形状を選択・追加する専用Inspectorは含まない。

隔離Unity 6000.4.0f1環境で既存評価・旧形式からの移行と再保存・混在リストの再読込み・微分未実装の確認を含む68テストが成功した。結果は`/tmp/nakatetsu-w1-03-pr.nQRHqp/geometry-abstract-results.xml`。

### 縦断区間の抽象化（2026-09-23）

`TrackGeometryVerticalSegment`を抽象クラスにし、`TrackGeometryConstantGradientSegment`（一定勾配）と`TrackGeometryLinearGradientSegment`（勾配を線形補間、高さは二次式）を追加した。

- `EvaluateHeightDeltaM(S)`はGeometry起点からの距離を受け、区間始点からの高さ差[m]を返す。
- `EvaluateDerivative(S)`は区間内の`dy/dS`[m/m]を返す。‰ではない。端点では区間側の勾配を返す。
- 長さ0.001m以下は従来どおり積算対象から除外し、具体型の高さ・微分評価も0を返す。
- `ProfileCalculator`と`Compiler`は具体型の勾配フィールドではなく共通の評価関数を呼ぶ。最初の区間より前は0‰、隙間・末尾は直前の終点勾配を延長する。
- 形状の数式は具体型へ移動し、旧`GetHeightDeltaM`と4引数の`GetGradientPermilleAt`は削除した。リスト全体を評価するAPIは維持する。

`verticalSegments`もリストを公開するプロパティにし、`[SerializeReference]`の`verticalSegmentDefinitions`へ保存する。旧インライン形式は隠しフィールドで読み込み、一定勾配を含めて`TrackGeometryLinearGradientSegment`へ移行する。開始距離・長さ・始終点勾配・順序を維持する。水平区間がない旧データも移行できる。専用Inspectorや姿勢計算の解析接線への切替は含まない。

Unity 6000.4.0f1の隔離環境で130テストが成功した。縦断の高さ・微分、正負勾配、微分と差分の比較、縮退区間、混在区間と隙間の積算、旧データの移行・再保存、新形式の型保持を確認した。結果は`/tmp/nakatetsu-w1-03-pr.nQRHqp/vertical-segments-results.xml`。本プロジェクト全体のScene・PlayMode検証は含まない。

### 評価結果の共通化（2026-09-23）

評価結果を`Nakatetsu.Track.TrackSample`へ改名し、`Track/Shared/Scripts`へ移動した。プロパティ・コンストラクターの引数・評価の挙動は維持する。`DistanceM`は評価対象上の距離で、Geometryでは基準線距離、今後のEdge実距離評価ではNode Aからの実距離を表す。

既存の`Graph/Shared`はGeometryを参照するため、共通型は独立した`Nakatetsu.Track.Shared`アセンブリへ置く。Geometry・Graphと両テストアセンブリから明示参照し、循環参照を避ける。スクリプトの`.meta` GUIDを維持し、`MovedFrom`に旧名前空間・型名・アセンブリを記載した。リポジトリ内の呼出しとドキュメントは新名へ更新する。外部C#コードでは`using Nakatetsu.Track;`と新型名・必要なasmdef参照への更新が必要であり、`MovedFrom`は旧C#型名の別名ではない。

### 開始情報のキャッシュ

`TrackGeometryContext.Workspace`へ、水平segmentの開始・終了距離、開始位置・水平姿勢と、勾配segmentの開始・終了距離、開始高さを保持できるようにした。各項目は元のsegmentIndexを保持する。高さは原点からの相対値、水平位置のYは原点のYで、勾配は別に扱う。

```csharp
var context = new TrackGeometryContext();
TrackGeometryCompiler.Rebuild(trackGeometry.Definition, context);
```

読み込み時・定義編集後に明示的に呼ぶ。Rebuildは前回の内容を消して各区間を順に計算し直す。null定義ならキャッシュを空にする。入力には従来どおり距離順の有効な区間を使う。

今回は保存処理だけを追加した。Assetへの永続化、自動再構築、共有Contextの管理、既存TryEvaluateでキャッシュを使う処理は追加していない。位置評価の高速化は、次にこのキャッシュを利用する評価処理を接続してから有効になる。

## 使用例

```csharp
using Nakatetsu.Track;
using Nakatetsu.Track.Graph.Geometry;

// trackGeometryはTrackGeometryAssetへの参照。
if (trackGeometry.TryEvaluate(25f, out TrackSample sample))
{
    // sample.Position / Rotation / Tangent
    // sample.GradientPermille
}
```

Assetを使わない場合は `TrackGeometryCalculator.TryEvaluate(definition, distanceM, out sample)` を呼ぶ。
定義は参照型なので、実行時にAssetのDefinitionを書き換えない。評価処理自体は変更を行わない。

## Unityで確認する手順

1. `Assets/Nakatetsu/Track/Graph/Geometry/Data/TrackGeometryStraight100m.asset` を選ぶ。原点からZ方向へ100mの直線を用意済み。
2. 確認用の空GameObjectを作り、`TrackGeometryPreview` を追加する。
3. `Track Geometry`へこのアセットを割り当てる。
4. SceneビューのGizmosを有効にする。再生しなくても確認できる。
5. `Probe Distance M`で確認地点を指定する。赤は右、緑は上、青は前方向。

PreviewのGameObjectのTransformは座標に加算しない。Assetの原点・開始方向が基準になる。
Previewは最大10,000区間に制限して描画するため、非常に長い基準線では指定したサンプル間隔より粗くなる。Gizmoは走行用レールメッシュではない。
既存Sceneへのオブジェクト追加は行っていない。

新規AssetはCreateメニューの `Nakatetsu > Track > Track Geometry` から作る。
新規作成直後は長さ0・区間なしなので、`lengthM`と水平区間を設定してから評価する。

## データの規則と互換性

- 位置・長さはm、勾配は‰、曲線半径は符号付きm。
- `originRotation`の水平な前方向を開始方位として使い、pitchは勾配から求め、カントによるrollは適用しない。
- 評価距離は0〜`lengthM`へClampする。範囲外の距離を移動の終端判定に使う処理はまだない。
- 基準線の距離パラメーターは旧方式を維持する。3D実長と同じとは限らない。後段のオフセット距離変換と混同しない。
- 水平区間は距離順で0から連続して配置し、`lengthM`を区間長の合計と合わせる。Inspector入力の完全な検証・自動整列・長さ自動計算は未実装。
- 勾配区間が空なら0。区間の隙間や末尾では旧方式の延長規則を使う。
- 円曲線半径の絶対値が0.001m未満なら直線として扱う旧フォールバックを保持。
- 基準線の緩和曲線は旧三次式の近似を維持した。厳密な弧長・曲率積分へ変更していない。
- null定義、空の水平区間、正でない長さ、非有限の距離、非有限の出力は評価失敗になる。すべての不正な区間構成を検出するバリデーターではない。

## 検証結果

Unity 6000.4.0f1の隔離プロジェクトでEditModeテストを実行し、17件成功・失敗0。

- リポジトリに追加した16件：距離Clamp、原点・方位、左右円曲線、区間連結、勾配とロールなし姿勢、縦曲線積分、緩和曲線近似、不正入力、定義の非変更、サンプルAsset読込。
- 隔離環境だけに用意した1件：旧Geometry一式と最小のグラフ検索代替を使い、旧 `TryResolveGeometryPose` と新APIを200地点で比較。4種類の水平線形、正負半径、開始方位、勾配変化を含む。カント廃止後の比較は旧側のカントを0にして行う。
- 比較条件：位置・接線差0.0001未満、姿勢角差0.05度未満、勾配値一致。これは今回の入力セットの結果であり、全路線での精度保証ではない。
- サンプルAssetはUnityのAssetDatabaseから読込み、終点が(0,0,100)になることを確認。

検証用プロジェクトは `/tmp/nakatetsu-guideline/`、カント廃止後のXML結果は `no-cant-results.xml` にある。旧コードは検証用領域にだけ置き、新プロジェクトのAssetsへは入れていない。
本プロジェクト全体のPlayMode検証、Sceneビューの目視確認は未実施。今回のUnity検証起動は成功しており、Rosettaのインストール等のシステム変更は行っていない。

## 次の作業

基準線の表示と編集を確認した後、旧 `TrackOffsetSegment`・距離表生成・オフセット位置評価をこのAPIへ接続する。その次にNode/Edge接続と車両位置へ進む。
分岐付帯曲線の作成補助はEditor側で追加し、基準線評価と混ぜない。
