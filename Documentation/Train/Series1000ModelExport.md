# Series1000 FBX書き出し

2026-09-20。Blender 5.1.0で開いている最適化版の現在状態（未保存編集を含む）から出力。制作元の.blendは上書きしていない。

## 出力

`Assets/Nakatetsu/Train/Series1000/Models/` に1両ずつ保存。

| ファイル | メッシュ | 三角形換算 |
|---|---:|---:|
| Kuha1001.fbx | 21 | 150,032 |
| Moha1001.fbx | 20 | 151,036 |
| Moha1501.fbx | 30 | 155,180 |
| Moha1601.fbx | 25 | 152,848 |

## 座標・階層

- 各車の車体中心の平面位置を原点とし、元シーンの高さ基準を維持。下端はおよそ0.014m。
- 長手方向はUnityのZ軸、上方向はY軸。先頭車の運転台側が+Zとなる向き。
- 1 Unity unit = 1 m。幅約2.86m、全長約20.69m（連結器等を含む）。
- 車両ルートと子オブジェクトのスケールを単位スケールへ整理し、実寸をメッシュへ反映。
- 車両ごとのルート、各戸板、パンタの関節階層を維持。カメラ・ライト・他車両は含めない。
- アニメーションクリップは生成しない。
- Unityの多角形変換で一部の面が破棄されるため、FBX出力時のみ三角形化する。元のBlenderメッシュは変更しない。

## 設定

- Blender: Forward -Z / Up Y、Scale 1、Apply Unit、FBX Units Scale、Normals Only、Triangulate Faces、Animation Off。
- Unity: Preserve Hierarchy、Bake Axis Conversionを有効化、Import Animationを無効化。

## テクスチャ

利用できた梱包済み画像を `Textures/` に出力し、FBXから相対参照。

- `M_shatai_Base_color.png`
- `M_Hokomaku.jpg`
- `M_takamizu1000_mohashatai_Base_color.png`

元モデルで参照切れだった以下の画像は出力できない。書き出し用のコピーから欠損画像参照を外し、元のBlenderには変更を加えていない。広告・LCD表示および一部の質感は、素材を用意してUnity側で設定する必要がある。

- `Kokoku.png`
- `Image`
- `M_shatai_Roughness.png`
- `M_shatai_Metallic.png`
- `M_takamizu1000_mohashatai_Metallic.png`
- `M_takamizu1000_mohashatai_Normal.png`
- `M_shatai_Normal.png`
- `M_takamizu1000_mohashatai_Roughness.png`

## Unity向けマテリアルと検証

- 9種類のマテリアルをUnity公式StandardUpgraderでURP/Litへ変換して `Materials/` に保存。各FBXのImporterから外部マテリアルへ割り当て済み。
- Unity 6000.4.0f1の隔離プロジェクトで4両ともインポートを確認。メッシュ数、三角形数、UV、法線、各車16枚の客用ドア、パンタ関節が一致。
- インスタンスのRoot Scaleは(1,1,1)。幅2.86m、全長20.69m、高さはパンタなし4.16m／あり5.76m。
- 利用可能な車体・方向幕テクスチャへの参照を確認。既存のUnityシーンには配置していない。

## 運転席の椅子・ワイパーを独立させた版

制作元は `SourceAssets/Train/Series1000/Blender/Series1000.blend`。このファイルに保存されたユーザーの形状編集・部品削除・材質・支点をそのまま引き継ぎ、4両の固定部品を結合した。元FBXや別のBlenderバックアップから部品・支点を補完しない。制作元ファイルは変更しない。

- 最適化版: `SourceAssets/Train/Series1000/Blender/Series1000_optimized_cab_movable.blend`
- Unity用: `Assets/Nakatetsu/Train/Series1000/Models/` の4つのFBX。
- Kuha1001の `DriverSeat` に椅子10部品、`Wiper` にワイパー3部品を保持。グループと各部品を個別に動かせる。
- 客用ドア各車16枚、乗務員扉、貫通扉、パンタグラフの階層も維持。固定部分のみBody_Static／Windows_Staticへ結合。
- 既存のUnity用9材質を参照し、窓を含むユーザーの材質設定を維持。
- 自動開閉・ワイパー往復・椅子の動作クリップは含まない。Unityで各Transformに動作を付けるためのモデル。
- 既存シーン上のモデルは自動で差し替えない。

| ファイル | 元メッシュ数 | 結合後 | 三角形換算 |
|---|---:|---:|---:|
| Kuha1001.fbx | 219 | 34 | 150,043 |
| Moha1001.fbx | 171 | 20 | 151,036 |
| Moha1501.fbx | 179 | 30 | 155,180 |
| Moha1601.fbx | 174 | 25 | 152,848 |
| 合計 | 743 | 109 | 609,107 |

結合前後で頂点数、三角形数、UV、材質割当てが一致し、頂点のワールド位置誤差は最大0.000004m未満。Unityの隔離プロジェクトで4両の形状・寸法・ドア枚数・パンタ関節数を確認し、椅子／ワイパーの試験回転が固定車体を動かさないことも確認。

### Kuhaマスコンの削除

最適化済みKuha車体から、制作元の `masucon` と一致する132頂点・256三角形だけを削除。運転台本体と椅子・ワイパーは維持。削除対象以外の形状、UV、材質割当て、カスタム法線が一致することを確認し、`Models/Kuha1001.fbx` のみ再出力した。制作元 `Series1000.blend` と他3両のFBXは変更していない。
