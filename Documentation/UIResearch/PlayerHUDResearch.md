# Nakatetsu プレイヤー向けHUD・フォント調査

調査日：2026-09-22

## 結論

NakatetsuのゲームHUDは、実車の計器盤を画面上へ再現するより、**都市鉄道のサイン計画と近未来ゲームUIを合わせた「Neo Transit Control」**を推奨する。

- 画面四隅に情報を分散し、前方の線路と信号を隠さない。
- 円形メーターを主役にせず、速度・ノッチ・距離を大きな数字と短いバーで見せる。
- 通常時は静かで細身、制限速度超過・停止位置接近・ドア扱いなど、必要な瞬間だけ面積と輝度を上げる。
- 英数字は **Oxanium SemiBold / Bold**、日本語は **IBM Plex Sans JP Medium / SemiBold** を第一候補にする。
- 色はシアンを通常操作、アンバーを注意、赤を即時対応が必要な異常に限定する。
- HUDは `Full / Minimal / Off` の3段階を用意し、要素単位の表示切替とサイズ変更も可能にする。

「鉄道らしさ」は計器の形ではなく、路線色、駅ナンバリング、進行方向、運行時刻、停止位置の見せ方で作る。この方がユーザー向けゲームUIとして独自性を出しやすい。

## このプロジェクトで確認できた前提

- Unity `6000.4.0f1`、uGUI `2.0.0`、Input System `1.19.0` を使用している。
- 現在の表示資産は主に運転台内のTIMS向けで、基準解像度は1536×1024。今回のプレイヤーHUDとは役割を分ける。
- `Assets/Nakatetsu/Content/Fonts/Roboto/` にRoboto、Roboto Condensed、Roboto SemiCondensedの多数のウェイトとTMP Font Assetが既にある。
- 現行UIには白文字、シアン系の力行、アンバー系の制動、赤の非常表示という色分けがある。プレイヤーHUDでも意味を合わせると学習コストを下げられる。
- 走行データとして速度、P/Bノッチ、進行方向、ドア、非常状態、将来のATC・ATO/TASC表示へつなげられる土台がある。

既存Robotoは試作には十分だが、製品の顔としては汎用的に見えやすい。新規HUDでは見出しと主要数値だけ別書体に替えると、追加コストを抑えながら印象を変えられる。

## 推奨ビジュアル：Neo Transit Control

### キーワード

`urban` / `precise` / `calm` / `fast` / `night transit` / `operations center`

SF宇宙船のように全面を発光させず、駅サインの整理された情報設計に、ゲームらしい角欠け・走査ライン・短いトランジションを足す。パネルは黒い板ではなく、背景の景色がわずかに透ける濃紺面とする。

### 形

- 角丸は小さくする。基本 `4–8 px`、駅コードのピルだけ大きく丸める。
- パネル左端または上端に `2–4 px` の路線色ラインを置く。
- 主要カードは左上または右下の一角だけを斜めに欠く。すべての角を装飾しない。
- 区切り線は白ではなく青灰色を低い不透明度で使う。
- 影よりも、暗い背景面＋細い明色境界で情報階層を作る。
- 速度や距離の更新で桁が横に揺れないよう、同じ幅の領域に右揃えする。

### 推奨パレット

| 用途 | 色 | 想定 |
| --- | --- | --- |
| HUD背景 | `#081018` | 90–94%不透明。景色が明るい場面でも文字を守る |
| 浮上カード | `#111C26` | 背景より一段明るい面 |
| 主文字 | `#EAF7FF` | 数値、駅名、主要ラベル |
| 副文字 | `#A6B4C0` | 単位、補足、未選択項目 |
| 通常アクセント | `#66E3FF` | 選択、進行、力行、フォーカス |
| 正常完了 | `#38E8A2` | 定位置、ドア閉、条件成立 |
| 注意 | `#FFC857` | 制限接近、遅延、制動誘導 |
| 警告 | `#FF6B6B` | 超過、非常、操作必須 |

`#111C26` 上で、上記の主文字・副文字・アクセントはすべて4.5:1以上になる。色だけで状態を伝えず、`!`、形、ラベル、点滅回数などを併用する。赤と緑を同じ形のランプだけで区別する構成は避ける。

## フォント候補

