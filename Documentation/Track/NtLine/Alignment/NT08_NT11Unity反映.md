# NT08〜NT11 Unity反映記録

2026-10-13。最新の線形図・配線図を共通Graph・ATC・NT11連動へ反映し、Unity Native形式で保存・再読込した。NT07古谷方の末尾30mを延長し、NT08〜NT11の44 Edge・66回路を追加した。現在の全線は119 Edge・103 Node・95 Connection・154回路・245 Section、Compiled ATCは194 Edge・178 Node・56進路。ホームMesh・Prefab・ホームのScene配置は未作成。

## 保存先

| 保存先 | 内容 |
| --- | --- |
| [NakatetsuGraph.asset](../../../../Assets/Nakatetsu/Track/Network/Data/NakatetsuGraph.asset) | 共通Geometryを11860mまで延長。44 Edge追加・NT07 outgoing 4 Edge延長、66回路追加・古谷方4回路延長。既存71 Edge・84回路は保持。 |
| [NakatetsuAtcGraphSource.asset](../../../../Assets/Nakatetsu/Track/Network/Data/ATC/NakatetsuAtcGraphSource.asset) | 新回路setting・NT11進路Sourceを追加し、既存設定を保持。 |
| [NakatetsuAtcGraph.asset](../../../../Assets/Nakatetsu/Track/Network/Data/ATC/NakatetsuAtcGraph.asset) | `NakatetsuAtc`、194 Edge・178 Node・56進路をNativeコンパイル。NT01・NT05・NT07・NT11の連動Asset参照を登録。 |
| [Interlocking/NT11.asset](../../../../Assets/Nakatetsu/Track/Network/Data/Interlocking/NT11.asset) | 18進路・11転轍機鎖錠。構内18回路＋接近2回路を所属登録。 |
| [NtLine.unity](../../../../Assets/Scenes/NtLine.unity) | GameObject `NT11` に `TrackStationInterlockingController` を追加し、Managerへ登録。既存の登録・列車設定を保持。 |

Graph SHA256：`9c41dc27564fbbdc7084cb6cf4653f4924f67dce4dfa917b2d679da4ada60b41`。回路の実ID・保存値は[全線回路カタログ](../Wiring/軌道回路区間一覧.json)に一致する。

## 線形と回路

距離sはNT01車止めを0mとする共通Geometry `NT01_baseline`。駅・回路の220mはGeometry距離差であり、距離表の走行実距離`lengthM`とは区別する。曲線表・線形図は理論設計値、回路表・JSONはNativeの単精度保存値。新設の独立Geometryはない。

NT07の既存7500〜7900m Cubicを保ち、outgoing 4 Edgeへ7900〜7930mの同横位置Constantを追加した。`NT07_NT08_DE1T`・`DL1T`・`UL1T`・`UE1T`は7020〜7930mへ延長する。NT08・NT09・NT10の各4駅Edgeは7930〜8150／8730〜8950／9730〜9950mで各駅回路と一致する。ホーム210mは回路の各端5m内側に置く資料寸法。

NT11はホーム240m、入口21〜24の主線・斜線の共通境界、25イT／25ロT、26T・中央電留Tを最新図に合わせた。斜線の交差は非接続。中央26はs=11160m・局所x=0m、定位は3番線側曲線、反位は2番線側曲線。2・3番線からArc＋斜直線＋逆Arcで中央へ合流し、共通引上`NT11_track3_stub`は11160〜11400m、電留Tは11180〜11400mの220m。200m車体11190〜11390mは資料上の停止範囲。

| 境界 | 理論設計s (m) | Native保存s (m) |
| --- | ---: | ---: |
| 入口イ／ロ共通境界 | 10227.127487 | 10227.1279296875 |
| 入口シーサス終端Node | 10274.254973340 | 10274.2548828125 |
| 25イT／26T・対称Arc始端 | 11020.012484399 | 11020.0126953125 |
| 26共通Node | 11160 | 11160 |
| 26T／電留T | 11180 | 11180 |

