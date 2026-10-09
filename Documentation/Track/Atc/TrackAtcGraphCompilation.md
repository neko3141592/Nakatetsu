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

NtLine用は `Assets/Nakatetsu/Track/NtLine/Data/NtLineAtcGraph.asset` に入力3種類を割り当て済み。生成済みの定義も保存している。入力を変更しても自動更新はしないため、変更後は再コンパイルする。

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
- `routes`：ATC進路ID、連動進路ID、通過順の`trackCircuitIds`。Connectionの接続ペアと対応する連動進路の`requiredTurnouts`から、定位・反位のATC Edgeを解決する。単一回路の進路だけは`entryDirection`でA→B・B→Aを指定する。複数回路なら順序から入口方向を求める。
- `gradientSampleIntervalM`：勾配サンプル間隔。既定10 m。Edge実距離を基準に採取する。

速度制限の保存キーに生成済みATC Edge IDを使わないので、軌道回路や進路境界を変更して再分割しても制限を再配置できる。物理Edge IDやGeometry距離の基準自体を変更した場合は、生成元も修正する。

## 結合と接続

線路用GraphのEdge・Node・Connection・軌道回路区間は変更しない。ATC Graphだけを生成する。

1. 軌道回路境界で線路用Edgeを区切り、距離表でGeometry距離をEdge実距離へ変換する。
2. 同一軌道回路内の接続可能な区間を結合し、回路の入口から出口までを一つのATC Edgeにする。
3. 転轍機回路ではConnectionの定位・反位ペアごとに、common＋normalとcommon＋reverseの二つのATC Edgeを作る。共通区間は同じ線路用区間を参照する。
4. 複数のATC Edgeも同じ`trackCircuitId`へ所属させ、占有は回路全体で扱う。
5. 速度制限・勾配は結合後の距離へ変換する。速度境界ではEdgeを分割しない。

各ATC Edgeの`physicalSpans`は、ATCのA→B方向に並べた線路用区間を保持する。各区間は`trackEdgeId`と`startDistanceOnEdgeM`・`endDistanceOnEdgeM`を持ち、線路用Edgeを逆方向に通る場合は始点距離が終点距離より大きくなる。ATC上の区間長は両距離の差の絶対値で、その累積がATC Edge内の位置になる。転轍機IDや必要位置はATC Edgeへ重複保存しない。

例えば生成元は`21T → 2RT`と連動進路IDを指定するだけでよい。21定位を要求する連動進路なら、21Tの定位用Edgeと2RTのEdgeへ解決する。人が生成済みEdge IDや三つの物理区間を並べる必要はない。

Connectionにない接続、連動進路の転轍機条件と矛盾する経路、一意に解決できない進路は生成失敗とする。現在の転轍機位置や連動の実行時状態から静的進路を生成しない。

生成IDは軌道回路と静的な物理経路から決め、定位・反位で異なるIDにする。同じ入力の再コンパイルやリストの並べ替えで変わらない。IDの接尾辞から転轍機位置を判定しない。

## 方向と進路

- 同じ共通物理区間を含む定位・反位Edgeは、その共通区間の向きを揃える。
- 非連動区間は物理区間の`travelDirection`をATC Edgeの向きへ変換する。
- 進路に含まれるEdgeの`direction`は`Unspecified`とする。進路は順序付き`atcEdgeIds`と先頭Edgeの`entryDirection`を持ち、以降の方向は接続から求める。単一Edgeの進路にも対応する。
- 地上の探索では、成立した連動進路に沿って回路の送信対象Edgeを選択する。未成立の進路へ新規進入させず、手前のEdge終端で停止する。既に選択Edge内にいる列車は、そのEdge終端まで進行できる。

## 勾配

始終端・等間隔の点・縦断線形／水平線形／横オフセットの区間境界で、`TrackEdgeCalculator`を使って評価する。保存値はATC EdgeのA→Bに向かって上りを正とする。逆方向の利用時は距離を`lengthM - distance`へ、勾配を負符号へ変換する。

これは点サンプルであり、サンプル間の最大・最小勾配を保証する包絡値ではない。勾配不連続の境界値は既存の線形評価規則に従う。制動計算での補間や保守的な勾配の選択は、今後のATC計算側で定義する。

## 検証と責務

全物理Edgeを軌道回路が覆う必要がある。未割当区間、異なる軌道回路の重複、範囲外の距離、存在しないID、不連続な進路、転轍機要求位置との矛盾は生成失敗にする。同じ軌道回路の区間同士の重複と、異なる回路の端点だけの接触は許す。

入力のリストや距離表は変更しない。距離表の欠損・範囲・単調性は検証するが、GeometryやOffsetの編集による距離表の古さは判定できない。線形変更後は先に[距離表を再生成](../Graph/TrackDistanceMapCompilation.md)する。

このコンパイラは静的なグラフの生成までを担当する。`ProceedAllowed`、軌道回路の占有、非自動閉塞の通行許可、構内モード、停止限界の探索は実行時の入力・計算として接続する。
