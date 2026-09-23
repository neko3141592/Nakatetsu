# 線形計算の式と座標・距離の約束

作成日：2026-09-23。

現在の実装に使われている位置計算と、これから実装する解析微分の式をまとめる。ここに数式を記載したことは、対応する機能の実装完了を意味しない。

## 1. 実装状況と参照先

| 内容 | 現在の状態 |
| --- | --- |
| 直線・円曲線・三次緩和曲線の位置と従来の向き | 実装済み |
| 勾配・高さの評価 | 実装済み |
| 水平Segmentの`EvaluateDerivative()` | 全4形状で実装済み。姿勢評価への接続は未実装 |
| 縦断Segmentの高さ変化量・`EvaluateDerivative()` | 一定勾配・線形勾配で実装済み |
| 一定オフセットの値と微分 | 実装済み。値は一定、微分は0 |
| Geometryとオフセットの解析微分の合成 | 未実装 |
| Edge実距離のLUT生成・距離変換・走行評価 | 未実装。LUT保存用の型は用意済み |

主な参照先：

- [TrackGeometryCalculator.cs](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryCalculator.cs)：区間選択・座標変換・勾配との合成。
- 水平線形の式：[直線](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryStraightSegment.cs)、[円曲線](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryCircularSegment.cs)、[緩和曲線の入口](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryTransitionInSegment.cs)、[緩和曲線の出口](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryTransitionOutSegment.cs)の`EvaluatePosition()`。
- [TrackGeometryHorizontalSegment.cs](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryHorizontalSegment.cs)：水平Segmentの共通契約。
- [TrackGeometryVerticalSegment.cs](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryVerticalSegment.cs)：縦断Segmentの共通契約。
- 勾配・高さの式：[一定勾配](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryConstantGradientSegment.cs)、[線形勾配](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryLinearGradientSegment.cs)。
- [TrackGeometryProfileCalculator.cs](../../Assets/Nakatetsu/Track/Graph/Geometry/Scripts/TrackGeometryProfileCalculator.cs)：縦断区間選択・高さ積算・区間外の勾配延長。
- [TrackEdgeOffsetSegment.cs](../../Assets/Nakatetsu/Track/Graph/Edge/Scripts/TrackEdgeOffsetSegment.cs)：オフセットの共通契約。
- [線路上の位置・方向・移動結果の契約](../Architecture/TrackMovement.md)：列車位置と移動距離の符号。

## 2. 記号と座標

| 記号 | 意味 | 単位 |
| --- | --- | --- |
| `S` | Geometry起点からの基準線距離 | m |
| `S₀` | 対象Segmentの開始基準線距離 | m |
| `s = S − S₀` | Segment内の基準線距離 | m |
| `L` | Segmentの基準線距離上の長さ | m |
| `r` | 符号付き曲線半径。正は右曲がり、負は左曲がり | m |
| `ℓ` | Node Aから測ったEdge実距離 | m |
| `o(S)` | 横オフセット。Geometryの距離増加方向を向いて右が正 | m |
| `θ` | 水平の向き。式の中ではラジアン | rad |

水平Segmentのローカル座標は、始点が原点、開始時の前方が`+Z`、右が`+X`、上が`+Y`。水平線形だけの位置は`(x, 0, z)`となる。

`EvaluatePosition(S, ...)`はGeometry全体の基準線距離を受け取り、内部で区間内距離`s`に変換する。返すのはSegment始点基準のローカル位置と、Segment開始時からの相対的な向き。

以下は有効な定義、`L > 0`、`0 ≤ s ≤ L`を前提とする。現在のコードは開始距離や区間長に対して`Max(0, ...)`を使う箇所があるが、負の値を正規の定義として許可するという意味ではない。

基準線距離は実際の弧長とは限らない。特に三次緩和曲線や勾配・オフセットを加えた線路では、基準線距離の差をそのまま実距離としない。

## 3. 微分を返すときの契約

水平位置を`p(s) = (x(s), 0, z(s))`とすると、返す微分は次のベクトル。

```text
p'(s) = (dx/ds, 0, dz/ds)
```

`S₀`は区間内で定数なので、`ds/dS = 1`。この区間内では`dp/dS = dp/ds`となる。

**微分ベクトルは正規化しない。** その長さは、基準線距離に対して実際の位置が変化する割合を表す。

```text
水平実距離の増加率 = |p'(s)| = sqrt((dx/ds)² + (dz/ds)²)
```

方向だけが必要になった時点で、長さが0でないことを確認して正規化する。先に正規化すると、実距離計算や勾配・オフセットとの合成に必要な大きさが失われる。

## 4. 水平線形の位置・微分

この節の位置式と従来の向きは既存実装に対応する。一次・二次微分は位置式から導いた実装用の式であり、現時点では未実装。

### 4.1 直線

対象：`TrackGeometryStraightSegment`。

```text
x(s) = 0
z(s) = s

x'(s) = 0
z'(s) = 1

x''(s) = 0
z''(s) = 0

θ = 0
```

微分ベクトルは`(0, 0, 1)`で、水平実距離の増加率は1。

