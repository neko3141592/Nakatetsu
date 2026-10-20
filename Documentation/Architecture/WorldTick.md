# W1-02：世界時計とシミュレーションステップの設計

- 合意日：2026-09-22
- 対象：[実装計画 W1-02](../implementation-plan-2026-11-17.md)
- 状態：**W1-02完了（2026-09-22）**。固定tick・時計・停止／再開・再生倍率・開発用UIを実装。本人の動作確認報告と簡易テストの結果は [完了記録](../Validation/W1-02-world-tick-2026-09-22.md) を参照。

## 1. 責務

`Application`が世界時計、ステップ数、再生倍率、一時停止状態を管理する。各TrainはApplicationから渡された時間分だけ、既存の内部更新順でシミュレーションを進める。

Applicationの管理下にあるTrainは、自身の`Update()`による自動進行を停止し、外部Stepとの二重更新を防ぐ。UIはApplicationの停止状態に合わせて表示する。

## 2. 世界時計

- 運行時刻はシナリオで指定した時刻から開始する。
- 現実の時計とは独立して進む。
- 世界時計と列車は同じシミュレーション時間で進む。一時停止・早送りは両方に適用する。
- 全Trainの1tick分の更新を終えた後、世界時計を1tick分進める。

## 3. 毎Updateの可変ステップと早送り

2026-10-18の変更により、固定tickの蓄積・繰り返し方式から次の方式へ移行する。

- 自動実行では`Update()`ごとに`Time.unscaledDeltaTime * playbackSpeed`を1回計算し、その時間で世界全体を1回進める。
- 同じステップ内ではTrain・転てつ器・占有・連動・地上ATCへ同じdeltaTimeを渡し、既存の更新順を維持する。
- 再生倍率はステップの時間幅へ掛ける。倍率に比例してステップ回数を増やさない。
- 1Update内の反復・固定tick待ち・未処理時間の蓄積・フレームあたりの回数上限・実時間予算を削除する。今回の方式ではdeltaTimeの分割や切り捨ても行わない。
- 0以下・NaN・InfinityのdeltaTimeでは計算・公開状態・世界時計・完了回数を更新しない。
- `ElapsedTimeSeconds`は完了したステップに渡したdeltaTimeをdoubleで累積する。`CompletedTickCount`は完了ステップ数であり、時刻の計算には使用しない。
- `TickDurationSeconds`は最後に完了したステップのdeltaTimeを返す。初回前は手動実行用の`tickDurationSeconds`を返し、既存のsnapshot形式で送信する。

例：60FPSで3倍速なら、1Updateの世界計算は約50msで、実時間1秒に約60回の計算で約3秒進む。30FPSで3倍速なら約100msになる。計算幅がFPSに依存するため、固定tick時と同じ時刻の速度・停止位置・ATC介入・連動の結果が完全一致するとは扱わない。大きなdeltaTimeの挙動は別に確認する。

## 4. 1tickの更新順

```text
Applicationが1tickを開始
  → 各Trainを既存の内部更新順で1回ずつ進める
  → 全Trainの更新完了後に世界時計を1tick分進める
```

Applicationは個々の車両機器の更新を直接管理せず、Train側のStepに任せる。将来のダイヤ・信号などの処理は、この更新順を基礎に追加する。具体的な挿入位置は各機能の設計時に決める。

## 5. 一時停止と再開

- 自動tickを止め、世界時計と全Trainの時間進行を止める。
- UIに「一時停止」とメニューを表示する。
- 停止中もUIの操作と描画は続ける。
- 停止中の実時間で機器・世界時計を進めない。再開時にも停止していた時間分を加算しない。

## 6. フレーム時間と処理負荷

毎Updateで1回実行するため、フレーム時間が長いと次のステップ幅も大きくなる。世界時計だけを先行させず、すべての機器を同じdeltaTimeで計算した後に時刻と完了出力を公開する。FPSが低下した場合の制御精度・境界通過・時素の検出粒度は検証対象とする。

3倍速の実効倍率は、完了した`ElapsedTimeSeconds`の増分を同じ区間の実時間で割って求める。ProfilerではステップあたりのCPU時間に加え、毎秒のステップ数・FPS・GC Alloc・シミュレーション計算の実時間あたりCPU負荷を比較する。

## 7. 開発用の手動実行

- 一時停止中に、`tickDurationSeconds`で指定した時間だけ世界全体を1回進める。Inspectorには手動実行用の設定であることを表示し、自動実行のステップ幅には使用しない。
- 再生倍率を掛けず、全Trainを進めた後に時計を進める通常実行と同じ処理を利用する。
- 通常の一時停止メニューとは別の開発用操作として設ける。

## 8. 実装と確認の進め方

1. Trainの手動Stepと自動更新停止を接続し、二重更新を防ぐ。
2. Applicationに時計・可変ステップ・再生倍率・停止状態の管理を実装する。
3. 一時停止UIと開発用の1tick操作を接続する。
4. 時計と列車の同期、停止・再開、早送り、手動実行を検証する。
5. 将来のsnapshotに保存すべきStateとその所有者を一覧化する。

検証では、各ステップのdeltaTimeの合計だけ時刻が進むこと、毎Updateで1回だけ進むこと、停止中にシミュレーション状態が進まないこと、再開時に停止していた実時間を加算しないこと、可変ステップ後も手動実行が設定時間・倍率非依存で進むこと、時刻・完了ステップ数・直近ステップ幅がsnapshotでも一致することを確認する。

