# EB装置のTIMS状態公開と有効運転台判定

## 入力と監視条件

`EbDeviceTimsInputAdapter` は、編成のMasterBusの `Direction/ActivatedCabPosition` を読み、自車が有効運転台側かを判定する。

| MasterBusの値 | EBを監視できる車両 |
| --- | --- |
| Front（1） | 車両index 0 |
| Rear（-1） | 車両index `CarCount - 1` |
| None（0） | なし |
| タグ欠損・型不一致・不正値 | なし |

この対応は既存の `TimsDirectionController` と同じ編成順に基づく。逆転器の向きや実際の進行方向で有効運転台を推測しない。マスコンの力行・ブレーキ・逆転器・入力有効状態は、引き続き自車のLocalBusから取得する。

速度はMasterBusの `Train/SpeedMps`（Float、m/s）から取得する。絶対値が `activationSpeedMps`（既定 `5 / 3.6` m/s、5 km/h）以上なら走行と判定し、前進・後退とも同じ条件で監視する。タグ欠損・型不一致・NaN・Infinityは入力欠落として扱う。

監視条件は、有効運転台側であること、マスコン入力が有効であること、必要な入力を取得できること、走行中であること。5 km/h未満や入力欠落などで監視対象外になると無操作時間と非常ブレーキ要求をリセットする。再び有効になった場合は新しく計時する。既定60秒の無操作判定とマスコン操作によるリセットは維持し、速度変化だけでは操作と判定しない。

TIMS型と車両indexの解決はTIMS側Adapterに置き、EB Logicは `isActiveCab` などの入力値だけで判定する。

## LocalBusへ公開するタグ

`EbDeviceTimsBusSource` が同じGameObjectの `EbDevice.Output` を読み、`TrainEquipmentAssignment.AssignedCarIndex` に対応するLocalBusへ次の値を公開する。

| Device / Item | 型 | 内容 |
| --- | --- | --- |
| `EB / IsEmergencyBrakeRequested` | Bool | EB装置の非常ブレーキ要求 |
| `EB / InactivitySeconds` | Float | 無操作時間（秒） |
| `EB / RemainingSeconds` | Float | 作動までの残り時間（秒、下限0） |

前後とも同じキーを使い、LocalBusの車両indexで区別する。MasterBusへの集約は行わない。これらはEB装置の計算結果であり、入力通信の正常性を保証するタグではない。入力欠損時にも既存Logicのリセット結果が公開される。

起動直後、一度も計算していないOutputは `HasOutput == false` として公開しない。送信元またはEBコンポーネントが無効な場合は、次の収集でこの3タグを削除する。他装置のタグは変更しない。未割り当ての装置は収集対象にならない。

共有の `EbDevice.prefab` にBusSourceを追加済み。Tc1・Tc2の車両定義はこのPrefabを参照するため、通常のEquipment Builderによる生成で送信元も追加される。別の独自EB Prefabを作る場合は、同じGameObjectにこのBusSourceを配置する。

## 更新順と接続範囲

現在のSimulationは、各ステップの冒頭でTIMS BusSourceを収集してから、全Equipmentの入力収集・計算・出力反映を行う。従ってEBの公開値は**前ステップの計算結果**であり、作動・リセットの結果は次のTIMS収集で反映される。BusSourceはEBの計算や時間進行を行わない。

有効運転台の判定はEBの入力収集時にMasterBusにある値を使う。`TimsDirectionController.CalculateAndPublish()` が有効運転台タグを公開するための既存の入口であり、上位からEB入力収集前に呼ぶ必要がある。現行コードにはその自動呼び出し経路がないため、この変更だけで方向Controllerの定期実行まで接続したことにはならない。タグが未公開の環境では、前後ともEB監視を停止する。

今回の接続はEB入力の有効運転台判定とTIMSへの状態公開まで。実際のブレーキ、予告警報、専用リセットボタン、TIMS画面の生成は接続していない。

## 検証

EditModeテストで、EB Logic、MasterBusからの運転台判定、実Prefabの送信部品、前後別LocalBusへの公開、前ステップ値の公開、操作によるリセット、前後切替、無効・欠損・不正な運転台情報、初回計算前、コンポーネント無効、別編成との分離を確認する。

2026-09-15、Unity `6000.4.0f1` の検証用プロジェクトで `EbDevice` を対象にEditModeテストを実行し、21件成功・失敗0件。作業中のEditorを閉じず、自作アセットをコピーし、描画不要のテストに必要なパッケージで実行した。変更したC#とPrefabが作業ツリーと一致すること、追加GUIDとPrefab参照、`git diff --check` を確認した。実際のSceneの運転操作やURP描画を検証したものではない。
