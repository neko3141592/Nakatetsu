# TIMSの力行カーブ

設定アセット：`Assets/Nakatetsu/Train/Equipment/Tims/Configuration/Data/TimsControlConfig.asset`

## 速度の正規化

`Maximum Operating Speed Kmh`（`maximumOperatingSpeedKmh`）を最高運転速度として設定する。初期値・現在の設定値は120 km/h。

`TimsControlController`は、TIMSの速度を次の式で力行カーブの横軸に変換する。

```csharp
float normalizedSpeed = Mathf.Clamp01(speedMps * 3.6f / settings.maximumOperatingSpeedKmh);
```

120 km/h設定では、停止時が0、60 km/hが0.5、120 km/h以上が1となる。編成質量やモーターの定格出力が変わっても、同じ速度なら同じ横軸位置を評価する。

## 縦軸とノッチ

`Power Curves`はP1から順番に設定する。縦軸は基準引張力に掛ける倍率で、1が100%。基準引張力の定加速域・定出力域の計算は`TimsTractionLogic`が担当する。

今回の試作カーブは次の4本。

| ノッチ | 設定 |
| --- | --- |
| P1 | 既存の編集済みカーブを保持 |
| P2 | 各速度でP1から倍率1までの差の1/3を加える |
| P3 | 各速度でP1から倍率1までの差の2/3を加える |
| P4 | 全域で倍率1 |

P2・P3はP1のキー位置を引き継ぎ、値と接線を調整している。停止時の倍率は、およそ49.6%・66.4%・83.2%・100%。

配列自体が空の場合は、従来どおり「選択ノッチ÷力行ノッチ数」を倍率として使う。カーブを設定した場合は、選択ノッチのカーブと正の有限な最高運転速度が必要。120 km/hを超えた場合も、設定した最高運転速度での倍率を使い続ける。