現コードの`ISimulationController`は`Calculate`と`ApplyOutput`の2メソッドであり、[実装ルール](ImplementationRules.md)に記載された3段階の契約との差分がある。W1-02では既存Trainの内部更新順を尊重し、全機器の一括改修へ広げず世界側の入口を整える。

snapshotの保存・復元本体はW5、線路上の移動はW1-03以降の対象とする。

## 9. 現在の実装（2026-10-18更新）

- `ApplicationSimulationController`が手動ステップ幅、完了ステップ数、累積経過時間、直近のステップ幅、開始時刻、停止状態、倍率を所有する。
- 手動ステップ幅は正の有限値を検証する。自動実行のdeltaTimeは毎Updateで取得する。
- 全Trainの`Start()`で参照収集が完了し、`IsInitialized`がtrueになってから実行する。
- `Time.unscaledDeltaTime * playbackSpeed`で毎Updateに1回進める。再生倍率はInspectorで指定する。
- 停止中の実時間は加算せず、未処理時間も保持しない。
- 世界時刻は開始時刻（時・分）＋完了したdeltaTimeの累積。開始時刻はAwakeで確定し、24時間を超えて保持する。
- `Application/Debug/Scripts/ApplicationSimulationDebugPanel`が時計・倍率・停止状態と停止／再開ボタンを表示する。時刻表示だけ24時間表記に変換する。
- 一時停止中の`StepOnce()`は自動更新と同じ1tick処理を使う。
- 正式なシナリオ読込・製品用メニュー、snapshot保存復元は後続工程。W1-02のUIはデバッグ用とする。

## TIMSの情報収集間隔

`TimsCommunicationController.localCollectionIntervalSeconds`と`masterCollectionIntervalSeconds`は各0.25秒。各車の機器からLocalBusへの収集と、ATCなどの共通機器からMasterBusへの収集を、独立したシミュレーション時間の周期で行う。初回は両方とも即時収集する。速度・ドア・TIMS指令の計算は毎tick、受信済みの値で続ける。共通機器もEquipment計算より前に収集するため、ATC表示は前tickまでに確定した計算結果を使う。MasterBusから機器への出力側に別の転送待ちは追加しない。

`TrainSimulationController`が`IEquipmentInputSourceCollector.CollectInputSources(deltaTimeSeconds)`へそのステップのdeltaTimeを渡す。初回の正のtickは即時収集し、以後は指定間隔で更新する。端数は持ち越すため、20ms tickでは更新間隔が260ms・240msとなり、平均250msになる。一時停止中はtickが来ないため待ち時間も進まない。0秒設定では毎tick収集する。

時間引数なしの`CollectInputSources()`は既存テスト・手動確認用の即時収集として維持する。将来のsnapshotには、通信Stateの`hasCollectedSources`と`collectionElapsedSeconds`も含める。


## 2026-10-18：3倍速の通常Profiler計測

Unity 6000.4.0f1、Deep Profile OFF、未保存のNtLineシーンを保存・破棄せず使用。変更前は実行時の固定ステップ17.5ms・処理予算7.5ms・最大100tick/frame・倍率3、変更後は同じシーンの毎Update可変ステップ・倍率3。各採取でPlayを再開し、シミュレーションを止めたままProfilerを1秒以上動かしてから約4秒記録した。取得済みの前後各3回を集計した。実効倍率はシミュレーション経過時間の増分／実時間、FPSはUnityのframeCountの増分／実時間で算出。短いEditor計測のため製品ビルドの性能を保証する値ではない。

| 指標 | 固定ステップ | 毎Update可変ステップ |
| --- | ---: | ---: |
| 3x指定時の実効倍率 | 1.79x（1.66〜1.93x） | 3.01x（3.00〜3.01x） |
| FPS | 52.7 | 72.0 |
| 世界更新回数／秒 | 102.3 | 72.0 |
| 世界更新CPU／描画frame | 9.94ms | 5.30ms |
| 地上ATC CPU／描画frame | 5.09ms | 2.65ms |
| 世界更新CPU／ステップ | 5.12ms | 5.30ms |
| 世界更新GC Alloc／描画frame | 約1.34MiB | 約0.70MiB |

描画frameあたりの世界更新CPUは46.7%減、実時間1秒あたりでは約524→381ms（27.2%減）。ステップそのものの計算を高速化した変更ではなく、世界更新を毎frame1回にして各回で進む時間を増やした結果である。GC Allocも1ステップあたりでは約725,357→733,942Bと同程度で、描画frameあたりの減少は更新回数の減少による。実効倍率の約3xへの追従を確認したが、固定ステップとの停止位置・制御結果の完全一致を示す計測ではない。

Unityコンパイル成功、Applicationの既存14ケース＋可変ステップ関連11ケースの25/25が成功。未保存シーンを保つため、Play中のUnityメインスレッド上でUnityTestAssemblyRunnerを使い同期Testを実行した。時刻累積・snapshotの直近幅・可変時間での転換完了・1Update1回・倍率・停止・不正時間・手動実行の継続を確認。再生とProfiler記録は終了し、シーンの未保存状態と元の停止設定を維持した。