26の片側2.862405226°・全角5.724810452°は[JP5773780B2の式1](https://patents.google.com/patent/JP5773780B2/ja#description)の`β=2atan(1/(2n))`をn=10で計算した値。R400mはゲーム用設計半径で、10番の規格半径ではない。

### 論理経路とNative Edge

図面の連続経路を分岐点Nodeで分けた。回路はこれらの実IDへ展開し、同じ回路が複数Edgeにまたがる区間を245 Sectionとして保存する。旧論理経路・区間はJSONの`logical_edges`・`logical_sections`、実ID対応は`logical_to_native_edge_map`に区別して保持する。

| 図面の論理経路 | Native Edge IDと保存区間s (m) |
| --- | --- |
| `NT07_P01_050` | `NT07_P01_050` 6962.32373〜7930 |
| `NT07_DL_070` | `NT07_DL_070` 6975.420898〜7930 |
| `NT07_UL_070` | `NT07_UL_070` 6975.420898〜7930 |
| `NT07_P04_050` | `NT07_P04_050` 6962.32373〜7930 |
| `NT08_track1` | `NT08_track1` 7930〜8150 |
| `NT08_NT09_DE` | `NT08_NT09_DE` 8150〜8730 |
| `NT08_track2` | `NT08_track2` 7930〜8150 |
| `NT08_NT09_DL` | `NT08_NT09_DL` 8150〜8730 |
| `NT08_track3` | `NT08_track3` 7930〜8150 |
| `NT08_NT09_UL` | `NT08_NT09_UL` 8150〜8730 |
| `NT08_track4` | `NT08_track4` 7930〜8150 |
| `NT08_NT09_UE` | `NT08_NT09_UE` 8150〜8730 |
| `NT09_track1` | `NT09_track1` 8730〜8950 |
| `NT09_NT10_DE` | `NT09_NT10_DE` 8950〜9730 |
| `NT09_track2` | `NT09_track2` 8730〜8950 |
| `NT09_NT10_DL` | `NT09_NT10_DL` 8950〜9730 |
| `NT09_track3` | `NT09_track3` 8730〜8950 |
| `NT09_NT10_UL` | `NT09_NT10_UL` 8950〜9730 |
| `NT09_track4` | `NT09_track4` 8730〜8950 |
| `NT09_NT10_UE` | `NT09_NT10_UE` 8950〜9730 |
| `NT10_track1` | `NT10_track1` 9730〜9950 |
| `NT10_NT11_DE` | `NT10_NT11_DE` 9950〜10140 |
| `NT10_track2` | `NT10_track2` 9730〜9950 |
| `NT10_NT11_DL` | `NT10_NT11_DL` 9950〜10140 |
| `NT10_track3` | `NT10_track3` 9730〜9950 |
| `NT10_NT11_UL` | `NT10_NT11_UL` 9950〜10140 |
| `NT10_track4` | `NT10_track4` 9730〜9950 |
| `NT10_NT11_UE` | `NT10_NT11_UE` 9950〜10140 |
| `NT11_track1` | `NT11_track1_approach` 10140〜10180 / `NT11_track1_scissors` 10180〜10274.254883 / `NT11_track1` 10274.254883〜11080 / `NT11_track1_stub` 11080〜11860 |
| `NT11_track2` | `NT11_track2_approach` 10140〜10180 / `NT11_track2_scissors` 10180〜10274.254883 / `NT11_track2` 10274.254883〜10900 / `NT11_track2_to26` 10900〜11160 |
| `NT11_track3` | `NT11_track3_approach` 10140〜10180 / `NT11_track3_scissors` 10180〜10274.254883 / `NT11_track3` 10274.254883〜11160 / `NT11_track3_stub` 11160〜11400 |
| `NT11_track4` | `NT11_track4_approach` 10140〜10180 / `NT11_track4_scissors` 10180〜10274.254883 / `NT11_track4` 10274.254883〜11860 |
| `NT11_crossover21` | `NT11_crossover21` 10180〜10274.254883 |
| `NT11_crossover22` | `NT11_crossover22` 10180〜10274.254883 |
| `NT11_crossover23` | `NT11_crossover23` 10180〜10274.254883 |
| `NT11_crossover24` | `NT11_crossover24` 10180〜10274.254883 |
| `NT11_crossover25` | `NT11_crossover25` 10900〜11080 |

### 保存済み駅Edgeの距離表

| Native Edge | Geometry開始s | Geometry終了s | 走行実距離 lengthM (m) |
| --- | ---: | ---: | ---: |
| `NT08_track1` | 7930 | 8150 | 220.001099 |
| `NT08_track2` | 7930 | 8150 | 220.001099 |
| `NT08_track3` | 7930 | 8150 | 220.001099 |
| `NT08_track4` | 7930 | 8150 | 220.001099 |
| `NT09_track1` | 8730 | 8950 | 220.002396 |
| `NT09_track2` | 8730 | 8950 | 220.002396 |
| `NT09_track3` | 8730 | 8950 | 220.002396 |
| `NT09_track4` | 8730 | 8950 | 220.002396 |
| `NT10_track1` | 9730 | 9950 | 220.002213 |
| `NT10_track2` | 9730 | 9950 | 220.002213 |
| `NT10_track3` | 9730 | 9950 | 220.002213 |
| `NT10_track4` | 9730 | 9950 | 220.002213 |
| `NT11_track1_approach` | 10140 | 10180 | 40.000324 |
| `NT11_track1_scissors` | 10180 | 10274.254883 | 94.255699 |
| `NT11_track2_approach` | 10140 | 10180 | 40.000324 |
| `NT11_track2_scissors` | 10180 | 10274.254883 | 94.255699 |
| `NT11_track3_approach` | 10140 | 10180 | 40.000324 |
| `NT11_track3_scissors` | 10180 | 10274.254883 | 94.255699 |
| `NT11_track4_approach` | 10140 | 10180 | 40.000324 |
| `NT11_track4_scissors` | 10180 | 10274.254883 | 94.255699 |
| `NT11_track1` | 10274.254883 | 11080 | 805.847778 |
| `NT11_track1_stub` | 11080 | 11860 | 780.235779 |
| `NT11_track2` | 10274.254883 | 10900 | 625.756104 |
| `NT11_track2_to26` | 10900 | 11160 | 260.150696 |
| `NT11_track3` | 10274.254883 | 11160 | 885.906433 |
| `NT11_track3_stub` | 11160 | 11400 | 240.004654 |
| `NT11_track4` | 10274.254883 | 11860 | 1586.082886 |
| `NT11_crossover21` | 10180 | 10274.254883 | 94.356041 |
| `NT11_crossover22` | 10180 | 10274.254883 | 94.35556 |
| `NT11_crossover23` | 10180 | 10274.254883 | 94.355415 |
| `NT11_crossover24` | 10180 | 10274.254883 | 94.356247 |
| `NT11_crossover25` | 10900 | 11080 | 180.340424 |

## 連動とATC

NT08〜NT10の駅回路と全追加駅間はBlock制御、NT11構内はInterlocking制御。路線最高速度105km/h、新設回路の速度設定120km/hを踏襲した。NT01の40km/h区間を含め、既存回路設定・進路は保持。NT11は物理転轍機21イ／ロ・22イ／ロ・23イ／ロ・24イ／ロ・25イ／ロ・26の11点を鎖錠する。新しい過走防護は定義しない。

| 種類 | NT11進路ID（`NT11_`を省略） |
| --- | --- |
| 下り場内 | 101RE・102RE・101RL・102RL |
| 上り出発 | 203LE・203LL・204LE・204LL |
| 急行引上1 | 301R・302R・303LA・303LB |
| 中央電留・折返 | 304R・305R・306LA・306LB |
| 急行引上4 | 307R・308L |

`NT11_308L`は4引T→4LT。中間回路がないため、clearとreleaseの両方に`NT11_4LT`を含め、4LTが空くまで保持する。接近鎖錠は`NT11_4_STUB_T`・30秒、対向の307Rと相互排他。同方向の場内→引上への続行と、4番線・引上4の対向排他をNative保存・再読込後の動作で確認した。

## シーンへの登録

NtLine.unityのGameObject `NT11` に連動Controllerを追加し、NT11連動Asset・既存の回路Simulation・ConnectionControllerを参照させ、駅連動Managerの一覧にNT11を追加した。既存のNT01・NT05・NT07登録を保ち、Scene内のローカル参照を検証した。ホーム範囲を保持するGraph項目はなく、本工程のScene追加は連動装置の登録。図のホームと停止車体は寸法注記で、描画Mesh・Prefabは作成していない。

## 確認結果

- Unity Native保存・再読込：119 Edge・103 Node・95 Connection・154回路・245 Sectionを確認。既存71 Edge・84回路を保持。
- 全119 Edgeを回路Sectionが先頭〜終端まで隙間・重複なく覆い、全回路IDは一意。新39接続の位置・接線と分岐経路を確認。
- NT08〜NT10の駅Edgeと220m駅回路の境界一致、26の左右鏡映、交差部に接続Nodeがないこと、2・3ホーム同士の直接接続がないことを確認。
- ATC Nativeコンパイル：194 Edge・178 Node・56進路。NT11の18進路・11転轍機鎖錠と、全154回路の有効電文を確認。
- 全56進路の設定・開通・取消、NT07の4段階接続、26の定位／反位と2番線→電留→3番線の折返しを確認。
- 同方向の到着進路→引上進路の続行、4番線と引上4の対向排他を両方向で確認。308Lは4LTが空くまで解放しない。
- SceneのNT11 Controller・Manager登録と参照、他138 objectの保持を差分とローカル参照で検証。同構成ControllerのNative runtime smokeを確認。

ホーム210m／240m・幅・高さは資料寸法として管理する。線路・回路・ATC・連動への反映と、ホームMesh・Prefab作成を区別する。

### 電留線内の停止範囲

電留Tは11180〜11400mの220mを保持し、200m車体注記を11190〜11390mへそろえた。回路内の余裕は両端10m、26共通点から車体後端までは30m、車体前端から車止めまでは10m。占有判定`RangesOverlap`は境界一致も占有とするため、車体後端を26T末端11180mから10m離し、電留停車中に26Tを占有しない寸法とした。線路・回路・ホーム形状は変更していない。

電留実在線の追加Native smokeで、旧車体11180〜11380mが電留Tと26Tを同時占有することを再現した。修正後の11190〜11390mは電留Tのみ在線・26T空き。発点電留Tが在線したまま、306LA（26反位）・306LB（26定位）の双方でRouteSet・PathEstablished・ProceedAllowedが成立し、全154電文が有効。通常取消後は29秒で鎖錠を保持し、31秒で解放することも両ケースで確認した。既存56進路・NT07の4段階接続・対向排他・同方向続行の検証は維持される。
