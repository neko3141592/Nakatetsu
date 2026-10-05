# ATCのブレーキパターン仕様実装メモ

## 1. 保持するパターン
- ATC常用ブレーキパターン
- ATC非常ブレーキパターン

## 2.　パターンの仕様
ATCのパターンは、以下の情報を持つ
- 進行可能なATCエッジ列
- ATCエッジ列を1次元の直線として捉えた時、距離の合計長
- パターン計算のサンプル間隔(どのくらいの間隔で積分するか)

``` c#
public class TrainAtcBrakePattern
{
    //　距離長
    public float pathLengthM;

    // サンプリング間隔
    public float samplingIntervalM;

    // 進行可能なATCエッジ列
    public List<float> pathAtcEdges;

    //　速度パターン
    public List<float> speedLimitMps = new();
}
```

## 3. パターンの計算方法
基本的には、``` v^2 - v_0 ^ 2 = 2ax ```の公式を使用し、後方からパターンを作成する。

### 初期化
```speedLimitMps```は、最初にすべての要素をその線区の営業最高速度で初期化する。そして、常設速度制限を反映する。(各距離に対応するエッジを取得して、そのエッジの常設速度制限でspeedLimitMpsを更新する)

### パターン作成
まず、```speedLimitMps[^1]```を絶対停止点とし、speedLimitMpsを0にする。また、最後から過走余裕距離分の要素もspeedLimitMpsを0にする