### 4.2 円曲線

対象：`TrackGeometryCircularSegment`。

```text
θ(s) = s / r

x(s) = r × (1 − cos(s / r))
z(s) = r × sin(s / r)

x'(s) = sin(s / r)
z'(s) = cos(s / r)

x''(s) = cos(s / r) / r
z''(s) = −sin(s / r) / r
```

水平実距離の増加率は1。半径の符号をそのまま式に使えば左右の曲線を表せる。

### 4.3 緩和曲線の入口

対象：`TrackGeometryTransitionInSegment`。

現在の位置式は、旧実装から引き継いだ三次式の近似。厳密なクロソイドの積分ではない。

```text
x(s) = s³ / (6Lr)
z(s) = s

x'(s) = s² / (2Lr)
z'(s) = 1

x''(s) = s / (Lr)
z''(s) = 0
```

従来の向きと、位置式の解析接線の向きは次のように異なる。

```text
従来の近似角：θ_legacy(s) = s² / (2Lr)
解析接線の角：θ_tangent(s) = atan2(x'(s), z'(s))
                           = atan(s² / (2Lr))
```

`atan(a)`と`a`は小さい値で近いが、同じ式ではない。微分関数の追加と、既存の向きを解析接線へ切り替える変更は分けて検証する。

### 4.4 緩和曲線の出口

対象：`TrackGeometryTransitionOutSegment`。この位置式も旧実装の三次近似を維持する。

```text
x(s) = s² / (2r) − s³ / (6rL)
z(s) = s

x'(s) = s / r − s² / (2rL)
z'(s) = 1

x''(s) = 1 / r − s / (rL)
z''(s) = 0

従来の近似角：θ_legacy(s) = s / r − s² / (2rL)
解析接線の角：θ_tangent(s) = atan(s / r − s² / (2rL))
```

### 4.5 現在のフォールバックと境界

- 円曲線は`|r| < 0.001m`で直線の式を使う。
- 緩和曲線は`|r| < 0.001m`または`L < 0.001m`で直線の式を使う。
- 微分を実装するときも、位置評価が選んだ式と同じ分岐を使う。
- Segment境界で左右の接線が一致するとは限らない。旧近似角で次Segmentを配置しているため、各位置式を微分するだけでは、境界の滑らかさまで保証できない。
- 現在のCalculatorは区間終端ちょうどでは前側のSegmentを評価する。境界の左右それぞれで位置と接線を確認する。

## 5. ローカル座標からワールド座標へ

対象Segmentの始点位置を`O`、開始時の水平回転を`Q`とする。`Q × ベクトル`はQuaternionによる回転を表す。

```text
水平位置 G_horizontal(S) = O + Q × p(s)
水平位置の微分          = Q × p'(s)
```

`O`と`Q`はそのSegment内では定数なので、微分に平行移動`O`は加えない。

次のSegmentの開始位置・回転は、現在の区間の終点位置・終点角から積み上げる。現行コードはこの角に`θ_legacy`を使っている。微分ベクトルはワールドへ回転しても正規化せず保持する。

式の角度はラジアン、`headingDegrees`とUnityの`Quaternion.Euler`に渡す角度は度。変換は`degrees = radians × 180 / π`。

## 6. 勾配と高さ

高さ区間のローカル基準線距離を`v`、区間長を`Lᵥ`、始点・終点勾配を`g₀`・`g₁`（‰）とする。水平区間とは別の区間なので、水平の`s`・`L`と混同しない。

`TrackGeometryLinearGradientSegment`では、勾配を区間内で直線補間し、基準線距離について積分する。

```text
g(v) = g₀ + (g₁ − g₀) × v / Lᵥ

区間始点からの高さ差
Δh(v) = [g₀v + (g₁ − g₀)v² / (2Lᵥ)] / 1000

dh/dS = g(v) / 1000
```

`TrackGeometryConstantGradientSegment`は一定勾配`g`を持ち、`Δh(v) = gv / 1000`、`dh/dS = g / 1000`を返す。

両形状とも`EvaluateHeightDeltaM(S)`は区間始点からの高さ差[m]、`EvaluateDerivative(S)`は区間内の`dy/dS`[m/m]を返す。入力はGeometry起点からの距離で、内部で`v`へ変換する。端点の微分は区間側の勾配を表す。長さ0.001m以下は高さ・微分とも0で、積算から除外する。

高さには前の区間までの累積高さを加える。区間の隙間・最後の区間より先は、直前の終点勾配を延長する。最初の勾配区間より前は0‰。

3D位置と、位置式に対応する微分は次の合成になる。

```text
G(S)  = G_horizontal(S) + (0, h(S), 0)
G'(S) = Q × p'(s)       + (0, g(S) / 1000, 0)
```

`h(S)`はGeometry原点からの高さ差。原点のY座標は水平位置側に含まれるので、二重に加えない。

現在の姿勢は`pitch = −atan(g/1000)`と従来の水平角から作る。一方、位置の微分に厳密に合わせる場合は、水平微分の大きさも考慮する。

```text
水平微分の大きさ q = sqrt(G'x² + G'z²)
位置式に対応するpitch = −atan2(G'y, q)
```

