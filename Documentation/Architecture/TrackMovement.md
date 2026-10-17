# W1-03：線路上の位置・方向・移動結果の契約

- 合意日：2026-09-22
- 対象：[実装計画 W1-03](../implementation-plan-2026-11-17.md)
- 状態：基本契約を確定。実装結果は[W1-03完了記録](../Validation/W1-03-completion-2026-09-24.md)を参照。移動結果DTOは今回省略。
- 関連：[実装ルール](ImplementationRules.md)、[線路基盤の移植計画](../Migration/TrackFoundationMigration.md)

## 1. 基準点と位置

列車位置の基準点は、**編成定義の先頭車の車体中心**とする。「先頭車」は編成定義上の固定された車両であり、後退や使用運転台の切替で基準車・基準点・車両順を変更しない。

線路上の位置と編成前方向は、次の組で表す。

| 項目 | 意味 |
| --- | --- |
| Edge ID | 基準点が乗っているEdge |
| Node Aからの実距離 | Node Aから基準点まで、実際の線路に沿って測った距離[m]。範囲は0からEdge実長まで（両端を含む） |
| 編成前方向 | 編成の固定された前側が、そのEdgeのA→B／B→Aのどちらを向いているか |

Edge内の距離は、逆向きに走ってもNode A起点のままとする。基準Geometry上のパラメーター距離とは区別し、必要な変換は距離表で行う。

後退しても編成前方向を反転させない。別のEdgeへ遷移したときは、編成の物理的な前方向を保つように、接続先EdgeのA/Bに合わせて方向の値を更新する。

## 2. 移動距離の符号

入力する移動距離は、**固定された編成前方向への移動を正、編成後方への移動を負**とする。使用中の運転台を符号の基準にしない。運転台に応じた指令方向の変換は操作側で行う。

例：編成前方向がB→A、基準点がNode Aから40mにある場合、同じEdge内では次のようになる。

| 要求する移動距離 | 移動後のNode Aからの距離 | 編成前方向 |
| --- | --- | --- |
| +10m | 30m | B→Aのまま |
| −10m | 50m | B→Aのまま |

Trackは渡された符号付き移動距離を適用する。速度・時間からの距離積分はPhysicsで行い、Simulationがその出力をTrackへ渡す。Trackで再計算しない。

## 3. 移動結果

| 項目 | 意味 |
| --- | --- |
| 更新後の位置・編成前方向 | 移動後のEdge ID、Node Aからの実距離、編成前方向 |
| 実移動距離 | 実際に進めた符号付き距離[m]。入力と同じ符号規則を使う |
| 未消費距離 | 要求距離から実移動距離を引いた符号付き距離[m] |
| 終了理由 | 下記の区分で、移動を終えた理由を示す |

距離の関係は、`要求距離 = 実移動距離 + 未消費距離`とする。

例：−15mの後退要求に対し、線路終端まで10mだけ後退できた場合は、実移動距離−10m、未消費距離−5m、終了理由「線路終端」を返す。

### 終了理由

| 区分 | 意味 |
| --- | --- |
| 移動完了 | 要求距離をすべて移動した。有効な位置・データに対する0mの要求も含む |
| 線路終端 | 続くEdgeがなく、要求距離が残った |
| 接続先未確定 | 接続候補が複数あり、進むEdgeを決められない |
| 入力・データ不正 | 不明なID、範囲外の位置、不正な距離など |
| 探索上限到達 | Edge遷移の回数制限に達した |

**基準点が線路終端ぴったりまで進み、要求距離をすべて消費できた場合は「移動完了」**とする。さらに先へ進む要求が残った場合に「線路終端」とする。

## 4. W1-03の終端制限

移動可能範囲は**基準点だけで判定**する。基準点が線路外へ出なければ進める。

車体前端や編成後端が線路外にはみ出すことは許容し、編成全体を線路内に収めるための移動制限は今回実装しない。例えば20m車でも、前端のために基準点を終端の10m手前で止める処理は入れない。

台車・後続車などの位置評価に必要な線路が足りない場合の有効性は、基準点の移動制限と分けて扱う。既存の移植方針どおり、必要距離未達を有効な位置とみなして末端へ重ねて配置しない。