### 第一候補：Oxanium + IBM Plex Sans JP

| 役割 | 書体 | ウェイト | 用例 |
| --- | --- | --- | --- |
| 主数値 | Oxanium | 600 / 700 | `92 km/h`、`B4`、`+00:18`、距離 |
| 英字見出し | Oxanium | 600 | `NEXT`, `LIMIT`, `ATO`, `DOOR` |
| 日本語 | IBM Plex Sans JP | 500 / 600 | 駅名、操作案内、通知 |
| 強い日本語 | IBM Plex Sans JP | 700 | 警告、ミッション結果 |

[Oxanium](https://fonts.google.com/specimen/Oxanium) は角の処理にゲームHUDらしさがあり、Google Fonts側でも宇宙船HUDやゲームのスコアボードを想起する表示書体として説明されている。本文すべてに使うと癖が強いため、英数字と短い見出しに限定する。

[IBM Plex Sans JP](https://github.com/IBM/plex) は機械的だが冷たすぎず、日本語と英数字の情報量が多い画面でも読ませやすい。IBM公式は日本語を含む多言語対応とOFL提供を案内している。

両方ともSIL Open Font License 1.1で入手できる。実際に同梱する版のライセンスファイルもリポジトリへ保存する。

### 第二候補：Barlow Condensed + IBM Plex Sans JP

[Barlow](https://github.com/jpt/barlow) は道路標識、バス、鉄道を含む公共交通の視覚文化から着想を得た書体で、OFL 1.1。Oxaniumより現実寄りで、スピード感を保ちつつ長く使いやすい。

向いている方向：近未来感を弱め、都市交通ブランドとして上品にまとめたい場合。

- 主数値・英字：Barlow Condensed SemiBold / Bold
- 日本語：IBM Plex Sans JP Medium / SemiBold
- 長所：狭いカードへ情報を多く置ける。速度や時刻がシャープに見える。
- 短所：単体ではゲームらしい固有性が弱い。形とアニメーション側で個性を足す必要がある。

### 第三候補：IBM Plex Sans JP 単独

日本語と英数字のトーンを完全に統一する安全案。数字にはSemiBold、本文にはMediumを使う。実装とローカライズが簡単で、車両運行システムらしい精密さが出る。一方、タイトル固有の印象は弱くなる。

### 既存資産による即時試作：Roboto Condensed + Roboto

- 数字：`Roboto_Condensed-SemiBold`
- 日本語：日本語グリフを持つ新規フォントが必要。現状のRobotoだけで日本語HUD全体を完結できる前提にはしない。
- 用途：レイアウト検証、情報量、アニメーション、視認距離の確認。
- 判断：プロトタイプには採用、本番のブランド書体には第一候補との比較後に決める。

### 補助候補：M PLUS 1 Code

[M PLUS FONTS](https://github.com/coz-m/MPLUS_FONTS) はOFL 1.1。`M PLUS 1 Code` は等幅情報に向くが、全面に使うとデバッグ画面や端末に寄りすぎる。時刻、駅コード、ログ、詳細情報パネルだけの補助用途に向く。

### 避けたい使い方

- Thin / ExtraLightを走行中HUDに使う。
- 日本語本文をOxanium風に無理に加工する。
- 速度、距離、時刻で異なる数字書体を混在させる。
- 斜体を通常ラベルに使う。動きや警告の意味と混ざる。
- デジタル7セグをHUD全体に使う。実車計器の再現へ寄り、今回の狙いから外れる。

## HUDの情報設計

常時表示は「いま運転判断に必要か」で決める。車両システムの状態をすべて並べない。

### 常時表示

1. **速度ブロック**：現在速度を最大、制限速度を隣に小さく表示。
2. **操作ブロック**：`P4 / N / B6 / EB` を一つの直線上で表示。選択中だけ面を持たせる。
3. **次駅カード**：駅コード、駅名、残距離、到着予定または定時差。
4. **運行ストリップ**：次駅までの進捗と重要地点。信号、速度制限、停止位置を同じ時間軸または距離軸に置く。

### 状況に応じて出す

- 駅接近時：停止位置までの距離と停止精度バー。
- 制限速度接近時：変更後の速度と残距離。
- ドア扱い時：開扉側、閉扉確認、出発条件。
- 異常時：原因と、次に必要な操作を一文で表示。
- チュートリアル時：ボタン表示と行動。通常プレイでは消す。
- 結果時：停止誤差、定時差、乗り心地などを一時カードで返す。

### 詳細画面へ送る

編成別BC圧、回生力、機器状態、通信状態などはプレイヤーHUDへ常時並べず、TIMSまたはデバッグ画面へ置く。プレイヤーに必要な場合だけ、要約した異常通知としてHUDへ上げる。

## 16:9レイアウト案

```text
┌─ NT LINE ─ NEXT  NT04 中央駅 ───── 1.24 km ─── 10:42:18  +00:12 ┐
│                                                                      │
│                                                                      │
│                     前方視界・信号・線路を空ける                     │
│                                                                      │
│                                                                      │
│  速度制限変更                                                        │
│  80 → 65 / 320 m                                                     │
│                                                                      │
│  092        LIMIT 100                     DOOR ◀ CLOSED    P4 ▶      │
│  km/h     ━━━━━━━━━━━━          ──●───────────────○── 1.24 km       │
└──────────────────────────────────────────────────────────────────────┘
```

- 上端：路線・次駅・時刻。高さを抑えた一本の情報帯。
- 左下：現在速度。視線移動を少なくする固定位置。
- 右下：ノッチとドア。操作結果を即座に確認できる。
- 下中央：次駅までの進捗。停止案内時だけ厚くなる。
- 左中段：速度制限変更など、短時間の先読み通知。
- 画面中央はミッション開始・緊急警告・停止評価以外では使わない。

### 速度ブロック

アナログ円形計器ではなく、`092` を主役にする。`km/h` は小さく、制限速度は独立した標識形または `LIMIT 100` で示す。速度超過時は数字を赤に変えるだけでなく、制限値との差 `+4` と上向き警告形を出す。

### ノッチ表示

```text
B7  B6  B5  B4  B3  B2  B1   N   P1  P2  P3 [P4] P5
```

全セルを常に箱で囲まず、細いレールと小さな目盛りにする。現在位置だけ明るい面と方向マークを持たせる。力行はシアン、制動はアンバー、非常は赤＋`EB`表記にする。

### 次駅・停止ガイド

駅コードの丸いバッジ、駅名、距離を一群にする。駅まで400–600m程度の設計値に入ったら、通常の進捗ストリップを停止ガイドへモーフさせる。正確な切替距離は制動性能とプレイテストで決め、固定の業界値とは扱わない。

停止位置付近は `+1.2 m` のように符号付きで表示し、バーの左右にも `SHORT / OVER` または日本語を置く。色を見分けられなくても意味が分かるようにする。

## サイズとタイポグラフィ

1920×1080を基準とする初期値：

| 用途 | 目安 | 書体 |
| --- | ---: | --- |
| 現在速度 | 104–120 px | Oxanium 600 |
| ノッチ | 48–64 px | Oxanium 700 |
| 次駅名 | 36–44 px | IBM Plex Sans JP 600 |
| 主要通知 | 32–40 px | IBM Plex Sans JP 600 |
| 通常ラベル | 28–32 px | IBM Plex Sans JP 500 |
| 最小補助文字 | 26 px | IBM Plex Sans JP 500 |

Microsoftのゲーム向けアクセシビリティ基準は、PC/コンソールの1080pで既定文字高26px、HUDを含む文字と背景のコントラスト4.5:1を要件例としている。26pxはすべてのフォントで同じ見かけの大きさになるわけではないため、実機の表示距離で確認する。

- 数値の単位はベースラインを揃え、数値の40–50%程度にする。
- 全大文字は3–6文字の英字ラベルに限定する。
- 駅名を一行に収めるための自動縮小には下限を設ける。下限を割る場合はカード幅または改行で処理する。
- 4Kでは単純にpx値を固定せず、Canvas ScalerとユーザーのHUD倍率を使う。

## 動きとフィードバック

- 数値更新は即時。パネル自体は毎フレーム動かさない。
- カード出現は `140–180 ms`、消失は `100–140 ms` を開始値とする。
- 駅接近の進捗は滑らかに補間するが、表示値そのものを遅らせない。
- 注意は1回の短い発光、警告は最大2–3回の明滅後に点灯へ移る。無限点滅を標準にしない。
- 停止成功は線が中央へ吸着する動きと短い音で返す。紙吹雪のような演出は運転画面に重ねない。
- HUDアニメーションを減らす設定を用意する。

## HUDプリセット

| プリセット | 表示 |
| --- | --- |
| Full | 速度、制限、ノッチ、次駅、距離、定時差、停止ガイド、操作ヒント |
| Minimal | 速度、制限、ノッチ、次の重要イベントだけ |
| Off | 中央警告などゲーム進行に不可欠な通知のみ。必要なら完全非表示も別設定 |

さらに `HUD scale 80–140%`、背景不透明度、色覚補助、字幕・通知時間、要素別ON/OFFを設定できるようにする。Train Sim World 4がMini HUDの速度・目的地距離・マーカーを個別に切り替えられるよう改善した点は参考になる。

## アイコン

一般操作には [Lucide](https://github.com/lucide-icons/lucide) を第一候補とする。24×24の一貫した線画で、ISCライセンス。ドア、時刻、設定、警告、音などの汎用記号を早く揃えられる。

- 線幅はHUD上で2px相当を基準にする。
- 重要状態はアイコンだけで伝えず、`閉`、`EB`、`100`などの文字を併記する。
- 信号、速度制限標識、停止位置、ノッチなど鉄道固有記号は独自に作る。
- 既製アイコンを変形しすぎず、同じviewBox・線端・角のルールに揃える。
- ライセンス本文と取得元・バージョンを同梱する。

[Material Symbols](https://developers.google.com/fonts/docs/material_symbols) もApache 2.0で候補になるが、見慣れたモバイルアプリ感が強い。Nakatetsuでは設定画面などに限定するか、Lucideとの比較後に一系統へ統一する。

## 参考ゲームから持ち帰る点

### Train Sim World 2 / 4

[Train Sim World 2の公式HUD紹介](https://live.trainsimworld.com/news/tsw2-take-control) は、走行中に重要情報を一目で読めること、操作方向、出力、勾配、安全装置、ドア状態を明確にすること、HUDサイズを変えられることを狙いとしている。

[Train Sim World 4のMini HUD改善](https://live.trainsimworld.com/news/train-sim-world-4-roadmap-november-2023) では、速度・目的地までの距離・マーカー等を切り替え可能にし、線路モニターの先読み範囲を速度に応じて変える方針が示されている。

持ち帰る点：初心者向け情報量と没入向け最小表示を同じ固定HUDへ詰めず、設定で段階化する。速度に応じて先読み距離を変える。

### 電車でGO！！ はしろう山手線

[公式サイト](https://www.jp.square-enix.com/denshadego/) の運転画面は、シミュレーション情報に加えてミッション、評価、停止などを強くゲーム化している。

持ち帰る点：駅接近や停止をイベントとして盛り上げ、結果をすぐ返す。避ける点：常時すべてを強く発光させること。Nakatetsuでは通常時を静かにし、イベント時との落差を作る。

### JR東日本トレインシミュレータ

[公式マニュアルの運転画面](https://shared.steamstatic.com/store_item_assets/steam/apps/2111630/manuals/ec7c864e3742342718716110cccc7be92801d722/JR_EAST_TrainSimulator_Instruction_Manual_JP.pdf) は、運転情報、列車位置、停止位置を抑制した表示で扱う。

持ち帰る点：実写・前方風景を壊さない情報密度と、停止位置を距離の流れとして見せる考え方。Nakatetsuではこれを土台に、書体、路線色、アニメーションでゲーム固有の魅力を足す。

## Unity実装時の方針

### 構成

既存TIMSと分けて、プレイヤー画面用Canvasを `Assets/Nakatetsu/Application` 側のUIとして持たせるのが自然。列車機器から直接値を探さず、HUD用の読み取りモデルへ速度、制限、ノッチ、次駅、距離、時刻、ドア、警告をまとめる。

```text
PlayerHudCanvas
├── SafeArea
│   ├── TopRouteBar
│   ├── SpeedBlock
│   ├── ContextAlert
│   ├── JourneyStrip
│   └── OperationBlock
└── CenterOverlay
    ├── CriticalAlert
    └── StopResult
```

- 基準解像度は1920×1080、Canvas Scalerは画面比率の検証後にmatch値を決める。
- `Screen.safeArea` 内へ主要情報を置く。Unity公式はTVのオーバースキャンもsafe areaの理由として挙げている。
- 16:9、16:10、21:9、1280×720、1920×1080、2560×1440、3840×2160で確認する。
- 背景パネルを持たない文字には、どの天候でも読める暗色の太めアウトラインまたは影を付ける。

### TMP Font Asset

UnityのFont Assetは、Staticが最も実行時負荷が低く、Dynamicは実行時追加ができる代わりに負荷と元フォント同梱が増える。

初期案：

1. Oxanium 600 / 700はHUDで使うASCII、記号、単位をStatic Atlas化する。
2. IBM Plex Sans JP 500 / 600 / 700は、UI文言・駅名・将来のローカライズ範囲から文字集合を作る。
3. 日本語をDynamicにする場合は複数Atlas化によるメモリ増加と初回生成時の負荷を計測する。
4. フォールバック順を明示し、欠字を四角で表示したままリリースしない。
5. 数字 `0–9`、符号、コロン、スラッシュ、`km/h`、駅コードを実サイズで比較する。

参考：[Unity AtlasPopulationMode](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/TextCore.Text.AtlasPopulationMode.html)

## 最初に作るデザインスライス

一度に全HUDを作らず、次の1画面で方向性を判断する。

**シーン**：晴天昼、90km/hで走行、次駅1.2km、制限100→65km/h、P4から制動へ移る場面。

含めるもの：

- 速度＋制限
- ノッチレール
- 次駅カード＋定時差
- 距離ベースの運行ストリップ
- 速度制限変更の通知
- 昼・夜の2背景
- Full / Minimalの切替

比較するフォント：

1. Oxanium + IBM Plex Sans JP
2. Barlow Condensed + IBM Plex Sans JP
3. 既存Roboto Condensed + IBM Plex Sans JP

評価軸：2–3m離れた1080p表示での可読性、背景が明暗変化したときの安定性、速度と制限の見分け、ゲーム固有の印象、駅名の収まり、数字更新時の揺れ。

## 採用判断

現時点の推奨は以下。

| 項目 | 採用案 |
| --- | --- |
| ビジュアル | Neo Transit Control |
| 数字・短い英字 | Oxanium SemiBold / Bold |
| 日本語 | IBM Plex Sans JP Medium / SemiBold |
| 基本色 | 濃紺＋白＋シアン |
| 注意・警告 | アンバー／赤。色に加えて形と文字を併用 |
| 主要部品 | 大型速度、直線ノッチ、次駅カード、運行ストリップ |
| 表示モード | Full / Minimal / Off |
| アイコン | Lucide＋鉄道固有の自作記号 |
| 試作 | 既存Robotoを利用し、レイアウト確定後に書体比較 |

## 参考資料

- [Train Sim World 2 – Take Control（公式）](https://live.trainsimworld.com/news/tsw2-take-control)
- [Train Sim World 4 Roadmap: November 2023（公式）](https://live.trainsimworld.com/news/train-sim-world-4-roadmap-november-2023)
- [電車でGO！！ はしろう山手線（公式）](https://www.jp.square-enix.com/denshadego/)
- [JR東日本トレインシミュレータ 操作マニュアル](https://shared.steamstatic.com/store_item_assets/steam/apps/2111630/manuals/ec7c864e3742342718716110cccc7be92801d722/JR_EAST_TrainSimulator_Instruction_Manual_JP.pdf)
- [IBM Plex公式タイポグラフィ](https://www.ibm.com/design/language/typography/typeface/)
- [Oxanium / Google Fonts](https://fonts.google.com/specimen/Oxanium)
- [Barlow公式リポジトリ](https://github.com/jpt/barlow)
- [M PLUS FONTS公式リポジトリ](https://github.com/coz-m/MPLUS_FONTS)
- [Lucide公式リポジトリ](https://github.com/lucide-icons/lucide)
- [Microsoft Game Accessibility: Text display](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101)
- [W3C WCAG: Contrast Minimum](https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum)
- [W3C WCAG: Use of Color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color)
- [Unity Screen.safeArea](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Screen-safeArea.html)
- [Unity AtlasPopulationMode](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/TextCore.Text.AtlasPopulationMode.html)
