# 運転台のEBブザー

## 責務

- `Train/Equipment/Safety/Eb`：60秒無操作 → 5秒の警報猶予 → 非常要求。猶予中はSpace・マスコン操作でリセットできる。非常作動後は、停車・力行なしで解除されるまで警報要求も保持する。
- `Train/Presentation/Audio/Scripts/CabAudioController.cs`：同じ編成・車両IndexのEB装置をStart時に接続し、計算済みの`IsBuzzerRequested`を読んで再生・停止する。装置の時間や状態は変更しない。
- `Application/Player/Scripts/TrainFocusController.cs`：注目編成とキーボード入力先を管理する。音声には依存しない。
- `Application/Audio/Scripts/ApplicationAudioController.cs`：Focusを読み、注目編成の有効運転台だけに再生を許可する。PresentationからApplicationを参照しない。

ブザー以外の警笛・操作音などは、今回の実装には含めない。

## GameObject

アプリ側の選択処理と、各車両の再生処理を別のGameObjectに置く。

```text
Application
├── Focus     TrainFocusController
└── Audio     ApplicationAudioController → Focusを参照
```

`ApplicationSceneSetup`はAudioの配置とFocus参照も設定する。

Tc1の既存の`Presentation/Cab/Audio`に追加する。CabModuleモデル内には配置しない。Tc2はTc1のPrefabを参照するため同じ構成を引き継ぐ。

```text
Tc1
└── Presentation
    └── Cab
        └── Audio
            └── CabAudio         CabAudioController
                └── EbBuzzer     AudioSource
```

実行時はPresentationBuilderが車両ルートに`TrainPresentationAssignment`を付ける。Audioはその車両Indexと同じEBを参照し、他編成・他車両には接続しない。EBが複数ある場合は接続しない。機器を再生成した場合は`BindEbFromAssignment()`で接続を更新する。

## 再生許可

CabAudioはOnEnable/OnDisableで有効な音源の一覧に登録・解除する。ApplicationAudioControllerは一覧から対象を選ぶため、毎フレームのシーン全体探索は行わない。TIMSの有効運転台がFrontなら車両Index 0、Rearなら最後尾を対象とする。現在のカメラと同じ選択基準。

注目先変更・有効運転台なし・Focus無効化・ApplicationAudioController無効化・音源無効化では、対象外の音を停止する。再び注目したときは現在の装置出力を読む。過去の警報をキューにためない。同一車両に複数のCabAudioがある場合は再生を許可しない。

ApplicationAudioControllerはLateUpdateでFocusを読み、そのフレームの再生許可を更新する。Focusが未設定・無効なら全運転台を停止する。ApplicationAudioController自身を無効化するとOnDisableで即座に全運転台を停止する。

運転台内音は2D、Loop有効、Play On Awake無効。音量の初期値は0.2。AudioListenerはプレイヤーカメラの既存のものを使う。連続鳴動中は毎フレームPlayし直さない。

## 素材

- 元音声：`SourceAssets/Train/Equipment/Tims/Sounds/EB.wav`。変更しない。
- 再生用：`Assets/Nakatetsu/Train/Presentation/Audio/Clips/EbBuzzer.wav`。

約110秒の元音声から2秒のループを作成し、モノラル・48 kHz・16 bit PCMで書き出した。ループ境界の20 msをクロスフェードして接続している。ImporterはPCM・Decompress On Load。音の交換はEbBuzzerのAudioSourceのClip、音量調整はVolumeで行う。
