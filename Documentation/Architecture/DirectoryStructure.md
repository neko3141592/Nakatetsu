# ディレクトリ構成

新プロジェクト `Nakatetsu` の配置規則をまとめる。
作業先は `/Users/yudai/Documents/Unity/Nakatetsu`。
現行の `/Users/yudai/TD-ATC` は文化祭用の本番版として扱い、明示的な依頼なしに変更しない。

## 現在の主要構成

2026-09-13に、`Assets/Nakatetsu/Train` 以下の車両機器を `Equipment` 配下へ再編した。
`Runtime` や `Presentation` を単なる置き場として使わず、責務を確定できるアセットは機能の末端にある `Scripts`、`Prefabs`、`Data`、`Sprites`、`Tests` へ置く。

```text
Nakatetsu/
├── Assets/
│   ├── Nakatetsu/
│   │   ├── Application/
│   │   │   └── Runtime/
│   │   ├── Core/
│   │   │   └── Runtime/
│   │   ├── Track/
│   │   │   └── Geometry/
│   │   │       ├── Scripts/
│   │   │       ├── Data/
│   │   │       └── Tests/
│   │   ├── Train/
│   │   │   ├── Composition/Scripts/
│   │   │   ├── Consist/Definitions/{Scripts,Data}/
│   │   │   ├── Equipment/
│   │   │   │   ├── Shared/Scripts/
│   │   │   │   ├── Brake/
│   │   │   │   │   ├── Cylinder/{Scripts,Data,Prefabs,Tests}/
│   │   │   │   │   └── ControlDevice/{Scripts,Tests}/
│   │   │   │   ├── LoadWeightDevice/Scripts/
│   │   │   │   ├── Operation/
│   │   │   │   │   ├── CabActivationSwitch/{Scripts,Prefabs}/
│   │   │   │   │   ├── MasterController/{Scripts,Prefabs,Tests}/
│   │   │   │   │   └── Switches/{Scripts,Tests}/
│   │   │   │   ├── Traction/
│   │   │   │   │   ├── Scripts/
│   │   │   │   │   ├── Drive/{Scripts,Definitions}/
│   │   │   │   │   ├── Electrical/Scripts/
│   │   │   │   │   ├── Motor/{Scripts,Data}/
│   │   │   │   │   └── Vvvf/{Scripts,Data,Prefabs}/
│   │   │   │   └── Tims/
│   │   │   │       ├── Brake/{Scripts,Tests}/
│   │   │   │       ├── Bus/{Scripts,Tests}/
│   │   │   │       ├── Communication/{Scripts,Tests}/
│   │   │   │       ├── Configuration/{Scripts,Data}/
│   │   │   │       ├── Notch/{Scripts,Tests}/
│   │   │   │       ├── Operation/
│   │   │   │       ├── Traction/{Scripts,Tests}/
│   │   │   │       ├── Shared/Scripts/
│   │   │   │       └── Display/
│   │   │   │           ├── Indicators/{Scripts,Prefabs}/
│   │   │   │           ├── Notch/{Scripts,Prefabs}/
│   │   │   │           └── SpeedMeter/{Scripts,Sprites,Documentation}/
│   │   │   └── Simulation/Orchestration/Scripts/
│   │   ├── World/
│   │   │   └── Runtime/
│   │   ├── Content/
│   │   └── Settings/
│   ├── Scenes/
│   ├── Settings/
│   └── ThirdParty/
├── SourceAssets/
├── Documentation/
│   └── Architecture/
│       └── DirectoryStructure.md
├── Packages/
└── ProjectSettings/
```

テンプレート由来の補助ファイルやUnityの生成フォルダは省略している。
文書フォルダ名は、現在存在する `Documentation` に統一する。`Docs` は別途作らない。

## 各フォルダの用途

