# 運転デバッグ

本書は既存のInspector操作の説明。W1-04で採用するキー操作・初期状態・レバーサー操作条件は[キーボード運転入力仕様](KeyboardControls.md)を参照。ノッチ・レバーサー・EBリセットのキー入力と、新仕様のレバーサー操作条件を実装済み。

編成ルート（TrainRootがあるGameObject）に、Add Component → Nakatetsu → Train → Debug → Train Debug Controllerを追加する。

前提は編成定義を持つTrainRoot、TrainEquipmentBuilder、TrainSimulationBuilder、TrainSimulationController、TrainPhysicsControllerと、子のTims.prefab。両BuilderのBuild On Awakeを有効にする。デバッグ機能は機器の生成やTIMSの設定を自動変更しない。

1. Playを開始し、Train Debug ControllerのInspectorを開く。
2. 「前側運転台を準備」または「後側運転台を準備」を押す。前後の運転台選択スイッチを設定し、ノッチをNにして、選択した運転台のレバーサーを前進にする。
3. TIMSの更新後、Pボタンで発進。Bボタンで常用制動、Nで惰行、非常ブレーキで非常ノッチを操作する。
4. 非常ノッチにしてレバーサーを後進へ切り替えれば、同じ運転台から後退指令を出せる。レバーサー操作自体には速度制限を設けない。

速度の符号は編成前方を正とする。レバーサーの前進・後進は選択中の運転台を基準とするため、後側運転台の前進では負の速度になる。

Inspectorにはマスコン、TIMS非常・指令有効状態、各車の回生能力・実回生力・回生指令・空制指令・BC圧、EBの残り時間を表示する。生成後に機器を追加した場合は「機器を再取得」を押す。

TIMSは現在の非常要求を集約する。手動非常はノッチを戻してほかの要求がなくなれば解除する。EB装置の作動後の保持・解除は装置自身が担当し、停車・力行なしで解除する。デバッグ機能からEBの保持状態や物理速度を直接書き換えない。Unityを一時停止中はTIMSも更新されないので、準備後に再開またはフレーム送りが必要。

操作用コンポーネントとEditor専用Inspectorは独立したAssemblyに置き、Train本体からTIMSへの依存は追加しない。テストファイルは追加していない。

この機能は運転指令の操作と制御状態の確認を担当する。車両モデルの線路上移動は、Physicsが速度・移動距離を計算し、TrainSimulationControllerが移動距離をTrackへ渡し、Presentationが線路上の姿勢を車体へ反映する。NtLineシーンではW1-03でこの接続を実装済み。
