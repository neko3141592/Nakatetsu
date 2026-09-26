# TIMS TrainStatus

`TimsMonitorOutput/TimsMonitorCanvas2/TrainStatus` に `TimsTrainStatusDisplay` を設定済み。
実行時に所有列車の `TrainRoot.ConsistDefinition` から車両画像と全角の号車番号を生成する。
`Sample` は編集用に保持し、実行時は非表示。`BackGround` はそのまま使用する。
ソースコードは `Assets/Nakatetsu/Train/Equipment/Tims/Display/TrainStatus/Scripts`。

## TIMS バインディング

`TimsMonitorOutput.Initialize` が表示先列車のTIMSを渡す。
単独使用時は `Tims` を手動指定でき、未指定なら親または同じTrainRoot内で取得する。
別列車のTIMSをグローバル検索しない。編成定義を表示コンポーネントに重複保持しない。

Inspectorの **State Binding** で以下を変更できる。

| 設定 | 初期値・意味 |
| --- | --- |
| Traction Binding / Bus Target | Local。表示中の各号車のLocalBusを読む。Masterなら全車共通のMasterBusを読む |
| Traction Binding / Device Name | `Traction` |
| Traction Binding / Item Name | `ActualTractionForceN`（符号付きfloat、単位N） |
| Check Availability | 有効。可用性タグがtrueのときだけ読む。独自タグに可用性がなければ無効化できる |
| Availability Binding | Local / `Traction` / `IsAvailable`（bool）。参照先を変更可能 |

旧TD-ATCの `TrainFormationDisplayBuilder` / `TrainFormationDisplayWorkspace` と同じく、
電動車の実測牽引力が **+1N超なら力行、-1N未満なら回生、それ以外は惰性**。
ノッチ指令や速度から推定せず、TIMSで受信した各車の測定値を使用する。
浮動小数点以外のタグ、欠測、非有限値、無効なソースは直前の力行・回生表示を残さず、惰性用画像と通常番号色へ戻す。
このフォールバックは実際の惰性状態を保証するものではない。

## 見た目

- `Spacing X`: 画像・番号に共通の中心間隔。初期値80。
- `Center Position`: 編成全体の中心。画面中央にしたい位置を指定する。1号車のXは中心X − 間隔×(両数−1)/2で計算する。旧`First Car Position`の値は保持されるため、既存設定は中心として使いたい位置へ調整する。
- `Sprite Size`: 透明余白を含む画像Rect。付属素材は1024×1024で見本と同じ大きさ。
- `Number Font` / `Number Font Size`: 番号フォント・サイズ。初期設定はNoto Sans JP Regular、24。
- `Number Offset` / `Number Size`: 車両画像に対する番号位置・Rectサイズ。
- `Coasting Number Color`: 惰性・欠測時。初期値は白。
- `Active Number Color`: 力行・回生時。初期値は黒。
- `Show Traction Regen State`: 無効なら全車を惰性画像・通常番号色で表示。列車制御やTIMSの値は変更しない。

両端に運転台画像を使う旧版の並びを継承。見本に合わせて末尾画像のみ左右反転し、番号は反転しない。
`motorCount > 0` または車種M/Mc1/Mc2を電動車として、T/Tc/M/Mcのスプライトを選択する。
編成長に合わせて生成し、10号車以降も `１０` のように全角表示する。
画像の配置と号車番号は実行時に更新され、設定変更も反映する。

## 進行方向の矢印

`Direction Arrow` の `Activated Cab Binding` と `Reverser Binding` で方向の参照先を指定する。
初期値はMasterBusの `Direction/ActivatedCabPosition` と `Direction/ReverserPosition`（両方int）。
先頭運転台=1、後部運転台=-1、前進=1、後進=-1を組み合わせて編成に対する方向を決める。
前部運転台で前進／後部運転台で後進なら1号車の前に左向き矢印、逆の組み合わせなら最終号車の後に右向き矢印を表示する。
中立、運転台未選択、不正値、欠測、無効なTIMSでは非表示。停車中もレバーサが選択されていれば表示する。
レバーサをLocalBusへバインドする場合は、有効運転台の号車のタグを読む。運転台選択のLocal指定は先頭車のタグを読む。

矢印は車両数・号車番号・生成開始位置に含めない。
1号車の中心Xを `Center Position.x - Spacing X * (CarCount - 1) / 2` として、矢印をその1間隔前または最終号車の1間隔後へ置く。
中心X=0・間隔80・10両なら、1号車は-360、10号車は360、矢印は-440または440となる。
1両ならその車両が指定位置に置かれ、奇数・偶数いずれの両数でも車両列の中心は変わらない。
`Show Traction Regen State` をオフにしても矢印は方向に追従する。

`Direction Arrow Sprite` は差し替え可能。素材が右向きなら `Direction Arrow Sprite Points Right` をオン、左向きならオフにする。
付属素材は旧TD-ATCの `Assets/Art/Textures/Ui/Ntims/Arrow.png` をコピーしたもの（移植元は変更しない）。
`Direction Arrow Size` は透明余白を含むRectサイズで、320×320の設定時の図柄は約42×16。

## パンタグラフ搭載表示

`CarDefinitionAsset` の `Has Pantograph` をオンにした車両へ `Pantograph Main Active Sprite` を表示する。
Series10000のMp、Series1000のMを搭載車として設定済み。
シミュレーション未実装のため、力行・回生・惰性やTIMSの有効性にかかわらず、搭載車は `Main Active` 固定。
`Main Inactive`・`Sub Active`・`Sub Inactive` は今回の表示では使用しない。

パンタ画像はその号車の車体と同じ位置・Rectサイズで重ねる。素材内の上下左右の余白を使い、追加のオフセットや左右反転は行わない。
未搭載車・車両定義欠落・スプライト未指定なら非表示。車両数や中央揃えの計算には含めない。
