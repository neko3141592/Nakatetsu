# Japanese Residential 20

Unity 6 / URP向けにセットアップした、日本風住宅・集合住宅・小規模店舗の20棟セットです。

## 使い方

- `Prefabs/` にある `JR20_01`〜`JR20_20` をシーンへドラッグしてください。
- 全PrefabはURP/Litマテリアル、影、静的フラグ、BoxColliderを設定済みです。
- `JapaneseResidential20_Showcase.unity` で20棟をまとめて確認できます。
- 再生成はUnityメニューの `Tools > TD-ATC > Rebuild Japanese Residential 20` です。

## 内容

- 元メッシュ: 7種類
- 集合住宅・都市住宅系: 12棟（4メッシュ × 3Prefab）
- 2階建て住宅系: 3棟（1メッシュ × 3Prefab）
- 伝統的な家屋: 2棟（1メッシュ × 2Prefab）
- 古い店舗兼住宅: 3棟（1メッシュ × 3Prefab）

配布データに画像テクスチャがある素材は、その画像をURP/LitのBase Mapへ明示的に割り当てています。画像がない素材は、元FBXに記録されたマテリアル色をそのまま使用しています。Prefab名の配色サフィックスは既存参照との互換性のため残していますが、現在はすべて元モデルの見た目を優先します。

## 出典とライセンス

### Tokyo Building set（4メッシュ）

- 配布元: BOOTH「Free FBX」 https://booth.pm/ja/items/3845791
- 収録ファイル: `Tokyo_Building.001.fbx`〜`Tokyo_Building.004.fbx`
- 配布ページ記載条件: 商用利用・改変可、再配布禁止

### Low Poly Japan Building

- 作者: DarwinDOS / darwin.art
- 配布元: https://sketchfab.com/3d-models/low-poly-japan-building-d92fbbd18d304e6795656677e4445768
- ライセンス: Creative Commons Attribution（CC BY）

### Japanese low poly building

- 作者: NemerionRae
- 配布元: https://sketchfab.com/3d-models/japanese-low-poly-building-782403f71c8b48bc878e28c19dd8d20d
- ライセンス: Creative Commons Attribution（CC BY）

### Old Japanese Store | Lowpoly

- 作者: Frid.blend / Fridqeir
- 配布元: https://sketchfab.com/3d-models/old-japanese-store-lowpoly-baa1a12ed0984143adde52b5e165e288
- ライセンス: Creative Commons Attribution（CC BY）

モデルデータや改変データを単体で再配布しないでください。ゲーム・映像へ組み込んで公開する際は、上記CC BY素材の作者名と配布元URLをクレジットへ記載してください。
