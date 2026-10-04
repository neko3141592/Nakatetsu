# 最小TIMS指令パイプライン

`Assets/Nakatetsu/Train/Equipment/Tims/Composition/Prefabs/Tims.prefab` をTrainRoot配下に1つ配置する。通信、Direction、Notch、Traction、Brake、Speedと共通設定を含む。既に同一編成にTIMS通信オブジェクトがある場合は重複配置せず、そのオブジェクトへ `TimsControlController` と設定済み `TimsRoot` を追加する。

## 更新順

TrainSimulationControllerの測定 → LocalBus・MasterBusへの機器情報収集 → 速度公開 → `TimsControlController` がDirection → Notch → 力行・ブレーキの入力収集 → 力行・ブレーキ公開 → 通常Equipment → Motor/Brake/Load → Physics。新しいUpdateは追加しない。

制御パイプラインは `TimsCommunicationController.CollectInputSources()` から呼ばれる。低レベルの `CollectSources()` はLocalBusへの収集、`CollectMasterSources()` はMasterBusへの共通機器情報の収集だけ。方向・ノッチ・力行・ブレーキControllerを別途毎フレーム呼ぶ必要はない。

## 機器情報の収集周期

`TimsCommunicationController`の`localCollectionIntervalSeconds`と`masterCollectionIntervalSeconds`で、各車の機器と編成共通の機器の収集周期を個別に設定する。初期値はどちらも0.25秒。シミュレーション時間で計時し、初回は即時収集する。周期を超えた端数は次回へ持ち越し、0秒なら毎tick収集する。

各車の`ITimsBusSource`は車両割当先のLocalBusへ、編成共通の`ITimsMasterBusSource`はMasterBusへ現在値を書き込む。両方を同じtickで収集する場合、編成配下の検索は1回にまとめる。周期を待つ間も、TIMS内部の速度・方向・ノッチ・力行・ブレーキ計算は受信済みの値で毎tick実行する。

ATCの表示Adapterは`ITimsMasterBusSource`として収集する。通常の収集はEquipment計算より前に行うため、ATCが前tickまでに確定した表示情報を取得する。ATCの計算や位置更新をこの周期で間引く処理は含めない。

ATCのブレーキ指令は`TimsNotchController.CollectInput()`が毎tick直接読み取る。表示収集周期を待たず、前tickに確定した常用段と非常要求を手動ノッチへ合成する。常用は強い方の段、非常は最優先とし、ATCの計算から各車の制動指令への反映は1tick後となる。同じTrainRoot配下のATCを自動取得するため、Sceneでの追加配線は不要。

`TimsRoot`も`ITimsMasterBusSource`として、共通設定の常用減速度表・刻み数・常用最大段をMasterBusへ公開する。減速度はkm/h/sからm/s²へ変換し、ATCの入力Adapterが毎tick受信済みの設定を読む。ATCは設定Assetを直接参照しない。TimsRoot無効化・設定不正では設定タグを削除する。

引数なしの`CollectInputSources()`は手動収集用で、両方の現在値を即時収集し、両方の収集タイマーをリセットする。

## 入力と初期設定

- BC圧・空気制動力・車両質量は、BrakeControlDeviceがSimulationから受け取る測定値を専用BusSourceで公開するMVP。BC圧は実制動力と有効シリンダーの力/圧力係数から換算する。独立した応荷重センサー・圧力センサーのモデルは含めない。
- 質量は前ステップのLoad出力、初回のみ車両定義の空車質量を使う。
- モーター台数と車両全体の定格出力は既存のTimsTractionBusSourceから読む。Contextの1台あたり定格値には台数で割って格納する。
- リスト・指令配列は車両index順で、付随車の力行指令は0。
- 力行カーブの配列はP1から。横軸は停止=0、定加速域終端速度=1の正規化速度を使う。カーブ配列が空の場合は `力行ノッチ / 力行段数` の単純倍率とする。
- 同梱設定の起動加速度は既存の2.5 km/h/sを使用。空だった常用減速度表には試験用にB1=0.5からB7=3.5 km/h/sを0.5刻みで設定した。実車仕様の確定値ではない。

## 最小の制動と方向

常用ブレーキは全車共通の最低込め圧を確保し、残りの目標を各車の質量比で配分する。M車・T車の区分は維持し、VVVF搭載の有無・回生可能力・前ステップの実回生力をLocalBusから収集する。回生目標はVVVF搭載車の能力上限付きで均等配分し、実回生力の不足分を空気制動で補う。

`Brake.TargetRegenForcesN`をMasterBusへ公開し、常用ブレーキ中はAdapterが負のVVVF指令へ変換する。非常時は力行・回生指令とも0。回生指令欠損時も力行へ戻さない。空気制動は従来のN指令経路を使用し、TIMSのkPa出力を直接送信する経路は含めない。

方向は `Direction.ConsistDirectionSign` で別送し、VVVFの正/負のトルク指令（力行/回生）と混同しない。Simulationは正の実モーター力を編成方向付きの力行力へ、負の実モーター力を非負の回生制動力へ変換する。回生は空気制動と合算してPhysicsへ渡し、前進・後退とも速度に逆らわせる。反対方向へ移動中の力行は抑止し、停車してから逆方向へ発進できる。

EBの受信済み出力、非常ノッチ、必要入力の欠損を、そのステップの非常要求として集約する。非常時は力行0と最大BC圧を指令する。TIMSには非常の保持状態や停車待ちの解除判定を持たせない。手動の非常ノッチを戻し、ほかの非常要求がなければ、通信で操作が反映された時点で走行中でも非常制動を解除する。入力欠損による非常も、入力が復旧してほかの要求がなければ解除する。

EB作動後の保持・解除は`EbDeviceLogic`が担当する。作動した装置は、絶対速度0.01 m/s未満かつ自車のマスコンが力行0になるまで要求を保持する。B段でも力行0なら解除可能。5 km/h未満への減速、走行中のマスコン操作、入力欠損だけでは保持を解除しない。TIMSは装置から届く要求をそのまま集約する。空気ブレーキの実圧力は、非常指令解除後も既存の緩解速度に従って変化する。

通信Adapterを持つ装置の指令欠損は、VVVFでは力行0、BrakeControlDeviceでは最大空気制動とする。Adapterを使わない直接Setterの単独試験は従来どおり利用できる。

## 対象外

操作用キーボード/UI、車両の線路上移動、ATO、定速運転、高出力モードは含めない。Sceneは変更していない。
