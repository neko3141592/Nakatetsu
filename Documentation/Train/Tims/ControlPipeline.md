# 最小TIMS指令パイプライン

`Assets/Nakatetsu/Train/Equipment/Tims/Composition/Prefabs/Tims.prefab` をTrainRoot配下に1つ配置する。通信、Direction、Notch、Traction、Brake、Speedと共通設定を含む。既に同一編成にTIMS通信オブジェクトがある場合は重複配置せず、そのオブジェクトへ `TimsControlController` と設定済み `TimsRoot` を追加する。

## 更新順

TrainSimulationControllerの測定 → 全LocalBus送信 → 速度公開 → `TimsControlController` がDirection → Notch → 力行・ブレーキの入力収集 → 力行・ブレーキ公開 → 通常Equipment → Motor/Brake/Load → Physics。新しいUpdateは追加しない。

制御パイプラインは `TimsCommunicationController.CollectInputSources()` から呼ばれる。低レベルの `CollectSources()` はLocalBusへの収集だけ。方向・ノッチ・力行・ブレーキControllerを別途毎フレーム呼ぶ必要はない。

## 入力と初期設定

- BC圧・空気制動力・車両質量は、BrakeControlDeviceがSimulationから受け取る測定値を専用BusSourceで公開するMVP。BC圧は実制動力と有効シリンダーの力/圧力係数から換算する。独立した応荷重センサー・圧力センサーのモデルは含めない。
- 質量は前ステップのLoad出力、初回のみ車両定義の空車質量を使う。
- モーター台数と車両全体の定格出力は既存のTimsTractionBusSourceから読む。Contextの1台あたり定格値には台数で割って格納する。
- リスト・指令配列は車両index順で、付随車の力行指令は0。
- 力行カーブの配列はP1から。横軸は停止=0、定加速域終端速度=1の正規化速度を使う。カーブ配列が空の場合は `力行ノッチ / 力行段数` の単純倍率とする。
- 同梱設定の起動加速度は既存の2.5 km/h/sを使用。空だった常用減速度表には試験用にB1=0.5からB7=3.5 km/h/sを0.5刻みで設定した。実車仕様の確定値ではない。

## 最小の制動と方向

常用ブレーキは既存の質量配分計算で全車に空気制動を指令する。回生協調は未接続で、回生指令は生成しない。ブレーキ中・力行N・入力欠落は前回の力行指令を残さず0にする。

方向は `Direction.ConsistDirectionSign` で別送し、VVVFの正/負のトルク指令（力行/回生）と混同しない。Simulationが実モーター力を編成方向に変換してPhysicsに渡す。反対方向へ移動中の力行は抑止し、停車してから逆方向へ発進できる。

EBの前ステップ出力、非常ノッチ、必要入力の欠損を非常要求として集約する。非常時は力行0と最大BC圧を指令する。EBが5 km/h未満で監視を解除しても、TIMSの非常は停車まで保持する。原因が消え、絶対速度0.01 m/s未満かつ力行Nになったら解除する。

通信Adapterを持つ装置の指令欠損は、VVVFでは力行0、BrakeControlDeviceでは最大空気制動とする。Adapterを使わない直接Setterの単独試験は従来どおり利用できる。

## 対象外

操作用キーボード/UI、車両の線路上移動、ATC/ATO、回生協調、定速運転、高出力モードは含めない。Sceneは変更していない。
