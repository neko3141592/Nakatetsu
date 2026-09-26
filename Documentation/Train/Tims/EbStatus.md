# EB装置のTIMS状態公開と有効運転台判定

装置全体の送受信先と接続状況は [Bus・タグ接続一覧](BusTagMap.md) を参照。

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

作動前の監視条件は、有効運転台側であること、マスコン入力が有効であること、必要な入力を取得できること、走行中であること。5 km/h未満や入力欠落などで監視対象外になると無操作時間をリセットする。再び有効になった場合は新しく計時する。既定では60秒の無操作でブザーを開始し、さらに5秒無操作が続くと非常要求を出す。ブザー開始前と5秒の猶予中は、Spaceまたはマスコン操作で無操作時間を0秒へ戻し、ブザーも止める。速度変化だけでは操作と判定しない。

非常作動後は`EbDeviceState.isEmergencyBrakeLatched`でEB装置自身が非常要求を保持し、解除までブザーも鳴動要求を維持する。マスコン操作・5 km/h未満への減速・運転台切替・入力欠損では解除しない。有限の速度とマスコン状態を取得でき、絶対速度0.01 m/s未満かつ自車の力行位置0なら保持を解除し、タイマーも初期化する。ブレーキ位置は解除条件に含めない。マスコンの実際の操作位置を書き換える処理は持たない。

TIMS型と車両indexの解決はTIMS側Adapterに置き、EB Logicは `isActiveCab` などの入力値だけで判定する。

## 設定と公開状態

`warningDelaySeconds`（既定60秒）はブザー開始まで、`warningDurationSeconds`（既定5秒）は非常までの猶予。以前のPrefabの`activationDelaySeconds`は`FormerlySerializedAs`で前者へ引き継ぐ。`Output.isBuzzerRequested`と`Output.isEmergencyBrakeRequested`は別々に公開し、音声側では時間・警報条件を判定しない。

## LocalBusへ公開するタグ

`EbDeviceTimsBusSource` が同じGameObjectの `EbDevice.Output` を読み、`TrainEquipmentAssignment.AssignedCarIndex` に対応するLocalBusへ次の値を公開する。

| Device / Item | 型 | 内容 |
| --- | --- | --- |
| `EB / IsBuzzerRequested` | Bool | 猶予中および非常保持中のブザー要求 |
| `EB / IsEmergencyBrakeRequested` | Bool | EB装置の非常ブレーキ要求 |
| `EB / InactivitySeconds` | Float | 無操作時間（秒） |
| `EB / RemainingSeconds` | Float | 非常作動までの残り時間（秒、初期65、ブザー開始時5、下限0） |

前後とも同じキーを使い、LocalBusの車両indexで区別する。MasterBusへの集約は行わない。これらはEB装置の計算結果であり、入力通信の正常性を保証するタグではない。入力欠損時は、作動前ならタイマーのリセット結果、作動後なら保持中の非常要求が公開される。保持中は無操作時間を作動時の値に固定し、残り時間は0とする。

起動直後、一度も計算していないOutputは `HasOutput == false` として公開しない。送信元またはEBコンポーネントが無効な場合は、次の収集でこの4タグを削除する。他装置のタグは変更しない。未割り当ての装置は収集対象にならない。

共有の `EbDevice.prefab` にBusSourceを追加済み。Tc1・Tc2の車両定義はこのPrefabを参照するため、通常のEquipment Builderによる生成で送信元も追加される。別の独自EB Prefabを作る場合は、同じGameObjectにこのBusSourceを配置する。

## 更新順と接続範囲

現在のSimulationは、各ステップの冒頭でTIMSの転送間隔を判定し、収集時刻ならBusSourceを収集してから、全Equipmentの入力収集・計算・出力反映を行う。従ってEBの公開値は**直近のTIMS収集で受信した計算結果**であり、作動・リセットの結果は次のTIMS収集で反映される。BusSourceはEBの計算や時間進行を行わない。

有効運転台の判定はEBの入力収集時にMasterBusにある値を使う。`TimsDirectionController.CalculateAndPublish()` が有効運転台タグを公開するための既存の入口であり、上位からEB入力収集前に呼ぶ必要がある。`TimsControlController` を配置した編成では、通信収集後に方向Controllerが自動実行される。タグが未公開の環境では、前後ともEB監視を停止する。

`TimsControlController` は公開されたEB要求を集約し、実際の力行遮断・最大空気制動へ接続する。TIMSは非常を保持せず、その時点で受信済みの要求を使う。EB装置が解除した結果も、次のTIMS収集で反映される。Spaceからの専用リセット操作は接続済み。Integrationが有効運転台の`EbDevice.RequestReset()`へ要求し、次のtickで一度だけ作動前のタイマーを0秒に戻す。作動後の非常保持と解除条件は変えない。予告ブザーはPresentationの`CabAudioController`へ接続済み。注目している編成の有効運転台でのみ再生する。3D運転台の物理ボタンとEB状態用のTIMS画面の生成は未接続。詳細は [ControlPipeline.md](ControlPipeline.md) を参照。

## 検証

EditModeテストで、EB Logic、MasterBusからの運転台判定、実Prefabの送信部品、前後別LocalBusへの公開、前ステップ値の公開、操作によるリセット、前後切替、無効・欠損・不正な運転台情報、初回計算前、コンポーネント無効、別編成との分離を確認する。

2026-09-15、Unity `6000.4.0f1` の検証用プロジェクトで `EbDevice` を対象にEditModeテストを実行し、21件成功・失敗0件。作業中のEditorを閉じず、自作アセットをコピーし、描画不要のテストに必要なパッケージで実行した。変更したC#とPrefabが作業ツリーと一致すること、追加GUIDとPrefab参照、`git diff --check` を確認した。実際のSceneの運転操作やURP描画を検証したものではない。

2026-09-25、非常保持をTIMSからEB装置へ移した。Unity `6000.4.0f1` の分離した検証用プロジェクトで、`EbDeviceLogicTests`、`TimsControlEmergencyTests`、`TimsBrakeNewLogicTests`のEditModeテスト59件が成功、失敗0件。走行中の手動非常解除、EB要求の保持・停車時解除、入力復旧後にTIMSが非常を持ち越さないことを確認した。変更したBusSourceテストを含む関連C#は分割Assemblyでコンパイル確認済み。実Sceneでのキー操作とPrefabを使うBusSourceテストは今回の実行対象外。

2026-09-25、60秒の監視＋5秒のブザー猶予へ変更し、非常解除まで鳴動を保持するようにした。関連EditModeテスト118件、AudioSourceを使うPlayModeテスト5件が成功。詳細は[検証記録](../../Validation/W1-04-eb-buzzer-2026-09-25.md)を参照。
