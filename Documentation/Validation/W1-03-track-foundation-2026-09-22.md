# W1-03 中間実装：Track定義・一定オフセット

検証日：2026-09-22。W1-03全体の完了記録ではない。

## 実装範囲

- 位置・方向・移動結果の[基本契約](../Architecture/TrackMovement.md)を確定。
- `TrainGeometry`から`TrackGeometry`へ型・ファイル・フィールド名を整理。名前空間は`Nakatetsu.Track.Graph.Geometry`。既存の`.meta` GUIDを維持。
- Edge定義を`Graph/Edge`へ統一し、Node定義を`Graph/Node`へ分離。`TrackGraphDefinition`はGeometry・Edge・Nodeの定義リストを集約。
- `TrackGraphCompiler`でID・接続・Geometry参照・基準線距離範囲を検証し、`TrackGraphContext`に3種類のID検索辞書を構築。
- `TrackEdgeAsset`に定義と生成済みLUTを保存できる型を用意。LUT生成処理は未実装。
- `TrackEdgeOffsetSegment`の共通評価契約と一定オフセットを実装。微分は基準線距離に対する横オフセットの変化率で、一定形状では0。

## 検証

- ステージしたGraph関連ソースとCoreのasmdefを、新規の隔離Unityプロジェクトへ書き出して検証。
- Unity 6000.4.0f1、Unity Test Framework 1.6.0、EditModeテスト46件成功・失敗0。
- 対象：既存のGeometry評価、旧フィールド名の読込み、Edge AssetとLUTの保存・再読込み、オフセット派生型の保存、一定オフセットの値・微分、Graph集約・ID検索・不正参照の検出。
- 結果：`/tmp/nakatetsu-w1-03-pr.nQRHqp/results.xml`。コミット対象コードに対する検証であり、本プロジェクト全体のPlayMode・Scene検証は含まない。
- コミット差分に対する`git diff --cached --check`を実施。

## 後続作業・対象外

Geometryの解析微分、OffsetSegmentの区間選択・検証、LUT生成・更新検知、Edge位置評価、移動・終端結果、Edge跨ぎ、通過履歴は未実装。辞書構築を呼ぶScene側の初期化処理も今後接続する。

作業中の`TrackEdgeCalculator.cs`とその`.meta`は、このコミットと検証対象に含めず、ローカルに保持する。荷重装置・駅表・フォントの別作業も含めない。
