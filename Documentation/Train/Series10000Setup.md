# Series10000 の車両設定

`Assets/Nakatetsu/Train/Series10000/Data/Series10000_Consist.asset` を既存の `TrainRoot.Consist Definition` に設定して使用する。Series1000と同じEquipment / Simulation / Presentation Builder、Physics、共通TIMSを使用する。

| 車両定義 / Body Prefab | 用途 | 車種 |
|---|---|---|
| Series10000_Tc1 / Tc1 | 先頭・運転台付き | Tc1 |
| Series10000_Mp / Mp | パンタ付きモーター車 | M |
| Series10000_M / M | パンタなしモーター車 | M |
| Series10000_T / T | 付随車 | T |
| Series10000_Tc2 / Tc2 | 後尾・運転台付き | Tc2 |

10両編成は `Tc1 - Mp - T - M - Mp - M - M - Mp - T - Tc2`。車両性能、質量、定員、VVVF・モーター・ブレーキ・速度センサー・EB・主幹制御器はSeries1000の対応車種の設定を引き継いでいる。Series10000の実車性能として新規調整した値ではない。車両長は20m、台車中心間距離は元モデルに合わせて13.6m。

## ドア

- 各車左右4組・計8組の `TrainDoorPresentation` を設定済み。
- 各組2枚の戸を前後へ0.65mずつ動かす。開閉時間は既存 `TrainDoorSimulation` の3秒。
- シミュレーションは親TrainRootと車両Indexから自動接続する。
- 左右は編成前方の+Z基準、番号は前方から0〜3。Tc2も同じ基準。
- 車両定義にDoor機器とDoor物理モデルを設定済み。操作は既存TIMS / DoorDebugPanelを使用する。
- 乗務員扉・貫通扉・乗務員窓は客用ドア制御の対象に含めない。

## TIMS・運転台

- Tc1 / Tc2の `Tc1_Monitor1`〜`Tc1_Monitor3` に画面番号1〜3を設定済み。
- 同じTrainRoot内のTimsRootから実行時のRenderTextureを自動取得する。
- 画面用メッシュは `Models/Cab/Meshes/Monitor1Screen.asset`〜`Monitor3Screen.asset`。元FBXの形状・法線を保ち、画面平面上でUVを0〜1へ展開している。
- `Materials/Cab/TimsMonitorScreen1.mat`〜`TimsMonitorScreen3.mat` はSeries1000の設定を複製。Emissionを含む。共通TIMS UIの編集はそのまま両系列に反映される。
- 前後の運転用 `TrainCameraAnchor` と、同じ車両のEBへ接続する `CabAudioController` を設定済み。
- Tc2はモデルを内部で180°回転させた後尾用Prefab。Prefabルートは回転0のまま使用する。

## デバッグ配置

既存の `TrainPresentationDebugSpawner` を使用する場合は、Series10000専用の `Prefabs/Debug/TrainPresentationDebugSpawner.prefab` をTrainRootの子へ配置する。10両分の車種参照を設定済み。Tc2を含め、追加の回転設定は不要。

通常の `TrainPresentationBuilder` とデバッグSpawnerを同時に有効にすると二重に車体が生成されるので、使用する生成方式を1つ選ぶ。

## 確認内容

Unityの一時Preview Sceneで実際のBuilderから10両を生成し、TrainSimulationControllerとTIMS Busを進めて確認した。

- 6両の駆動・モーター、10両のブレーキ・荷重・ドアモデルが生成される。
- 左開→左右切替→全閉で80組・160枚のドアが正しい側と方向へ移動し、閉位置へ復帰する。
- 開扉中はTIMSの力行許可が落ち、全閉で閉状態が復帰する。
- 両端6画面のBaseMap / EmissionMapに対応するTIMSの実行時RTが接続される。
- 運転視点・画面番号とUVの向き・客用ドアの全開状態をレンダリングで確認した。

既存Series1000のアセットと、編集中のSceneは変更していない。