水平微分の大きさが1でない緩和曲線では、両者は一致しない。ここでも、現行姿勢を変更する際は位置式との整合性と旧挙動との差を検証する。

## 7. 横オフセットの合成

この節は今後のEdge評価のための式。カントなしで、基準線の水平な右方向を使う場合を対象とする。

Geometryのワールド位置を`G(S)`、単位右方向を`B(S)`、横オフセットを`o(S)`とする。半径`r`と右方向ベクトルを混同しないよう、右方向には`B`を用いる。

```text
Edgeの位置 P(S) = G(S) + o(S) B(S)

P'(S) = G'(S) + o'(S) B(S) + o(S) B'(S)
```

一定オフセット`o(S) = c`では`o'(S) = 0`。ただし基準線が曲がっていれば`B'(S)`は0ではないので、`oB'`の項を省略してはいけない。

参考として、直線的に変化するオフセットなら次の式になる。この形状はまだ未実装。

```text
o(S) = o₀ + (o₁ − o₀) × (S − S₀) / (S₁ − S₀)
o'(S) = (o₁ − o₀) / (S₁ − S₀)
```

### 7.1 解析接線に合わせた右方向の式

世界の`+Z`から右回りの水平角を`θ(S)`とすると、

```text
B(S)  = (cos θ, 0, −sin θ)
B'(S) = θ'(S) × (−sin θ, 0, −cos θ)
```

`θ`を位置の解析接線から求める場合、水平位置の一次・二次微分から角度の微分を求められる。

```text
θ = atan2(G'x, G'z)
θ' = (G'z G''x − G'x G''z) / (G'x² + G'z²)
```

分母が0になる地点では、この式で右方向を定義できない。不正な姿勢として扱う方針が必要。

**位置をずらすときに使う`B`と、微分式の`B'`は同じ定義から求める。** 例えば旧近似角から作った右方向で位置を計算し、解析接線の角度から求めた`B'`を合成すると、その結果は位置の微分にならない。旧近似角を残すなら、その近似角を微分する必要がある。

## 8. Edge実距離とLUT

オフセット後の3D位置を基準線距離で微分した`P'(S)`から、実距離の増加率を求められる。

```text
実距離の増加率 = |P'(S)|
```

Node Aの基準線距離を`S_A`とし、Edge内で基準線距離が単調に進むとき、Node Aからの実距離は次の積分で表せる。

```text
ℓ(S) = abs( ∫[S_A → S] |P'(u)| du )
```

この絶対値は、Geometryに対して逆向きのEdgeでもNode Aからの距離を非負にするためのもの。LUTにはNode A→Bの順に`(Edge実距離ℓ, 基準線距離S)`を保存する。

解析微分を使わず、評価した位置間の距離を積算して近似する方法もある。

```text
ℓ₀ = 0
ℓᵢ₊₁ = ℓᵢ + |P(Sᵢ₊₁) − P(Sᵢ)|
```

これは曲線を弦で近似する方法なので、刻み幅による誤差がある。どちらの積算法を実装するかと、その刻み幅・許容誤差は別途決める。端点と形状の区間境界を含め、最後の端数区間を落とさない。

向きが必要になった時点で、合成後の微分を正規化する。

```text
Geometry距離増加方向の接線 T = P'(S) / |P'(S)|
Edge A→B方向の接線 = sign(S_B − S_A) × T
```

`|P'| = 0`や非有限値では正規化しない。列車の編成前方向と前進・後退の扱いは、この線路接線の評価とは別にする。

## 9. 数値確認の例

`s = 50m`、`L = 100m`、`r = 500m`での値。水平線形のみ、Segmentローカル座標。

| 形状 | x [m] | z [m] | dx/ds | dz/ds | 微分の長さ |
| --- | ---: | ---: | ---: | ---: | ---: |
| 直線 | 0 | 50 | 0 | 1 | 1 |
| 円曲線 | 2.497917361 | 49.916708323 | 0.099833417 | 0.995004165 | 1 |
| 緩和曲線の入口 | 0.416666667 | 50 | 0.025 | 1 | 1.000312451 |
| 緩和曲線の出口 | 2.083333333 | 50 | 0.075 | 1 | 1.002808556 |

式の確認には、区間内部で中心差分と比較できる。

```text
p'(s) ≈ [p(s + ε) − p(s − ε)] / (2ε)
```

これは解析式を検証するための近似であり、走行時の微分を有限差分で実装するという方針ではない。Segment境界やフォールバックの境界を跨がず、浮動小数点誤差を考慮した`ε`・許容差を使う。

## 10. 線路の接線と車体の向き

線路位置の解析微分で求められるのは、その地点の接線。

- 車体中心の接線に合わせる方式では、その接線を車体の前方向に使う。
- 前後台車の位置に合わせる方式では、前後台車の位置差を車体の前方向に使う。これは接線の数値近似ではなく、別の配置モデル。
- 接線だけではロールは決まらない。上方向・カントを別途定義する必要がある。

解析微分の導入、車体の配置モデル変更、カント対応を一度に混ぜず、それぞれ検証する。