| 場所 | 内容 |
| --- | --- |
| `Assets/Nakatetsu/Application` | 起動、シーン遷移、各モジュールの生成と接続、アプリ全体のUI |
| `Assets/Nakatetsu/Core` | 単位、共通の小さなデータ型、外部依存の少ないインターフェース |
| `Assets/Nakatetsu/Track` | 線形、線路グラフ、経路、閉塞、連動、駅設備、地上ATC |
| `Assets/Nakatetsu/Train/Consist` | 編成定義と車種別の編成データ |
| `Assets/Nakatetsu/Train/Composition` | 編成GameObjectのルートと、編成全体で共有する定義参照 |
| `Assets/Nakatetsu/Train/Equipment` | ブレーキ・運転操作・牽引装置などの車両機器 |
| `Assets/Nakatetsu/Train/Equipment/Shared` | 車両機器の生成・車両Index割り当てに使う共通コード |
| `Assets/Nakatetsu/Train/Equipment/Brake` | ブレーキシリンダー、ブレーキ制御装置などの基礎ブレーキ機器 |
| `Assets/Nakatetsu/Train/Equipment/Operation` | 運転台スイッチ、主幹制御器などの運転操作機器 |
| `Assets/Nakatetsu/Train/Equipment/Traction` | VVVF、主電動機、駆動力計算と牽引装置の共通契約 |
| `Assets/Nakatetsu/Train/Equipment/Traction/Drive` | ギヤ比、車輪径、伝達効率などの駆動系定義 |
| `Assets/Nakatetsu/Train/Simulation/Orchestration` | 編成全体の時間進行と装置Stepの呼び出し順を統括 |
| `Assets/Nakatetsu/Train/Equipment/Tims` | TIMSの通信、バス、ノッチ、ブレーキ、力行、表示 |
| `Assets/Nakatetsu/World` | 沿線生成、カメラ、Floating Origin、ストリーミング |
| `Assets/Nakatetsu/Content` | 具体的な路線・車両のデータ、モデル、マテリアル、音声、Prefab |
| `Assets/Nakatetsu/Settings` | 自作機能のプロジェクト共通設定アセット |
| `Assets/Scenes` | 起動・開発確認用シーン。現在の配置を継続する |
| `Assets/Settings` | 既存テンプレートのURPなどの設定アセット |
| `Assets/ThirdParty` | 外部アセット。自作コード・素材と分け、ライセンス情報を保持する |
| `SourceAssets` | Blender・Illustrator・Photoshopなどの制作元ファイル |
| `Documentation` | 設計文書、移植記録、駅・路線・車両の参考資料 |
| `Packages` | パッケージの依存設定 |
| `ProjectSettings` | Unityプロジェクト全体の設定 |

`Core` に、単に複数機能で使うという理由だけで車両やTIMS固有の型を集めない。
責務を特定できない既存の設定アセットは推測で移動しない。

## コードの配置

機能・ドメインを上位に置き、ファイル種別は機能階層の末端に置く。

- `Scripts`：C#コード。
- `Prefabs`：その機能のPrefab。
- `Data`：ScriptableObject、設定、定義データ。
- `Sprites`、`Materials`、`Audio`：その機能で使う各アセット。
- `Tests`：その機能を検証するテスト。EditMode/PlayModeの区別はasmdefで表現し、必要がなければ `Tests/EditMode` のような中間階層を作らない。
- `Editor`：Unityが要求するEditor専用コード。必要な場合だけ機能配下に置く。

禁止例は `Tims/Scripts/Communication`。正しくは `Tims/Communication/Scripts` とする。同様に `Consist/Data/Series1000` ではなく `Consist/Series1000/Data` とする。

機能を移植した後の配置例：

```text
Assets/Nakatetsu/Train/Equipment/Brake/
├── Scripts/
├── Prefabs/
├── Data/
└── Tests/
```

ControllerはUnityとの接続、Logicは計算・判定、Contextは状態・入力・設定・出力・作業領域を担当する。
これらはファイル種別ではないため、ファイルが少ないうちは `Scripts` 内でさらに分割しない。存在しない種類の空フォルダも作らない。

