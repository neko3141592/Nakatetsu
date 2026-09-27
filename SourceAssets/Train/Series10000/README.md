# Series10000 車両別エクスポート

元データ: `/Users/yudai/Downloads/電車/電車.blend`。元ファイルは変更していません。

編集用ファイルは `Blender/Series10000_UnityExport.blend` です。車種ごとに4つのシーンを収録し、使用テクスチャをパックしています。

| シーン / FBX | 車種 | 元編成の車両 | 独立したメッシュ |
|---|---|---:|---|
| Series10000_Tc1 / Tc1.fbx | 運転台機器付き先頭車 | 1両目 | 車体1、ドア20、運転台機器10、乗務員窓6 |
| Series10000_Mp / Mp.fbx | パンタ付きモーター車 | 2両目 | 車体1、ドア18、パンタ4 |
| Series10000_M / M.fbx | パンタなしモーター車 | 3両目 | 車体1、ドア18 |
| Series10000_T / T.fbx | 付随車 | 4両目 | 車体1、ドア18 |

M/Tは元編成の対応する中間車から抽出しています。機器のモーター車・付随車専用形状を新規に作成したものではありません。

## Unityアセット

- FBX: `Assets/Nakatetsu/Train/Series10000/Models`
- URP Litマテリアル: `Assets/Nakatetsu/Train/Series10000/Materials`
- 元画像と同一のカラーパレット・メタリック画像: `Assets/Nakatetsu/Train/Series10000/Textures`

運転台機器・ドア・パンタ・乗務員窓以外は各車の `*_Body_Static` に統合しています。マテリアルの区分とUVは保持しています。台車・車輪も指定に従って車体に統合済みです。乗務員窓は左右それぞれ枠・前側ガラス・後側ガラスに、パンタは台座・下枠・上枠・集電舟に分割しています。

Blenderの屈折ガラスは、Unityでは透明なURP Litマテリアルで近似しています。元からマテリアルのなかった追加運転台部品にはグレーを設定しています。

## 座標と面の向き

各車の全体寸法の前後・左右中心を原点にしています。高さ方向は元データの車輪接地高さを保持しています。Unityでは上が+Y、先頭方向が+Zで、ルート回転0・スケール1です。

マイナススケールで左右反転されていた座席等は、変換をメッシュに適用する際に面の向きも補正しています。編集用Blenderの多角形は維持し、FBX書き出し時のみ三角形化しています。

## 再書き出し

プロジェクトルートから次を実行します。Blenderファイルを保存し直さず、4シーンを既存FBXに書き出します。Unity側のマテリアル対応はFBXの `.meta` に保存されています。

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background SourceAssets/Train/Series10000/Blender/Series10000_UnityExport.blend \
  --python SourceAssets/Train/Series10000/Blender/export_fbx.py
```

元オブジェクトと出力部品の対応は `export_manifest.json` に記録しています。以前の `Tc1.fbx` は `FBX/Backups/Tc1_before_split_20260926.fbx` に退避しています。

## Unityの動作設定

車両Prefab・ドア・TIMS・編成定義は `Documentation/Train/Series10000Setup.md` を参照してください。
