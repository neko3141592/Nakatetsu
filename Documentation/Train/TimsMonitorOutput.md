# TIMSモニター表示

## 起動時の流れ

`TimsRoot` が `Monitor Output Prefab` を子に生成し、画面数分のRenderTextureをメモリ上に作る。標準Prefabは3画面、解像度は1536×1024（3:2）。実行時に `.renderTexture` アセットを追加・変更しない。

`TimsMonitorOutput` が各CameraへRTを割り当て、速度計・ノッチ表示・圧力計・電流計・インジケーターを所有元のTIMS通信装置へ接続する。接続はUIのAwakeより先に行う。各計器の号車・タグ・手動表示値などの設定は維持する。

車内メッシュの `TimsMonitorScreen` は参照先TIMSと画面番号からRTを取得し、対象Rendererの `_BaseMap` と、対応するShaderなら `_EmissionMap` へ設定する。MaterialPropertyBlockを使うため共有Materialアセットのテクスチャや色は変更しない。Emissionの有効化・色・Intensityは材質側で設定する。

TIMSを無効化すると描画用オブジェクトを無効化し、再度有効にすると同じRTで描画を再開する。TIMSの破棄時にRTをRelease・Destroyする。RTはTIMSごとに独立し、描画用Camera・Canvasも別の描画位置に配置する。

## 配置と設定

1. `Assets/Nakatetsu/Train/Equipment/Tims/Composition/Prefabs/Tims.prefab` を配置する。`TimsRoot` の `Monitor Output Prefab` は設定済み。
2. 各モニターの `TimsMonitorScreen` の `Tims` に接続先のTimsRootを指定する。モニターがTIMSの子なら親から自動取得する。
3. `Screen Number` に1〜3、`Screen Renderer` に画面メッシュのRenderer、`Material Index` に対象の材質番号（0始まり）を設定する。
4. PlayするとTIMSの子に `Monitor Output` が作られ、RTが接続される。

`Assets/Nakatetsu/Train/Series1000/Prefabs/Body/Tc1.prefab` の `Presentation/Cab/CabModule/Monitor1`〜`Monitor3` は、それぞれ画面番号1〜3と自身のRendererを設定済み。実行時は親の `TrainRoot` 内から `TimsRoot` を自動取得する。各画面には、新しいCabModuleの頂点・法線・三角形を維持し、ローカルX/Z座標からUVを0〜1へ展開した `CabMonitor1Screen.asset`〜`CabMonitor3Screen.asset` を使用する。元FBXと各MonitorのTransformは変更しない。

旧 `Assets/Nakatetsu/Train/Series1000/Prefabs/Cab/TimsMonitor.prefab` の `Cube.001` / `Cube.002` は画面番号1 / 2の設定を維持する。

`TimsMonitorOutput` を別途Sceneへ配置する必要はない。以前の手動配置が残っている場合、必要なUI編集をPrefabへApplyしてから、その手動配置を無効にして二重描画を避ける。

## 画面の編集

`Assets/Nakatetsu/Train/Equipment/Tims/Display/Monitor/Prefabs/TimsMonitorOutput.prefab` を開き、次のCanvas上で部品を配置する。JSONは使用しない。

| 画面番号 | Canvas | Camera | Layer |
| --- | --- | --- | --- |
| 1 | TimsMonitorCanvas | TimsMonitorCamera | TimsMonitor |
| 2 | TimsMonitorCanvas2 | TimsMonitorCamera2 | TimsMonitor2 |
| 3 | TimsMonitorCanvas3 | TimsMonitorCamera3 | TimsMonitor3 |

Canvasの基準解像度は1536×1024。1枚目は既存TIMS表示、2・3枚目は空Canvas。Prefab内の各CameraとCanvasの相対配置・専用Layerは維持する。部品をコピーするときは、コピー先のCanvasと同じLayerに設定する。車内を映すCameraのCulling Maskからは3つのモニター用Layerを外す。

RT解像度は `TimsRoot` の `Monitor Resolution` で指定する。画面が歪まないよう3:2を維持する。

## 既存のアセット

`Display/Monitor/RenderTextures/` の3枚のRTと、`Series1000/Materials/Cab/TimsMonitorScreen*.mat` は編集時のプレビュー用として残す。実行時は新規RTをCameraとモニターに割り当てる。

`Series1000/Models/Cab/Meshes/MonitorScreenLeft.asset` と `MonitorScreenRight.asset` は既存のUV補正版メッシュ。元FBXは変更しない。

Sceneへの配置や未保存編集は自動保存しない。
