# 走行基準GuideLineの移植

実装日：2026-09-07。ブランチ：`feature/track-guideline`。

## 対象

旧 `TrackGeometry` を、新側の `Nakatetsu.Track.GuideLine` として移植した。
景観用の `SceneryGuideLineRule` とは別の機能。旧プロジェクトは変更していない。

今回は基準線だけを扱う。オフセットEdge、距離変換表、Node接続、分岐、JSON読込、車両走行、レールメッシュ生成はまだ追加していない。
今後はこの基準線からオフセット線路を作る。独立線形方式へ変更する作業は含まない。

## カント廃止

GuideLineから `cantSegments`、`TrackCantSegment`、`CantMm`、カント補間・ロール適用を削除した。カント角計算にのみ使用していた `gaugeM` も削除した。基準線は平面線形と高さ・勾配を担当する。実線路側のカント機能は今回追加していない。

旧カントが非ゼロの姿勢とは意図的に異なる。既存の独自GuideLineアセットに保存したカント・軌間値は使われなくなる。付属サンプルからも該当フィールドを除去済み。

## ファイルと役割

現在のスクリプトは `Assets/Nakatetsu/Track/Geometry/Scripts/`、テストは `Assets/Nakatetsu/Track/Geometry/Tests/`。既存の `Nakatetsu.Track.asmdef` を使用する。

| ファイル | 内容 |
| --- | --- |
| `GuideLineDefinition.cs` | ID、名称、長さ、原点・方向、水平・勾配区間 |
| `TrackCurveDefinitions.cs` | 旧区間型と曲線種別。Straight/Curve/TransitionIn/TransitionOut |
| `GuideLineCalculator.cs` | 距離から位置・接線・姿勢を評価する静的処理 |
| `GuideLineProfileCalculator.cs` | 旧勾配積分・勾配取得 |
| `GuideLineSample.cs` | 評価結果。距離、位置、接線、姿勢、勾配 |
| `GuideLineAsset.cs` | 定義をInspectorで編集・保存するScriptableObject |
| `GuideLinePreview.cs` | Sceneビューで基準線と指定地点の向きを描く確認用コンポーネント |

Definitionが固定入力、Sampleが出力で、Calculatorは状態を持たない。PreviewがUnityの描画接続を担当し、計算側からシーンやControllerを参照しない。

## segment開始情報の保存（2026-09-08）

`GuideLineContext.Workspace`へ、水平segmentの開始・終了距離、開始位置・水平姿勢と、勾配segmentの開始・終了距離、開始高さを保持できるようにした。各項目は元のsegmentIndexを保持する。高さは原点からの相対値、水平位置のYは原点のYで、勾配は別に扱う。

```csharp
var context = new GuideLineContext();
GuideLineCompiler.Rebuild(guideLine.Definition, context);
```

読み込み時・定義編集後に明示的に呼ぶ。Rebuildは前回の内容を消して各区間を順に計算し直す。null定義ならキャッシュを空にする。入力には従来どおり距離順の有効な区間を使う。

今回は保存処理だけを追加した。Assetへの永続化、自動再構築、共有Contextの管理、既存TryEvaluateでキャッシュを使う処理は追加していない。位置評価の高速化は、次にこのキャッシュを利用する評価処理を接続してから有効になる。

## 使用例

```csharp
using Nakatetsu.Track.GuideLine;

// guideLineはGuideLineAssetへの参照。
if (guideLine.TryEvaluate(25f, out GuideLineSample sample))
{
    // sample.Position / Rotation / Tangent
    // sample.GradientPermille
}
```

Assetを使わない場合は `GuideLineCalculator.TryEvaluate(definition, distanceM, out sample)` を呼ぶ。
定義は参照型なので、実行時にAssetのDefinitionを書き換えない。評価処理自体は変更を行わない。

## Unityで確認する手順

1. `Assets/Nakatetsu/Track/Geometry/Data/Straight100m.asset` を選ぶ。原点からZ方向へ100mの直線を用意済み。
2. 確認用の空GameObjectを作り、`GuideLinePreview` を追加する。
3. `Guide Line`へこのアセットを割り当てる。
4. SceneビューのGizmosを有効にする。再生しなくても確認できる。
5. `Probe Distance M`で確認地点を指定する。赤は右、緑は上、青は前方向。

PreviewのGameObjectのTransformは座標に加算しない。Assetの原点・開始方向が基準になる。
Previewは最大10,000区間に制限して描画するため、非常に長い基準線では指定したサンプル間隔より粗くなる。Gizmoは走行用レールメッシュではない。
既存Sceneへのオブジェクト追加は行っていない。

新規AssetはCreateメニューの `Nakatetsu > Track > Guide Line` から作る。
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
