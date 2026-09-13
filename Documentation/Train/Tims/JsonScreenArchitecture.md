# TIMS JSON画面・コンポーネント設計案

## 1. 目的と結論

本書は、TIMSの表示部品を再利用可能にし、次の機能を安全に実装するための設計方針を定める。

- 1536 x 1024を論理画面サイズとするJSON定義からの画面描画
- 画面内ボタンによるTIMSページ遷移
- ボタン操作を起点とする車両の遠隔制御
- TIMSバスや画面状態から表示部品へのデータバインディング

**結論として、現段階ではuGUIを維持し、JSONは「許可済みコンポーネントの配置とバインド」を記述する宣言データに限定する。** JSONから任意のUnity型、メソッド、Prefabパスを直接指定する方式にはしない。`TimsScreenHost` が画面定義を読み、`TimsComponentRegistry` のホワイトリストからPrefabを生成し、表示用の読み取り専用データと制御用コマンドを別経路で接続する。

この方式は既存のCanvas、uGUI部品、TextMesh Pro資産をそのまま利用でき、座標精度、段階的な移行、遠隔制御の安全性のバランスがよい。UI Toolkitへの全面移行は、現在の試作を捨てるコストに対する利点がまだ小さい。

## 2. 現在の実装から分かること

### 2.1 既に使える土台

- `Assets/Scenes/Prototype/Tims.unity` にはScreen Space - CameraのuGUI Canvas、`GraphicRaycaster`、`CanvasScaler`があり、基準解像度は既に1536 x 1024である。
- 同シーンのUIは背景、Image、`NotchCell` Prefabなどを直接配置した試作段階であり、ページ管理や定義ローダーはまだない。
- `Assets/Nakatetsu/Train/Equipment/Tims/Display/Indicators/Prefabs/BoolIndicator.prefab` と、同機能の `Scripts/TimsBoolIndicator.cs` があり、部品をPrefab化する方向は既に始まっている。
- `CircularGaugeScale` はuGUIの `MaskableGraphic` として実装されている。今後のメーター部品もuGUIで揃えると再利用できる。
- Noto Sans JPのTextMesh Proフォント資産が用意されている。

### 2.2 データ側の現状

- `TimsBusState` は `TimsTagKey(DeviceName, ItemName)` をキーに、Bool、Int、Float、Stringと各種配列を保持する。型付きの `TryGet...` があるため、表示バインドの読み取り元に利用できる。
- バスは現在、変更通知やrevisionを持たない。毎フレームすべての表示を無条件更新すると文字列生成やレイアウト更新が増えるため、表示層側に差分比較が必要である。
- `TimsCommunicationController` は編成長に応じた端末とMasterBusを初期化するところまでで、送信元の収集、定期送信、画面との接続は未完成である。
- ノッチ、ブレーキ、力行はContext/LogicとしてUIから分離されている。この分離を維持し、UIボタンからLogicや車両コンポーネントを直接呼ばないことが重要である。
- JSONライブラリのNewtonsoft.Json 3.2.1はパッケージロック上では推移依存として存在するが、`manifest.json` の直接依存ではない。実装時に利用するなら直接依存として固定する。

### 2.3 現状の課題

1. Scene/Prefab内の配置が画面定義を兼ね、車種別ページの差し替えが難しい。
2. 表示部品がどのバス値を読むかについて統一された契約がない。
3. ページ遷移と車両制御を同じButtonイベントで自由に接続すると、権限やインターロックを迂回できる。
4. JSONの構文、型、参照先、座標を検証する仕組みがない。
5. 表示不能、通信途絶、制御拒否をどのように画面へ出すかが未定義である。

## 3. 採用する全体構成

