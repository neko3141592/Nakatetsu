# ディレクトリ構成

新プロジェクト `Nakatetsu` の配置規則をまとめる。
作業先は `/Users/yudai/Documents/Unity/Nakatetsu`。
現行の `/Users/yudai/TD-ATC` は文化祭用の本番版として扱い、明示的な依頼なしに変更しない。

## 現在の主要構成

```text
Nakatetsu/
├── Assets/
│   ├── Nakatetsu/
│   │   ├── Application/
│   │   │   └── Runtime/
│   │   ├── Core/
│   │   │   └── Runtime/
│   │   ├── Track/
│   │   │   └── Runtime/
│   │   ├── Train/
│   │   │   ├── Runtime/
│   │   │   ├── Tims/
│   │   │   │   └── Runtime/
│   │   │   └── Presentation/
│   │   │       └── Runtime/
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
| `Assets/Nakatetsu/Train/Runtime` | 編成、運動、ブレーキ、力行、運転操作、車上ATC |
| `Assets/Nakatetsu/Train/Tims` | 共通TIMSバス、制御、ロジック、表示基盤 |
| `Assets/Nakatetsu/Train/Presentation` | 運転台の計器、音、アニメーションなどの表示・演出機構 |
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
既存の設定アセットは、この構成整理のためだけに移動しない。

## コードの配置

機能・ドメインを上位に置き、その中を必要に応じて分ける。

- `Runtime`：ゲーム実行時に使用するコード。各モジュールのasmdefを置く。
- `Editor`：Inspector拡張や制作ツール。必要になったら専用のEditor用asmdefとともに追加する。
- `Tests/EditMode`：計算・データ検証などのテスト。
- `Tests/PlayMode`：起動、シーン読み込み、Unity上の動作を確認するテスト。

機能を移植した後の配置例：

```text
Assets/Nakatetsu/Train/Runtime/Brake/
├── Controllers/
├── Logic/
└── Context/
```

ControllerはUnityとの接続、Logicは計算・判定、Contextは状態・入力・設定・出力・作業領域を担当する。
ファイルが少ないうちは同じ機能フォルダへ置き、空の下位フォルダを大量に作らない。

## 必要になったら追加する構成

以下は配置予定であり、すべて作成済みという意味ではない。

```text
Assets/Nakatetsu/
├── Simulation/
│   └── Runtime/
│       ├── Service/              # 運行・停車駅の順序
│       └── StationStop/          # 停車判定
└── Content/
    ├── Routes/
    │   └── NtLine/               # NT線固有のデータ・シーン・Prefab
    ├── Vehicles/
    │   └── Series1000/
    │       └── Tims/             # 1000系固有のTIMS画面・設定
    └── Shared/                   # 複数路線・車両で共用する素材
```

定義を表すC#の型は担当モジュールへ、具体的な定義アセットは `Content` へ置く。
例えば `CarDefinition.cs` は `Train/Runtime/Consist`、1000系の定義 `.asset` は `Content/Vehicles/Series1000` に置く。

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
| Unityで使う駅モデル・Prefab・駅データ | `Assets/Nakatetsu/Content/Routes/NtLine/Stations/NT01` |
| 1000系の車両モデルの制作元 | `SourceAssets/Vehicles/Series1000/Models` |
| Unityで使う1000系のモデル | `Assets/Nakatetsu/Content/Vehicles/Series1000/Models` |
| 共通TIMS画像の制作元 | `SourceAssets/Train/Tims/Graphics` |
| 共通TIMS画像の書き出し先 | `Assets/Nakatetsu/Train/Tims/Art` |

`.blend`、`.ai`、`.psd` などの制作元はAssets外へ置く。
制作元と書き出し先の基本名をそろえ、履歴はGitで管理する。
フォルダ名は英語のPascalCaseを基本とし、`NT01` など正式なIDは既存形式を維持する。
