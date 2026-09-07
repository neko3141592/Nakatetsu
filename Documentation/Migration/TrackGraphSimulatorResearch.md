# 線路グラフを持つシミュレーターの調査

採用結果：オフセット方式を継続し、走行基準GuideLineから移植する。本文の形状方式拡張は比較案であり、今回の実装には含めない。

調査日：2026-09-07。公開実装を確認できるOpen Railsと、公式Mod APIで構造を確認できるTransport Fever 2を対象にした。後者は運転シミュレーターではなく輸送経営シミュレーションである。

この文書では確認した事実とNakatetsuへの提案を分ける。各作品の起動・走行試験、ソースの移植は行っていない。リンク先のmasterやAPI文書は更新され得る。

## 1. 比較

| 対象 | 接続と形状 | 今回参考になる部分 |
| --- | --- | --- |
| Open Rails | 線路区間・分岐・終端を接続し、線路区間内に直線／曲線の列を持つ | 線路上の位置と、区間境界を跨ぐ移動 |
| Transport Fever 2 | Node/Edge、EdgeGeometry、TransportNetworkの接続情報 | 接続グラフと形状を分け、異なる形状を同じネットワークで扱う設計 |
| 旧TD-ATC | Node/Edgeに加え、基準Geometryとオフセットで形状を評価 | グラフは既に存在する。作成の難しさは主に形状入力・接続支援の問題 |

## 2. Open Rails

### 線路の表現

公式Track Viewer資料では、分岐や終端の間の線路をvector nodeとして扱い、その中に複数の直線・曲線sectionを持つと説明している。ここでの「node」は点だけを意味しない。NakatetsuのEdgeに近い線状の要素にもnodeという名前が付いている。[公式Track Viewer資料](https://www.openrails.org/assets/files/ORTS_Trackviewer_manual.pdf)

データ定義では `TrPin` が接続先と方向を持ち、`TrJunctionNode` が選択経路を持つ。[TrackDatabaseFile.cs](https://github.com/openrails/openrails/blob/master/Source/Orts.Formats.Msts/TrackDatabaseFile.cs)

### 線路上の移動

`Traveller` は現在のnode、section、区間内位置、向きを持つ。`Move` は現在sectionで距離を消費し、余りがあれば `NextSection` に進む。分岐での接続選択には `SelectedRoute` とpinを使う。内部位置の単位は直線でm、円曲線でradと異なる点にも注意が必要。[Traveller.cs](https://github.com/openrails/openrails/blob/master/Source/Orts.Simulation/Simulation/Traveller.cs)

コードの終端には `MoveInTrackSectionInfinite` という特別処理もある。「このMoveを使えば必ず車止めで停止する」とは解釈しない。移動カーソルと運転上の停止条件は区別して読む。

### Nakatetsuへの提案

「線路全体を知る列車Controller」に位置処理を集めず、線路上の位置を表す小さなデータと、そこから距離を進める処理を用意する。例えば次のような契約にする。

```text
TrackPosition = Edge ID + Edge上の距離[m] + 向き
Advance(position, signedDistance, connectionState)
  → 更新位置 + 移動できた距離 + 未消費距離 + 終了理由
```

これは新側への提案であり、Open RailsのAPIそのものではない。Nakatetsuでは公開する距離の単位をmに統一できる。車止め・無効接続・分岐未検知・探索上限を終了理由で区別する。

Open Railsの古いデータ形式やクラス構成をそのまま移す必要はない。既存のTrackGeometryの区間列と、TrainTrackStateの考え方を整理する際の比較対象にする。

## 3. Transport Fever 2

### 接続と形状を分ける

公式APIに `BaseNode`、`BaseEdge`、`TransportNetwork` がある。`EdgeGeometry` には Straight / Arc / CubicSpline / CubicOffsetSplineの選択肢がある。また `TransportNodeData.Port` は隣接Edgeと、そのportから接続可能な別portを表す。経路はEdgeと方向の列、経路内の位置は `PathPos` で表現される。[公式型API](https://wiki.transportfever2.com/api/modules/api.type.html)

これは公開APIから確認できる構造であり、ゲーム内部の全走行処理や分岐生成アルゴリズムを確認したものではない。`EdgePos.param`等をそのままmとみなしてはいけない。

### 作成入力

Constructionの線路入力は、Edgeの始終点それぞれに位置と接線ベクトルを与える。`snapNodes`で、後から線路を繋げられる端点を指定する。[公式Construction資料](https://www.transportfever2.com/wiki/doku.php?id=modding%3Aconstructionbasics)

この記述から、作成者が基準線からの横位置だけでなく、繋ぎたい端点と方向を入力できることが分かる。ただし「半径Rを保証する円曲線」と「端点と接線から生成するSpline」は同じ制約ではない。

### Nakatetsuへの提案

Edgeの形状を、必ずオフセットだけで作る設計にしない。

- 指定半径を維持したい場合：独立した直線・円曲線・緩和曲線の列。
- 並行線：基準線からのオフセット。
- 端点を合わせたい場合：接続条件から生成。ただし実際の曲率と最小半径を検証する。

交差点でEdgeの隣接一覧だけを持つと、入ってきた方向によっては不可能な経路も選べてしまう。進入portと退出portの組を持つ設計は、分岐・交差の接続を扱う参考になる。

旧 `TurnoutConnection` は既にEdgeの組を持つため、ゼロから全体を作り直す必要はない。後から形状と接続の選択状態を明確に分ければよい。

## 4. 分岐付帯曲線に対する結論

**グラフを採用すること自体では、曲線は描きやすくならない。** グラフは「どことどこが繋がるか」、形状は「その間がどう曲がるか」、作図ツールは「その形をどう入力するか」を担当する。

```text
作成入力：分岐出口 + 半径 + 曲線長、または接続先の端点条件
    ↓ 検証・形状生成
Edgeの形状：直線／円曲線／緩和曲線／オフセット等
    ↓
接続グラフ：進入Edge → 選択可能な退出Edge
    ↓
列車の経路と位置：使うEdge列 + 向き + 距離
```

上図は依存・利用関係の説明であり、毎フレーム上から全生成をやり直す意味ではない。

Nakatetsuでは以下を推奨する。

1. 既存のNode/Edge構造を活かす。
2. Edgeの位置評価を、形状の作成方法から切り離す。
3. 分岐出口の位置・向きを引き継いで、独立した円曲線を生成する小さな試作を行う。
4. 同じ位置更新処理で、旧オフセット形状と新しい形状の両方を走れるようにする。
5. 複数車両では選んだ経路を保持し、現在の分岐状態だけで後部位置を再計算しない。

既存Geometryへ独立線形を入れてオフセット0で評価する案と、Edge形状の種類を増やす案を比較する。後者を最初から必須にしない。半径・端点位置・端点方向を同時に満たせない場合は、作図側で条件の矛盾を伝える。

## 5. 確認範囲

- Open Rails：公式資料と `TrackDatabaseFile.cs`、`Traveller.cs` の関連箇所を確認。
- Transport Fever 2：公式型APIとConstruction入力仕様を確認。非公開エンジン実装は未確認。
- Advtrainsも候補として検索したが、今回詳細な経路資料の取得が安定しなかったため、構造の比較根拠には採用していない。旧GitHubミラーは更新停止の表示があり、現行実装と混同しない。
- 両作品の仕組みを参考にする設計調査であり、外部コードは新プロジェクトへコピーしていない。

関連文書：[線形作成方式の調査](TrackAuthoringResearch.md)、[線路基盤の移植計画](TrackFoundationMigration.md)。