```text
JSON/TextAsset
    │ parse + validate
    ▼
TimsScreenCatalog ── pageId解決 ── TimsNavigator
    │                                │
    ▼                                ▼
TimsScreenHost ── create ── TimsComponentRegistry ── Prefab/View
    │                                      │
    ├──── TimsBindingEngine ───────────────┤ 表示更新
    │             │                        │
    │             ▼                        │
    │       ITimsReadModel                 │
    │       ├─ MasterBus adapter           │
    │       ├─ LocalBus/car adapter        │
    │       └─ ViewState                   │
    │                                      │ 操作イベント
    ▼                                      ▼
TimsActionDispatcher ── navigate ── TimsNavigator
    │
    └─ command ── ITimsCommandGateway ── validate/interlock ── 車両制御
                                      └─ result/ack ── ViewState
```

責務の境界は次のとおりとする。

| 要素 | 責務 | 禁止事項 |
| --- | --- | --- |
| `TimsScreenHost` | ページの生成・破棄、部品ID管理、バインド開始・停止 | 車両制御の実行 |
| `TimsComponentRegistry` | `type` から許可済みFactory/Prefabを返す | JSONの任意パスからのロード、reflection生成 |
| `ITimsComponentView` | 定義を受けて見た目を構成し、操作イベントを通知 | MasterBusの直接探索、ページ遷移 |
| `TimsBindingEngine` | データ取得、変換、差分更新、欠損状態の通知 | バスへの書き込み |
| `TimsNavigator` | page stack、遷移、戻る、初期ページ | 車両コマンドの実行 |
| `TimsActionDispatcher` | JSON actionを許可済みハンドラーへ振り分け | メソッド名による任意呼び出し |
| `ITimsCommandGateway` | コマンド受付、検証、送信、結果通知 | UIオブジェクトへの依存 |

## 4. 描画基盤と1536 x 1024座標

### 4.1 uGUIを継続する理由

- 現在のCanvasと部品がuGUIであり、作り直しが不要。
- `RectTransform` の絶対座標を使う計器画面との相性がよい。
- Prefabに画像、TMP、独自 `Graphic`、アニメーションをまとめ、JSONを簡潔にできる。
- 画面ごとに大量のGameObjectをSceneへ保存せず、実行時にページ単位で生成できる。

JSONはレイアウトエンジンそのものにしない。特殊な計器や複雑な一組の表示は1つのPrefab/コンポーネントにまとめ、JSON上では1ノードとして配置する。細かな線や文字をすべてJSONノードにすると、定義が巨大化し、ロード時間と保守性が悪化する。

### 4.2 座標規則

- 論理領域は常に幅1536、高さ1024。
- JSON座標は左上原点、xは右向き、yは下向きとする。画面設計図と一致しやすい。
- `TimsScreenHost` 直下ではanchor/pivotを左上 `(0, 1)` に統一し、`anchoredPosition = (x, -y)` へ変換する。
- `rect` は `{ x, y, width, height }`、回転は時計回りdegree、`zIndex` は小さいものから先に生成する。
- Rootは1536 x 1024固定とし、CanvasScalerは現在と同じScale With Screen Sizeを用いる。
- 実表示のアスペクト比が3:2でない場合は、**引き伸ばさず3:2を維持してletterbox**する。操作座標は表示領域から論理座標へ逆変換する。
- 画像は可能なら整数座標・整数サイズとし、細線のにじみをVisual Reviewで確認する。

CanvasScalerの `matchWidthOrHeight` だけでは異なるアスペクト比で余白規則が曖昧になるため、1536 x 1024の `AspectRatioFitter` 相当のコンテナをCanvas内に置き、その下だけをJSON描画領域とする。

## 5. JSON仕様案

### 5.1 方針

- UTF-8、JSON Schemaで検証できる形式とする。
- ルートに必ず `schemaVersion`、`screenId`、`designSize`、`components` を持つ。
- 未知のmajor version、重複component ID、未知のtype/action/converter、領域外rect、循環する遷移依存はロードエラーとする。
- 色は `#RRGGBB` または `#RRGGBBAA`、リソースは論理IDを使用する。
- UnityのGUIDや `Resources` パスは画面JSONへ書かない。論理IDから実アセットへの対応はRegistry/ScriptableObject側で管理する。
- 製品版で外部JSONを許可する場合も、読み込めるcomponent/action/assetをホワイトリストに限定する。

### 5.2 画面例

