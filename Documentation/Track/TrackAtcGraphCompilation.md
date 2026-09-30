# ATC Graphのコンパイル

`TrackAtcGraphCompiler.TryCompile`は線路・軌道回路・連動進路の定義から、新しい`TrackAtcGraphDefinition`を生成する。Sceneや設備の現在状態には依存しない。

## 入力

| 引数 | 内容 |
| --- | --- |
| `track` | `TrackGraphDefinition`。Geometry、Node、Edge、Connection、Circuit。Edgeの距離表は生成済みであること |
| `source` | 保存して編集する`TrackAtcGraphCompileDefinition`。閉塞区分、速度制限、順序付きの進路経路 |
| `interlockings` | 参照する`TrackInterlockingDefinition`の一覧。進路IDと転轍機の要求位置を検証する。連動進路がなければ空の一覧でよい |

生成元と生成結果は別に保存する。再コンパイルの入力は常に`source`であり、生成結果への直接編集は引き継がない。

## Inspectorからコンパイルする

1. `Create > Nakatetsu > Track > ATC Graph` で `TrackAtcGraphAsset` を作成する。
2. `Track Graph` に物理線路の `TrackGraphAsset`、`Source` に `TrackAtcGraphCompileAsset` を指定する。
3. `Interlockings` に参照する `TrackInterlockingAsset` を追加する。複数指定できるが、進路IDは全連動装置で一意にする。連動進路がなければ空の一覧でよい。
4. 線形を変更した場合は物理線路側で `Compile Distance Maps` を実行する。
5. `Compile ATC Graph` を押す。成功すると、このAssetの `Definition` に生成結果を保存し、Edge・Node・進路の件数を表示する。

`Compiled Graph` は読み取り専用の表示。入力や生成元は書き換えない。失敗時は理由をInspectorとConsoleに表示し、前回の生成結果を保持する。成功時の置換はUndoに対応する。Play Modeではコンパイルできない。

NtLine用は `Assets/Nakatetsu/Track/NtLine/Data/NtLineAtcGraph.asset` に入力3種類を割り当て済み。追加時点では未コンパイルなので、Inspectorでボタンを押して生成する。入力を変更しても自動更新はしないため、変更後は再コンパイルする。

## コードから呼び出す

```csharp
var errors = new List<string>();
if (TrackAtcGraphCompiler.TryCompile(
    trackDefinition, compileDefinition, interlockingDefinitions,
    out var compiled, errors))
{
    // 成功した場合だけ、呼び出し側で保存先をcompiledへ置換する。
}
// 失敗時はcompiled == null。errorsに理由が入る。
```

## 生成元の設定

- `circuits`：軌道回路IDごとの制御方式と基本速度制限[km/h]。全回路に設定する。`Unspecified`は設定漏れとしてエラー。`Block`は個別の進路設定が不要な区間、`Interlocking`は連動進路に従う区間、`Yard`は構内モードで通行する区間。列挙値はそれぞれ1・2・3を維持する。
- `speedLimits`：物理Edge IDとGeometry距離の区間で指定する追加速度制限。区間の両端は昇順・降順どちらでもよい。回路の基本速度と重複する全区間の制限から、最も低い値を採用する。0 km/hも有効な制限。
- `routes`：ATC進路ID、連動進路ID、通過順の`path`。各区間は物理Edge IDと、入口・出口のGeometry距離を持つ。入口→出口の順序が進行方向になる。同じ回路を通る直進・分岐の進路も、この経路によって区別する。
- `gradientSampleIntervalM`：勾配サンプル間隔。既定10 m。Edge実距離を基準に採取する。

速度制限の保存キーに生成済みATC Edge IDを使わないので、軌道回路や進路境界を変更して再分割しても制限を再配置できる。物理Edge IDやGeometry距離の基準自体を変更した場合は、生成元も修正する。

## 分割と接続

1. 物理Edgeの両端、軌道回路区間の境界、進路区間の入口・出口を分割点にする。
2. 各区間を距離表でEdge実距離へ変換する。Geometry距離が減少するEdgeにも対応する。
3. 区間ごとにATC Edgeを作り、所属する軌道回路IDを持たせる。
4. 物理Nodeを共有する端点は、同じATC Nodeへ接続する。Edge内部の境界はそのEdgeだけのNodeとする。
5. 速度境界ではEdgeを分割せず、Edge内の`speedLimitSections`に変換する。

転轍機の共通側・直進側・分岐側を1つの軌道回路Pで覆った場合、それぞれのATC Edgeは同じ`trackCircuitId = P`を持つ。直進進路は共通側→直進側、分岐進路は共通側→分岐側のEdge列になる。回路Pを枝ごとに別の占有状態へ分けることはない。

物理Nodeの接続には`Connection`が必要。進路の経路は`edgePairs`と連動進路の`requiredTurnouts`で検証する。現在の転轍機位置から進路を推測しない。直進側→分岐側のように定義されたペアにない接続はエラーになる。

生成IDは物理IDと区間端のEdge実距離から決める。同じ入力の再コンパイルやリストの並べ替えで変わらない。分割点や距離表を変更した場合は変わり得る。

## 方向と進路

- ATC EdgeのA→Bは物理EdgeのA→Bと一致する。
- 非連動区間は物理Edgeの`travelDirection`を使う。`AtoB`または`BtoA`を必須とし、双方向は生成しない。
- 進路に含まれる区間は`direction = Unspecified`とし、順序付きの`atcEdgeIds`から方向を読む。非自動閉塞や構内運転の区分から、連動進路を自動的に作ることはない。
- 出力の進路は入口Nodeや方向フィールドを持たないため、先頭2本の共通Nodeが1つになる経路を必要とする。1本だけの進路、先頭2本が両端のNodeを共有する経路、同じATC Edgeを再度通る経路はエラー。
- 分岐Nodeに接するATC Edgeは進路への所属を必須とする。実行時の探索では、分岐Nodeの隣接リストから自由に枝を選ばず、許可された進路のEdge列に沿って辿る。

## 勾配

始終端・等間隔の点・縦断線形／水平線形／横オフセットの区間境界で、`TrackEdgeCalculator`を使って評価する。保存値はATC EdgeのA→Bに向かって上りを正とする。逆方向の利用時は距離を`lengthM - distance`へ、勾配を負符号へ変換する。

これは点サンプルであり、サンプル間の最大・最小勾配を保証する包絡値ではない。勾配不連続の境界値は既存の線形評価規則に従う。制動計算での補間や保守的な勾配の選択は、今後のATC計算側で定義する。

## 検証と責務

全物理Edgeを軌道回路が覆う必要がある。未割当区間、異なる軌道回路の重複、範囲外の距離、存在しないID、不連続な進路、転轍機要求位置との矛盾は生成失敗にする。同じ軌道回路の区間同士の重複と、異なる回路の端点だけの接触は許す。

入力のリストや距離表は変更しない。距離表の欠損・範囲・単調性は検証するが、GeometryやOffsetの編集による距離表の古さは判定できない。線形変更後は先に[距離表を再生成](TrackDistanceMapCompilation.md)する。

このコンパイラは静的なグラフの生成までを担当する。`ProceedAllowed`、軌道回路の占有、非自動閉塞の通行許可、構内モード、停止限界の探索は実行時の入力・計算として接続する。
