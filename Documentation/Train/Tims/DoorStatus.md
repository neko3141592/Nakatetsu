# TIMS DoorStatus

`TimsDoorStatusDisplay` はTrainStatusと独立したドア状態表示。
`TimsMonitorOutput/TimsMonitorCanvas2/DoorStatus` に設定し、初期位置は編成表示の50px下。
スクリプト・スプライト・テストは `Assets/Nakatetsu/Train/Equipment/Tims/Display/DoorStatus` に置く。

## 状態の参照

`TimsMonitorOutput.Initialize` が対象列車のTIMSを渡す。単独使用時は手動指定、または同じTrainRoot内で取得する。
各車のLocalBusに公開された `Door/AllClosed`（bool）を読む。
trueならClose、falseならOpen。左右すべてのドアが閉じたかどうかを示す接点で、マスコンやドア指令から推測しない。
未閉には開き途中・閉じ途中も含まれる。文字はどちらの状態でも全車「開」のまま。
欠測・型違い・無効なTIMSでは画像を隠し、灰色の「開」を表示する。

Inspectorの `Bus Target`・`Device Name`・`Item Name` でバインディング変更可能。
Localは号車ごと、Masterは全車共通のbool値を参照する。
`Value Means Closed` をオフにすると、trueを開とするタグを使用できる。

## 生成・見た目

- 編成定義はTIMSを所有するTrainRootから取得する。
- `Center Position` を中心に、`Spacing X × (両数−1) ÷ 2` 左から生成する。
- 画像と「開」の文字に同じ号車間隔を使う。車両数が変わると再生成する。
- `Sprite Size` は透明余白を含むサイズ。初期値1024×1024。
- `Open Sprite` / `Closed Sprite` にユーザー提供の画像を設定済み。
- `Label Font` / `Label Font Size` / `Label Offset` / `Label Size` で文字を調整できる。
- `Open Label Color` / `Closed Label Color` / `Unavailable Label Color` で状態ごとの文字色を指定できる。
- 背景や見本を削除せず、生成した `GeneratedDoors` だけを管理する。

TIMS各車の値の切り替え、欠測、参照先変更、両数変更時の中央揃え、別編成への切り替えをテストし、10両の描画と「開」のグリフを確認した。