```json
{
  "$schema": "./tims-screen.schema.json",
  "schemaVersion": "1.0",
  "screenId": "series1000.home",
  "designSize": { "width": 1536, "height": 1024 },
  "background": "#272A31FF",
  "components": [
    {
      "id": "speed",
      "type": "numericText",
      "rect": { "x": 640, "y": 72, "width": 256, "height": 96 },
      "props": { "font": "notoSansJp.bold", "fontSize": 64, "suffix": " km/h" },
      "bindings": {
        "value": {
          "source": "masterBus",
          "tag": { "device": "Train", "item": "SpeedMps" },
          "expectedType": "float",
          "converter": "mpsToKmh",
          "format": "0",
          "fallback": "---"
        }
      }
    },
    {
      "id": "doorClosed",
      "type": "boolIndicator",
      "rect": { "x": 96, "y": 160, "width": 240, "height": 64 },
      "props": { "label": "戸閉", "onColor": "#35E06FFF" },
      "bindings": {
        "isOn": {
          "source": "masterBus",
          "tag": { "device": "Doors", "item": "AllClosed" },
          "expectedType": "bool",
          "fallback": false,
          "staleAfterMs": 500
        }
      }
    },
    {
      "id": "monitorButton",
      "type": "button",
      "rect": { "x": 1248, "y": 896, "width": 240, "height": 88 },
      "props": { "label": "モニタ" },
      "actions": {
        "click": { "type": "navigate", "pageId": "series1000.monitor" }
      }
    },
    {
      "id": "resetFaultButton",
      "type": "commandButton",
      "rect": { "x": 984, "y": 896, "width": 240, "height": 88 },
      "props": { "label": "故障リセット", "confirmation": "hold", "holdMs": 1500 },
      "actions": {
        "confirmed": {
          "type": "command",
          "commandId": "fault.reset",
          "target": { "scope": "car", "index": 2 },
          "parameters": { "group": "door" }
        }
      },
      "bindings": {
        "interactable": { "source": "viewState", "path": "permissions.faultReset" },
        "pending": { "source": "viewState", "path": "commands.fault.reset.pending" }
      }
    }
  ]
}
```

`props` と `bindings` の許容フィールドはcomponent typeごとに決める。自由なプロパティバッグをそのままreflectionで流し込まず、Factoryが型付き定義へ変換・検証する。

### 5.3 初期コンポーネント一覧

| type | 主用途 | 主なbinding target |
| --- | --- | --- |
| `panel` | 背景、枠、グループ | `visible`, `color` |
| `label` | 固定文字 | `visible`, `text`, `color` |
| `numericText` | 速度、圧力、力 | `value`, `visible`, `color` |
| `image` | アイコン、車両模式図 | `visible`, `sprite`, `tint` |
| `boolIndicator` | 戸閉、故障、状態灯 | `isOn`, `visible`, `alarm` |
| `barGauge` | 圧力、力行、ブレーキ | `value`, `minimum`, `maximum` |
| `circularGauge` | 速度計など | `value`, `warning` |
| `button` | ページ遷移、戻る | `interactable`, `visible`, `label` |
| `commandButton` | 車両制御 | `interactable`, `pending`, `result` |
| `carRepeater` | 編成長に応じる反復表示 | `items`, 各itemの相対bind |

初期実装ではこの程度に絞り、実ページを作りながら追加する。単なる見た目違いは新typeではなくtheme/variantで扱う。

## 6. データバインディング

### 6.1 読み取りモデル

UIが `TimsCommunicationController` やSceneを探索しないよう、次の読み取り専用契約を画面起動時に注入する。

```csharp
public interface ITimsReadModel
{
    bool TryRead(TimsBindingAddress address, out TimsValue value, out double updatedAtSeconds);
    long Revision { get; }
}
```

アドレスは文字列1本に畳まず、少なくとも以下を型として保持する。

- source: `masterBus` / `localBus` / `viewState`
- car selection: index、leading、trailing、all、current item
- bus tag: device + item
- expected type