## 5. 編成寸法との関係

車体前後端・台車位置・編成後端は、中心基準の位置、編成寸法、経路から算出する。

現行の`CarDefinitionAsset`は`lengthM`と`bogieCenterDistanceM`を持つ。前後対称の台車配置なら、車体中心に対する台車中心のオフセットは±`bogieCenterDistanceM / 2`、車体前後端は±`lengthM / 2`となる。Edge境界を跨ぐ場合も経路に沿って評価する。

現行データに車間距離の独立した項目はない。既存の移植方針では、隣接車両の中心間距離は両車の`lengthM`の半分ずつの和として扱う。

本書は位置・方向・移動結果の基本契約を記録する。Geometry参照解決、実距離評価、Edge移動、通過履歴の実装結果は[W1-03完了記録](../Validation/W1-03-completion-2026-09-24.md)を参照。移動結果DTOは今回省略し、境界などの体系的な検証はW1-05で行う。

## 6. Nakatetsu TrackGraphのJSON定義

元データは`SourceAssets/Track/Network/NakatetsuTrackGraph/graph.json`と、その`files`に列挙したJSONに置く。`Stations/NT01.json`などは駅、`Sections/NT01_NT02.json`などは駅間の定義を持つ。線区全体で使用するGeometryは`Shared/Geometry.json`に一度だけ定義する。境界のNodeなどはIDを持つ駅・駅間のファイルに定義し、別ファイルから参照できる。

各ファイルは`geometries`・`nodes`・`edges`・`connections`・`circuits`を持ち、既存のDefinitionと同じフィールド名を使う。曲線・勾配・横オフセットは、Unityのmanaged reference IDではなく`type`で種類を指定する。水平線形は`Straight`・`Circular`・`TransitionIn`・`TransitionOut`、勾配は`ConstantGradient`・`LinearGradient`、横オフセットは`Constant`・`Linear`・`Cubic`・`CubicHermite`・`Arc`・`TransitionIn`・`TransitionOut`を使用できる。

Unityのメニュー`Nakatetsu > Track > Generate Nakatetsu Graph from JSON`で、全JSONを結合し、IDの重複・参照先を検証してAssetを生成する。`assetPath`が出力先、`integrationStepM`が距離表生成時の刻み幅[m]である。`distanceMap`はJSONに保存せず、既存の`TrackGraphCompiler`で生成する。既存Assetを更新する場合はGUIDを維持する。入力の読み込み・検証・距離表生成に失敗した場合は、既存Assetを変更しない。

軌道回路の`controlKind`には`"Block"`または`"Interlocking"`を指定する。既存のATC生成元から移した回路の属性で、C#の`TrackCircuitDefinition.controlKind`に読み込み、生成Assetにも保存する。未指定の旧定義は`Unknown`になる。現時点ではこのフィールドを走行・在線・ATC・連動の動作判定には使用せず、ATCは従来のATC用設定を参照する。

定義を変更するときはJSONを編集してAssetを再生成する。ATCの進路は今回のTrackGraph移行には含めない。

Goサーバーは`Server`ディレクトリから`go run .`で起動すると、gRPCの待ち受け開始前に同じTrackGraph JSONを読み込む。別の一覧ファイルを使う場合は`-track-graph <一覧JSONのパス>`を指定する。読み込んだ回路定義はCTC本体が保持する。

JSONの回路に`toCircuitId`が指定されていれば、そのIDの存在を確認してそのまま採用する。空の場合はBlockだけを対象に、`sections`の距離範囲・Edgeの通常走行方向・固定接続から次の回路を補完する。転轍機、複数候補、方向不明、循環、終端では未設定のまま残し、Interlockingは自動補完しない。この補完は起動時のメモリ上だけで行い、元のJSONを書き換えない。

進路定義は`SourceAssets/Track/Network/NakatetsuInterlocking/interlocking.json`の`stations`に登録された駅別JSONから読み込む。別の一覧ファイルは`-interlocking <一覧JSONのパス>`で指定する。各進路の`trackCircuitIds`は、`startTrackCircuitId`から`destinationTrackCircuitId`まで、進入元を含む進行方向順の回路列とする。初期データは既存ATC生成元の回路列を基に、含まれていなかった進入元を先頭に加えて作成した。空き照査用の`routeClearTrackCircuitIds`を走行順として流用しない。Unityの連動Asset生成ではこの追加項目を使用せず、ATC生成元も従来どおり別管理する。

