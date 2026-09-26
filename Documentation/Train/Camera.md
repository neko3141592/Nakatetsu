# 注目編成のカメラ

`Application/Player/Scripts/TrainCameraController.cs`が、注目編成の有効運転台に追従する。

## Unityでの接続

1. `Application/Focus`に`TrainFocusController`を付ける。
2. `Focused Train`に`1001F/Integration`の`TrainIntegrationController`を設定する。`Keyboard Input`には`Application/Input`の`TrainKeyboardInputController`を設定する。`Trains`が空の場合は、起動時に配置済みの編成を取得する。
3. `Application/Main Camera`に`TrainCameraController`を付け、`Focus Controller`に`Application/Focus`の`TrainFocusController`を設定する。
4. 運転台の目線位置に置いたGameObjectに`TrainCameraAnchor`を付ける。車両Presentationの子に配置し、青い軸（ローカル+Z）を視線方向、緑の軸（ローカル+Y）を上方向に合わせる。

既に付いているコンポーネントはそのまま使う。Anchorは前後の運転台の車両にそれぞれ1つずつ配置する。現在のTc1には配置済み。後側に追従するにはTc2にも配置が必要。

## Inspectorから注目編成を切り替える

1. Playモードに入り、`TrainFocusController`のInspectorを開く。
2. 「注目編成のデバッグ」の`Train Id`に、対象編成の`TrainRoot`に設定したID（例：`1001F`）を入力する。
3. 「この編成に注目」を押す。結果はボタンの下に表示される。

`TryFocusTrainByTrainId`を呼び、注目編成とキー入力先を一緒に切り替える。カメラは新しい注目編成の有効運転台に追従する。対象は`Trains`に登録された編成とし、見つからない場合は現在の注目先を維持する。

Play中の`Focused Train`欄は現在値の表示専用。Play開始前は、初期注目先として編集できる。デバッグでの切り替えはPlay終了時に元に戻る。

## 追従の流れ

- `TrainFocusController.FocusedTrain`から注目編成を取得する。
- `TrainIntegrationController.Status.TryGetActiveCab`からTIMSが判定した有効運転台を取得する。
- 前側なら車両インデックス0、後側なら最後の車両を対象にする。
- `TrainPresentationAssignment`を使い、対象車両の子にあるAnchorを取得する。車両PresentationはPlay開始時に生成されるため、生成後に取得できる。
- 車体の姿勢更新後の`LateUpdate`で、Anchorのワールド位置・回転をカメラに反映する。`DefaultExecutionOrder(100)`で車体の更新より後に実行する。
- 注目編成や有効運転台の変更にも追従する。有効運転台の変更が見える時点はTIMSの更新に従う。
- 有効運転台やAnchorを取得できない場合、または同じ対象車両にAnchorが複数ある場合は、カメラを動かさず最後の位置・回転を維持する。

Anchorは空クラスのまま。客室・外部視点の選択、マウスによる回転、補間はまだ実装しない。
