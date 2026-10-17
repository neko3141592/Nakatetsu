# TD-ATCからの再利用候補

TD-ATC (`/Users/yudai/TD-ATC`) の見た目用アセットを用途別に整理した候補置き場。元プロジェクトは変更していない。
PrefabをSceneへ配置して比較し、採用が決まったものを `Assets/Nakatetsu` の担当機能へ移動する。モデルと参照素材は `.meta` ごと移動する。

## フォルダ

| フォルダ | 内容 |
| --- | --- |
| `01_線路設備` | レール・枕木・架線柱・柵・速度標・勾配標・停止位置標・進路表示器・トンネル |
| `02_駅設備` | 駅柱・照明・駅名標・番線標・ベンチ・駐輪ラック |
| `03_建物・沿線` | 住宅・店舗・ビル、108種類の背景ビル、JapaneseResidential20、INDIEPLAYの組立部品 |
| `04_車両・運転台` | 車両外観の候補・試作モデル、運転台・運転机・モニター・LED表示器 |
| `05_地下駅` | SubwayPlatformのホーム・天井・柱・照明・点字ブロック・線路 |
| `共通素材` | 各Prefabの参照マテリアル・テクスチャ・文字表示素材 |

`Prefabs` または外部パックの `Prefabs` / `prefab` を入口として使う。外部パック内の階層とファイル名は維持した。
`候補一覧.json` に、候補と依存素材の区別、コピー元、コピー先、調整内容を記録した。

## 移植時の調整

- 全コピーのGUIDを新規発行し、候補同士の参照を書き換えた。既存NakatetsuのアセットとUnityパッケージの参照はそのまま利用する。
- 進路表示器・勾配標・可変速度標の旧変数バインダー／旧制御スクリプト、住宅の `SplineBuildingSize`、運転机の `MasterControllerLeverAnimator` を候補Prefabから除去した。現在保存されている文字・姿勢を表示する静的候補であり、値の更新やレバーの動作は採用後にNakatetsuへ接続する。
- Unity UI、TextMesh Pro、URPの標準コンポーネントは維持した。旧プロジェクトのC#コード・制御用Prefab・Sceneは取り込んでいない。
- 元から参照切れのマテリアル用テクスチャは候補側だけ未割当へ変更した。詳細は下記と `候補一覧.json` に記録する。
- `MonitorSingle.fbx` と `MonitorCover2.fbx` は既存Nakatetsuと完全一致するため複製していない。

## 除外した主なもの

- `Train/Equipment` のBrakeUnit・VVVFUnit、旧音声／制御機能を含む `1000_Tc1.prefab`。
- `Prefabs/Track/Legacy`、TMPのサンプル、SkySeriesのデモ。
- FBX参照切れのTakamizu moha1101／moha1501の旧Prefab。
- カメラと照明を含む `GenericHighRise.prefab`。建物の元FBX・素材を候補へ入れた。
- 完成済みの別候補と重なる、旧車両モデルの中間版。

## 出典・利用条件

`03_建物・沿線/ThirdParty/JapaneseResidential20/README.md` と `03_建物・沿線/ThirdParty/Buildings/japanese-residential-home-02/LICENSE.md` を同梱した。元作者・配布元・利用条件はその資料を参照する。
その他の外部素材はTD-ATC内に利用条件の資料が見つからなかったため、配布・公開する際は元の配布元で条件を確認する。

## 収集結果

- FBX: 65個、Prefab: 253個、OBJ: 59個。
- 依存素材を含めて874ファイル（`.meta` を除く）、約468.94 MiB。
- Unity / URP: 両プロジェクトとも6000.4.0f1 / 17.4.0。

### 元から参照切れだったテクスチャ

- `共通素材/Materials/Track/Signs/SpeedLimit/Plate.mat`: 元から参照切れのテクスチャを未割当へ変更: e78bbf6edd34e45ae8207e2d9d2a0a23

### 参照未解決で候補から除外した素材

- `Assets/ThirdParty/Buildings/INDIEPLAY/JapaneseBuildings01/JPBM01Shop.prefab`: 未解決GUID 7dcf1714868b62c4b96046192e583884 in Assets/ThirdParty/Buildings/INDIEPLAY/JapaneseBuildings01/JPBM01Shop.prefab
- `Assets/ThirdParty/Buildings/INDIEPLAY/JapaneseBuildings02/JapaneseBuildingModel04.prefab`: 未解決GUID 7dcf1714868b62c4b96046192e583884 in Assets/ThirdParty/Buildings/INDIEPLAY/JapaneseBuildings02/Prefabs/JapaneseBuildingModel04/JapaneseBuildingModel041F.prefab

## Unityでの読み込み確認と追加整理

- `CatenaryPole.prefab` は元から7個のメッシュ参照が切れていたため除外した。`CatenaryPole.fbx`、`CatenaryPoleSource.fbx` と `SingleCatenaryPole.prefab` は残した。
- MobBuildingSetの未使用の旧マテリアル3個を除外し、現行のURPマテリアルを残した。
- GenericHighRiseのFBXは、カメラ・照明を取り込まない設定にした。
- INDIEPLAYの一部の元MTLは、配布元からの古い絶対パスや、元プロジェクトにもないテクスチャ名を含む。Prefabは同梱のマテリアルを直接参照している。素材を選ぶ際は整理済みPrefabを優先し、OBJを直接使う場合は同梱マテリアルを割り当てる。

### 検証結果（2026-10-13）

Unity 6000.4.0f1のバッチ読み込みで、Prefab 253個、FBX 65個・OBJ 59個、外部マテリアル250個、メッシュ1322個を検査した。Missing Script、未解決のオブジェクト参照、欠落メッシュ／マテリアル／シェーダーは0件。GUID重複と候補内の未解決参照も0件。見た目の採用判断は各候補をSceneへ置いて行う。