`TimsBusState` には最終更新時刻がないため、最初はAdapterがsnapshotを比較してrevisionと時刻を管理する。通信基盤を完成させる段階で、`TimsBusState` 自体にrevisionまたは変更イベントを追加すると効率がよい。配列は現在getterでcloneされるため、毎フレームの全配列pollingは避ける。

### 6.2 一方向を基本にする

- Bus → Viewは一方向バインド。
- Button → actionはイベント。
- 車両状態を変えるための「View → Bus」バインドは禁止する。
- UIだけの選択値、モーダル、pending状態は `viewState` に置く。
- 車両制御は必ずCommand Gatewayを通す。

これにより「表示値を書き換えたら車両が動いた」という意図しない双方向動作を防ぐ。

### 6.3 型、変換、表示

Bindingは `expectedType` を必須とし、型が違えば暗黙変換しない。単位変換と表示形式は許可済みconverterで行う。

初期converter例：`identity`、`not`、`mpsToKmh`、`nToKn`、`paToKpa`、`enumLabel`、`allTrue`、`anyTrue`。JSONに式言語やC#式を入れない。式が必要になったら、引数と戻り値が決まった名前付きconverterをC#に追加し、単体テストする。

更新処理は次の順序とする。

1. フレーム開始時にReadModelの一貫したsnapshot/revisionを取得する。
2. 表示更新は通常10～20 Hz、針やアニメーションなど滑らかさが必要な値だけ毎フレーム補間する。
3. 前回の変換後値と異なるtargetのみViewへ反映する。
4. TMP textは表示文字列が変わった場合だけ更新する。
5. ページ非表示中は購読を停止する。

### 6.4 欠損・古いデータ

Bindingごとに `fallback` と任意の `staleAfterMs` を持たせる。次の3状態を区別する。

- **missing**: tagが存在しない、または型不一致。`---` 等を表示し診断ログを一度出す。
- **stale**: 値はあるが更新期限超過。灰色化や通信異常表示にする。
- **valid**: 通常表示。

`false` や `0` をmissing扱いしない。警報用途ではmissing/staleを安全側の表示へ倒す規則をcomponentごとに明記する。

## 7. 画面遷移

画面遷移はUnity Scene遷移ではなく、1つのTIMS Canvas内でページrootを差し替える。

- `TimsScreenCatalog` が `pageId -> TextAsset` を管理する。
- 初期ページを車種設定から指定する。
- `navigate` はpageIdを解決し、検証済み定義を生成して現在ページと交換する。
- `push` / `replace` / `back` の3種類を用意し、戻る履歴の挙動を明示する。
- 現在pageIdと必要な選択状態だけをNavigatorが保持する。Viewインスタンスそのものを履歴に残さない。
- 頻繁に行き来するページは、計測後に最大2～3ページをLRU cacheしてよい。最初から全ページ常駐にはしない。
- 未知pageIdやロード失敗時は現在ページを維持し、診断オーバーレイへエラーを出す。
- 多重クリックは同一フレームで1遷移に集約し、遷移中の入力を無効化する。

ページ固有の状態は `screenId + componentId` をキーにする。異なるページでcomponent IDが同じでも状態が衝突しない。

## 8. ボタンによる車両遠隔制御

### 8.1 表示操作と制御操作を分離する

`navigate`、`back`、タブ変更はローカル表示操作であり、即時実行できる。一方、ドア扱い、故障リセット、機器開放などは車両状態を変える制御操作であり、別の `command` actionとして扱う。

```csharp
public interface ITimsCommandGateway
{
    TimsCommandTicket Submit(TimsCommandRequest request);
}
```

Command Requestには少なくとも `requestId`、`commandId`、対象scope/index、型付きparameter、発行時刻、発行元端末を持たせる。`requestId` は再送時も同じにして冪等性を確保する。

### 8.2 Command Gatewayの処理

1. commandIdが登録済みか確認する。
2. パラメーターschema、対象車、端末権限を検証する。
3. 車速、運転台、ドア、ノッチ、通信状態等のインターロックを**UIとは別に**検証する。
4. 同種pending commandとの重複を判定する。
5. 車両Controller/制御系へ要求を渡す。
6. `Accepted` / `Rejected` / `TimedOut` / `Succeeded` / `Failed` を結果として返す。
7. requestId、commandId、対象、結果、理由、時刻を監査ログへ記録する。