Goの`LoadTrackRouteDefinitions`は駅ID・進路ID・回路IDの重複、参照先、回路列の両端を検証する。読み込み後はCTC本体が進路定義と`BuildRouteCircuitRefs`で作る候補辞書を保持する。

`Protocol/ctc.proto`の`UnitySnapshot.track_circuits`に、全回路の`occupied_by_circuit_id`（回路ID → 在線bool）を送る。非在線の`false`も含める。完了Tickの在線情報が未取得なら`track_circuits`自体を省略する。省略と全回路非在線は区別し、存在しないIDを非在線と解釈しない。

Unityの`ApplicationSimulationController`は、完了Tickの在線を`CompletedOccupiedByCircuitId`にコピーして保持する。`UnitySnapshotSender`はそのコピーを時刻・連動状態とまとめて電文へ移し、非同期送信中に次Tickの更新が混ざらないようにする。Goの`CTC.UpdateSnapshot`はコピーしたsnapshotを`BuildTrackingInput`で変換し、初期登録が完了して有効な在線情報がある場合に`UpdateTracking`を呼ぶ。更新後の在線は次回の比較用に保存する。未受信の場合は追跡状態を更新しない。

CTCの回路状態は`TrackingState.CircuitsByID`で管理し、各`TrackCircuitState`に前回処理した`IsOccupied`と`TrainID`を持つ。`TrainID`を置くのは最後尾側の基準回路だけとし、列番・編成番号・運用番号は`TrainsByID`の`TrainState`から取得する。`TrainState`は回路IDを持たない。`handleCircuitExit`では動的リンク先への在線を確認し、元回路の`TrainID`を次回路へ移してリンクを消費する。移動先に別列車が登録されている場合は上書きせず`NeedsReset`とする。受信した在線boolを保存するときも、回路に保持している`TrainID`は残す。

`TrackingState.IsInitialized`は最初の有効なsnapshotから列車の初期登録を完了した時点で一度だけ`true`になる。時刻・連動メッセージがあり、全回路の在線情報を過不足なく含み、`train_initialization`で全列車の個体IDと基準回路を取得できていることが条件となる。列車0本の場合も、全回路非在線と空の`train_initialization`メッセージを送る。フィールド省略は未取得として待つ。

Unityでは`TrainRoot.TrainId`を通信上の`consist_id`に使い、編成番号・列番・運用番号を`TrainRoot`のInspectorで設定する。これらの番号は空でもよい。`ConsistDefinitionAsset.consistId`は車種共通の値なので個体IDには使わない。初期軌道回路は`TrainRoot`の`Initial Circuit Id`に手動設定し、その値を`reference_circuit_id`として送る。占有区間から回路IDを自動判定する処理は持たない。`CompletedTrainInitialization`は在線情報と同じ完了Tickで設定値をコピーし、毎回snapshotへ載せる。列車ソースの登録完了を明示した後の0本は、未設定とは区別して全回路非在線を生成できる。

Goは全件を検証してから`train-000001`などの内部IDを発行し、`TrainsByID`と回路の`TrainID`にまとめて登録する。個体ID・初期回路の未設定や重複、定義外や非在線の初期回路、未登録の在線回路がある場合、`UpdateSnapshot`は`ErrTrainInitialization`で識別できるエラーを返し、途中までの登録を残さない。gRPCでは`FailedPrecondition`としてUnityへ返し、送信側はConsoleへ初期化エラーを表示する。初期情報自体が未受信なら待機する。登録後のsnapshotでは初期情報を再採用せず、位置は`handleCircuitExit`で追跡するため、初期回路が空いても初期化エラーにはならない。未送信や回路IDの欠落・定義外IDがある在線snapshotでは追跡状態を更新せず、受信snapshotだけ保存する。初期登録済みのフラグは、その後の不完全なsnapshotでは戻さない。`CTC.IsInitialized()`とコピーを返す`CTC.GetTrackingState()`で確認できる。