## Assembly Definition

`Nakatetsu.Train.asmdef` と `Nakatetsu.Train.Equipment.Tims.asmdef` は、それぞれの機能ツリーを包含できる機能ルートに置く。TIMSは `Equipment` 配下でも独立Assemblyとし、テストは機能別の `Tests` に分散させ、`asmref` で `Nakatetsu.Train.Equipment.Tims.Tests.EditMode` を参照する。

## 必要になったら追加する構成

以下は配置予定であり、すべて作成済みという意味ではない。

```text
Assets/Nakatetsu/
├── Simulation/
│   ├── Service/Scripts/          # 運行・停車駅の順序
│   └── StationStop/Scripts/      # 停車判定
├── Track/
│   └── NtLine/{Data,Prefabs}/    # NT線固有の線路データ
├── Station/
│   └── NT01/{Data,Models,Prefabs}/
└── Train/
    └── Tims/
        └── Series1000/{Data,Sprites}/ # 1000系固有のTIMS画面・設定
```

定義を表すC#の型は担当機能の `Scripts` へ、具体的な定義アセットはその機能・車種の `Data` へ置く。
例えば `CarDefinitionAsset.cs` は `Train/Consist/Scripts`、1000系の定義 `.asset` は `Train/Consist/Series1000/Data` に置く。

起動・開発確認用シーンは、まず以下の2ファイルでよい。

```text
Assets/Scenes/
├── Bootstrap.unity              # 初期化・シーン読み込み
└── Sandbox.unity                # 最小構成での動作確認
```

確認用シーンが増えたら `Assets/Scenes/Sandbox` などに分ける。
Addressablesの設定は、導入時に `Assets/AddressableAssetsData` へ生成する。

## 文書・参考資料

```text
Documentation/
├── Architecture/                # 構成・依存関係・命名規則
├── Migration/                   # 移植計画・進捗・移植元コミット
├── Track/                       # 線路システムの設計
├── Train/
│   └── Tims/                    # 車両・TIMSの設計
├── World/                       # 景観・カメラなどの設計
└── References/
    ├── Routes/
    │   └── NtLine/
    │       └── Stations/
    │           └── NT01/
    │               ├── README.md    # 駅名・概要・出典・調査日
    │               ├── Photos/
    │               ├── Drawings/
    │               └── Documents/
    └── Vehicles/
        └── Series1000/
```

文書・資料ができた時点で必要なフォルダだけを追加する。
資料が少ない駅は駅フォルダへ直接配置してよい。駅フォルダには既存の駅IDを使用する。

## 制作元・参考資料・Unity用データの区別

| 用途 | 配置例 |
| --- | --- |
| 駅の参考写真・寸法資料・配線図PDF | `Documentation/References/Routes/NtLine/Stations/NT01` |
| 自作の駅モデルの制作元 | `SourceAssets/Routes/NtLine/Stations/NT01` |
| Unityで使う駅モデル・Prefab・駅データ | `Assets/Nakatetsu/Station/NT01/{Models,Prefabs,Data}` |
| 1000系の車両モデルの制作元 | `SourceAssets/Vehicles/Series1000/Models` |
| Unityで使う1000系のモデル | `Assets/Nakatetsu/Train/Consist/Series1000/Models` |
| 共通TIMS画像の制作元 | `SourceAssets/Train/Tims/Graphics` |
| 共通TIMS速度計画像の書き出し先 | `Assets/Nakatetsu/Train/Equipment/Tims/Display/SpeedMeter/Sprites` |

`.blend`、`.ai`、`.psd` などの制作元はAssets外へ置く。
制作元と書き出し先の基本名をそろえ、履歴はGitで管理する。
フォルダ名は英語のPascalCaseを基本とし、`NT01` など正式なIDは既存形式を維持する。