Buttonの `interactable` バインドは操作性のための事前表示にすぎず、安全保証には使わない。Gatewayは必ず再検証する。JSONが変更されてもインターロックは変更できない設計にする。

### 8.3 誤操作対策

commandごとにC#側のポリシーで次を指定する。

- 即時、長押し、確認ダイアログ、二段操作のいずれか
- hold時間、cooldown、timeout
- 許可される走行状態と端末
- 通信断時の挙動
- timeout後の再問い合わせ/再送方針

危険操作の確認方式やインターロックをJSONだけで弱められないよう、JSON値はポリシーが許す範囲の見た目設定として扱う。通信応答前に成功表示へしない。pending中は二重送信を防ぎ、拒否理由やtimeoutを明示する。

非常ブレーキ等の安全クリティカルな操作は、要件とフェイルセーフ経路が別途確定するまで汎用JSON commandへ載せない。

## 9. アセットとコードの配置案

既存の配置規則に合わせ、共通の型・実装と車種固有の定義を分ける。

```text
Assets/Nakatetsu/Train/Equipment/Tims/
├── Bus/{Scripts,Tests}/
├── Screens/
│   ├── Definitions/{Scripts,Data,Tests}/
│   ├── Loading/{Scripts,Data,Tests}/
│   ├── Navigation/{Scripts,Data,Tests}/
│   ├── Binding/{Scripts,Data,Tests}/
│   ├── Actions/{Scripts,Data,Tests}/
│   └── Commands/{Scripts,Data,Tests}/
└── Display/
    └── Components/{Scripts,Prefabs,Tests}/

Assets/Nakatetsu/Train/Equipment/Tims/Series1000/
├── Screens/Data/                    # 画面JSON
├── Catalogs/Data/                   # Screen/asset registry asset
├── Themes/Data/                     # 色、font、variant
└── Graphics/Sprites/                # 車種固有画像
```

既存のTIMS表示は `Train/Equipment/Tims/Display` に集約し、`Indicators`、`Notch`、`SpeedMeter` の各機能を先に置いてから `Scripts`、`Prefabs`、`Sprites` に分ける。表示コードは既存の `Nakatetsu.Train.Presentation` Assemblyを維持し、TIMSの制御Assemblyと分離する。

Newtonsoft.Jsonを使う場合は `Packages/manifest.json` に `"com.unity.nuget.newtonsoft-json": "3.2.1"` を直接追加する。`JsonUtility` だけで実装する場合、辞書や多態的actionの扱いが煩雑になるため、この仕様ではNewtonsoft.Json + 明示的DTO + 厳格validatorを推奨する。

## 10. 検証とエラー処理

検証をEditor/CIとRuntimeの二段にする。

### 10.1 静的検証

- JSON構文とJSON Schema
- schemaVersion、designSize
- screenId/component IDの一意性
- component type、props、binding targetの組み合わせ
- pageId、asset ID、converter、commandIdの参照解決
- expectedTypeとconverter入出力型
- Rectの有限値、正のサイズ、論理領域との交差
- zIndexやcomponent数の上限
- command parameter schema

Content配下の全画面を走査するEditModeテストをCIで実行し、1ページでも不正ならビルドを失敗させる。可能ならImporter/EditorWindowで保存直後にも同じvalidatorを動かす。

### 10.2 Runtime

- 開発ビルド: 詳細なpath、component ID、原因をログ/診断overlayに表示する。
- 製品ビルド: 画面全体を落とさず、不正componentをplaceholderへ置換する。ただし画面ルートや安全関連actionの不正はページロードを中止する。
- 同じmissing tagのログを毎フレーム出さず、ページごとにrate limitする。
- 外部ファイル更新を採用する場合、サイズ上限、署名/ハッシュ、原子的な差し替え、最後に成功した定義へのrollbackを用意する。

## 11. テスト方針と受け入れ基準

### 11.1 EditMode

- 正常JSONをDTOへ変換できる。
- 未知type/action、重複ID、型不一致、範囲外参照を拒否できる。
- 全converterの境界値、NaN/Infinity、配列、missing/staleを確認する。
- Revisionが同じときView更新が発生しない。
- ページ履歴のpush/replace/backを確認する。
- Command Dispatcherが未知コマンドを拒否し、Gateway以外を呼ばない。
- 同requestIdの再送が重複実行されない。

### 11.2 PlayMode

- 1536 x 1024でJSONのrectと生成後RectTransformが一致する。
- 1920 x 1080等で3:2領域がletterboxされ、クリック位置がずれない。
- MasterBus更新が表示に反映され、欠損/古い値が所定の見た目になる。
- ボタンでページが遷移し、旧ページの購読とイベントが解除される。
- 長押し、pending、成功、拒否、timeoutの各表示を確認する。
- 画面を繰り返し切り替えてGameObject、購読、GC allocationが増え続けない。

### 11.3 Visual Regression

1536 x 1024のRenderTextureへ各ページを描画し、基準画像との差分をCI成果物にする。フォントやGPU差を考慮して小さな許容値を設ける。少なくとも次を受け入れ基準とする。

- 主要部品の境界が設計座標から1論理pixel以上ずれない。
- text overflow、欠け、意図しないstretchがない。
- 通常、警報、missing、pending、disabledの代表状態に基準画像がある。

性能目標は実機相当環境で決めるが、初期目安としてページ表示後のsteady stateで、バインド更新によるmanaged GC allocationを0 B/frame、画面生成を1フレームに収められない場合はロード中表示または事前生成を採用する。

## 12. 実装順序

### Phase 1: 安全な最小縦切り

1. JSON DTO、Schema、Validator、Screen Catalogを作る。
2. `panel`、`label`、`boolIndicator`、`button` のRegistry/Factoryを作る。
3. 1536 x 1024 Hostとletterbox、page navigationを作る。
4. 既存BoolIndicatorをView契約へ適合させる。
5. 固定値だけのhome/monitor 2ページをJSON化し、遷移テストを通す。

### Phase 2: 表示バインド

1. `ITimsReadModel` とMasterBus/LocalBus adapterを作る。
2. expectedType、converter、fallback、stale、差分更新を実装する。
3. numericText、image、bar/circular gauge、carRepeaterを必要順に追加する。
4. 既存試作画面を1ページずつJSONへ移し、Visual Regressionを追加する。

### Phase 3: 遠隔制御

1. 車両側と合意したCommand契約、結果、監査ログを先に実装する。
2. mock gatewayでcommandButtonの状態遷移を完成させる。
3. 低リスク操作1種類を実車両ロジックへ接続し、拒否/timeout/再送を検証する。
4. 各操作の安全要件をレビューしてからcommand registryへ追加する。

### Phase 4: 制作運用

1. JSON Schema補完、Editor preview、全画面lintを整備する。
2. 車種themeと共通componentの境界を整理する。
3. 計測結果に基づいてpool/cache/update frequencyを調整する。
4. 外部差し替えが本当に必要な場合だけ署名・rollback付き配布を追加する。

## 13. 先に確定すべき仕様

実装開始前に、プロダクト側で次を決める。

1. 1536 x 1024以外の出力はletterboxでよいか、物理モニターが常に3:2か。
2. JSONはビルド同梱のみか、運用中の外部差し替えも必要か。
3. 車種ごとのページ一覧と共通ページの範囲。
4. MasterBus/localBusの正式なtag名、型、単位、更新周期、stale閾値。
5. 各遠隔操作の対象、権限、インターロック、確認方法、timeout、成功判定。
6. 画面更新周期、入力遅延、画面切替時間の目標値。

特に4と5を決めずにJSONだけ先行すると、定義内へ一時的なタグ名や安全条件が埋まり、後で互換性と安全性の両方を損なう。まず表示用タグ契約とCommand契約を小さく固定し、最小の2ページで縦に検証してから部品数を増やすのが最適である。
