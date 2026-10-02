# Nakatetsu 連動装置・過走防護・PRC連携 追加仕様

## 実装状況（2026-10-02）

本書は将来拡張を含む全体仕様であり、以下が今回の実装範囲となる。

- Normalの本進路・過走防護の予約競合照査、防護用転轍機の転換とてっ査鎖錠。
- 防護成立照査、`PathEstablished` と `ProceedAllowed` の分離、ATC入力への開通状態・防護方式の受渡しと既存入口判定への反映。
- 到着後の時間鎖錠、本進路と防護の独立解錠、取消時の保持、過走・回路状態不明時の自動解錠停止。
- 到着トリガ回路は、現在の実装では `routeLockTrackCircuitIds` に含まれる回路に限定する。

Restrictedの追加制御と要求側の許可は未実装のため、Normalを確保できなければ予約を拒否する。後続進路との共同保持、PRC連携、ATCの進路内探索本体、異常時の手動復旧は今後の実装となる。

自動昇格は初期実装の完成条件から外す。予約時に選択した防護方式は、その予約が終了するまで固定する。第15・18節等の自動昇格に関する記述は将来拡張案として扱う。

## 1. 文書の目的

本書は、現在の Nakatetsu の連動装置実装を基礎として、以下を追加するための仕様を定義する。

- 場内進路に対する過走防護
- 通常防護と制限付き防護の選択
- 到着後の過走防護時間鎖錠
- 過走防護解錠後の防護条件・現示の自動引上げ
- 本進路と過走防護の競合判定
- PRCから連動装置への進路設定要求
- 地上ATCとの責務分離
- 将来の順次解錠への拡張

本仕様では、既存の「進路中心」の連動モデルを維持する。

信号機・転てつ器間に個別の `lockedSignals` / `lockedTurnouts` のような相互参照を大量に追加する方式は採用しない。

---

## 2. 基準とした現行実装

確認時点の `main` 最新コミット:

`e4f1b0c72bd37aab2c760c170e74357f030845ea`

本仕様作成時に確認した主なファイル:

- `Assets/Nakatetsu/Track/Interlocking/TrackInterlockingDefinition.cs`
  - blob `17b09a43ccdc0176fa3b51a3721cbcd8503bdf64`
- `TrackInterlockingContext.cs`
  - blob `dc416d135e8daae3e79e539693bad967f00664e2`
- `TrackInterlockingLogic.cs`
  - blob `859aa23a49ea47714360f9e76b731e9c5c8969ca`
- `TrackInterlockingController.cs`
- `Assets/Nakatetsu/Track/Atc/Scripts/TrackAtcContext.cs`
- `TrackAtcController.cs`
- `Documentation/Track/TrackAtcGroundSpecification.md`
  - blob `e0484a80f7a18fc4109e58d3507e8feec00ac9ef`
- `Assets/Nakatetsu/Track/NtLine/Data/NtLineInterlocking.asset`
  - blob `7ab756792003097a1f003cc7e632615e14d5315a`

上記基準時点のコードには、過走防護専用の定義・状態はまだ存在しない。

---

# 3. 現行設計で維持する部分

## 3.1 `InterlockingRoute`

現在は以下を保持している。

```csharp
public sealed class InterlockingRoute
{
    public string routeId;

    public string startTrackCircuitId;
    public string destinationTrackCircuitId;

    public List<TurnoutRequirement> requiredTurnouts = new();

    public List<string> routeLockTrackCircuitIds = new();

    public ApproachLockDefinition approachLock = new();

    public List<string> conflictRouteIds = new();
}
```

この「進路を中心に必要設備を定義する」という構造は維持する。

特に、

```csharp
requiredTurnouts
```

は今後も本進路に必要な転てつ器位置を表す。

---

## 3.2 `ProceedAllowed`

現行の `ProceedAllowed` は廃止しない。

意味を明確に次のように定義する。

> `ProceedAllowed`
>
> この進路入口から、新しい列車が当該進路へ進入することを許可してよい状態。

したがって、

```text
進路開通・未進入
→ ProceedAllowed = true

列車が進路へ進入
→ ProceedAllowed = false
```

でよい。

`ProceedAllowed == false` は、

> 「進路が存在しない」

ことを意味しない。

進入後でも、列車を保護するための転てつ器鎖錠や経路情報は残る。

---

## 3.3 `RouteLocked`

現行コードでは `TryRequestRoute()` の受付時点で

```csharp
RouteLocked = true;
```

となる。

したがって `RouteLocked` は、

> 転てつ器の実位置照査まで完了した「開通済み進路」

を意味しない。

この意味は維持しつつ、後述する

```csharp
PathEstablished
```

を別に追加する。

---

## 3.4 `TurnoutTrackCircuitLock`

現在存在する

```csharp
TurnoutTrackCircuitLock
```

は、転てつ器のてっ査鎖錠として維持する。

過走防護用転てつ器を転換するときにも、この在線照査を省略しない。

---

# 4. 連動状態の追加

`TrackInterlockingRouteState` に以下を追加する。

```csharp
public sealed class TrackInterlockingRouteState
{
    public bool ProceedAllowed { get; internal set; }

    // 新規追加
    public bool PathEstablished { get; internal set; }

    public bool CancelPending { get; internal set; }

    public bool ApproachLocked { get; internal set; }
    public float ApproachReleaseRemainingSeconds { get; internal set; }

    public bool RouteLocked { get; internal set; }

    // 過走防護
    public OverrunProtectionMode OverrunMode { get; internal set; }
    public bool NormalUpgradePending { get; internal set; }
    public OverrunProtectionPhase OverrunProtectionPhase { get; internal set; }
    public float OverrunReleaseRemainingSeconds { get; internal set; }

    internal readonly Dictionary<
        string,
        TrackInterlockingCircuitPassageState
    > CircuitPassageById = new();
}
```

---

## 4.1 `PathEstablished`

意味:

> 本進路と現在選択されている過走防護に必要な転てつ器が、すべて実際に所定位置へ転換され、転換中ではなく、必要な安全条件が成立している状態。

概念的には、

```text
RouteRequested
       ↓
転てつ器要求
       ↓
ActualPosition照査
       ↓
過走防護条件照査
       ↓
PathEstablished = true
       ↓
ProceedAllowed = true
```

とする。

ATCが「進路内から前方経路を辿る」場合にも、この情報を使用する。

---

# 5. 本進路と過走防護を分離する

過走防護用の軌道回路・転てつ器を、

```csharp
routeLockTrackCircuitIds
```

へ混在させない。

理由は解錠条件が異なるため。

本進路は通常、

```text
NotEntered
→ Occupied
→ Passed
```

という列車通過で解錠できる。

一方、過走防護区間は正常運転なら列車が通らない。

したがって、

```text
Main Route Lock
→ 列車通過に基づく解錠

Overrun Protection Lock
→ 到着後の時間鎖錠等に基づく解錠
```

として独立させる。

---

# 6. 過走防護の定義（共通＋通常用追加分）

1進路につき1つの過走防護定義を持ち、必要設備を次の2組に分ける。

- `common`：NormalでもRestrictedでも確保する設備。
- `normalAdditional`：Normalで追加確保する設備。

```text
Normal     = common ＋ normalAdditional
Restricted = common ＋ 追加の進入制御
```

今回の設計では、制限用の防護設備は通常用の部分集合とする。
別の防護経路への切替や、モードによって転てつ器を逆位置にする定義は扱わない。
本進路の `InterlockingRoute.requiredTurnouts` は別に維持する。

## 6.1 定義案

```csharp
public enum OverrunProtectionMode
{
    None,
    Normal,
    Restricted
}

public enum MovementControlRequirement
{
    None,
    RestrictedApproach
}

[Serializable]
public sealed class OverrunProtectionResources
{
    public List<string> clearTrackCircuitIds = new();
    public List<TurnoutRequirement> requiredTurnouts = new();
}

[Serializable]
public sealed class OverrunProtectionDefinition
{
    public OverrunProtectionResources common = new();
    public OverrunProtectionResources normalAdditional = new();

    // Restrictedを使える進路だけ設定する。NoneならRestricted不可。
    public MovementControlRequirement restrictedControlRequirement;

    public OverrunReleaseDefinition release = new();
}
```

進路側に追加する。

```csharp
// nullなら過走防護を要求しない。
public OverrunProtectionDefinition overrunProtection;
```

防護方式の配列、ID、priority、定義ごとのprotectionClassは設けない。
実行状態の `OverrunMode` に、選択した方式を記録する。
過走防護定義がない場合は `None` とし、空の設備リストだけで防護不要とは判断しない。

`TurnoutRequirement` は既存の型を共用する。本進路・共通防護・通常用追加分で保持理由と解錠条件を区別する。
共通と追加分に同じ設備を重複記載しない。本進路を含め、同じ転てつ器に矛盾する位置を要求する定義は拒否する。

Normalでも通常のATC制御は必要。`MovementControlRequirement.None` は「追加制御なし」を意味する。
Restrictedで省ける防護は、その追加制御と組み合わせて定義する。

## 6.2 例

```text
本進路 requiredTurnouts：P21反位

過走防護 common：22T
過走防護 normalAdditional：23T、P12定位
restrictedControlRequirement：RestrictedApproach

Normal     ：本進路 ＋ 22T ＋ 23T ＋ P12定位
Restricted ：本進路 ＋ 22T ＋ 追加の進入制御
```

---

# 7. 過走防護の選択

進路要求時に次の順で評価し、すべての条件が成立した場合だけ本進路と防護を一括予約する。

1. 本進路を確保できなければ拒否する。
2. 過走防護定義がなければ `None` として本進路を予約する。
3. `common ＋ normalAdditional` を確保できれば `Normal` を選ぶ。
4. Normalが不可でも、次の条件をすべて満たせば `Restricted` を選ぶ。
   - `common` を確保できる。
   - `restrictedControlRequirement` に追加制御が定義されている。
   - その制御を車上などで実際に強制できる。
   - 要求側が `AllowRestricted` を指定している。
5. どちらも成立しなければ拒否する。

評価途中では設備を予約しない。`common` が確保できなければ、どちらの方式も成立しない。
優先順位はNormal→Restrictedで固定し、priorityによる候補選択は行わない。

---

# 8. 進路要求API

現在:

```csharp
TryRequestRoute(
    string routeId,
    out string error
)
```

将来:

```csharp
TryRequestRoute(
    string routeId,
    RouteRequestOptions options,
    out RouteRequestResult result
)
```

を基本APIとする。

---

## 8.1 `RouteRequestOptions`

```csharp
public enum RouteRestrictionPolicy
{
    NormalOnly,
    AllowRestricted
}

public readonly struct RouteRequestOptions
{
    public RouteRestrictionPolicy RestrictionPolicy { get; init; }
}
```

PRCが指定するのはここまで。

PRCは、

```text
P22をNにする
この軌道回路を鎖錠する
YYを出す
```

とは指示しない。

安全条件は連動装置が決定・照査する。

---

## 8.2 `RouteRequestResult`

文字列だけのエラーではなく、機械判定可能な理由を返す。

```csharp
public enum RouteRequestRejectReason
{
    None,

    RouteUnavailable,
    AlreadyRequested,

    MainTrackOccupied,
    HardRouteConflict,
    TurnoutConflict,

    OverrunProtectionUnavailable,
    RestrictedControlUnavailable,

    InvalidDefinition
}
```

例:

```csharp
public readonly struct RouteRequestResult
{
    public bool Accepted { get; init; }

    public RouteRequestRejectReason RejectReason { get; init; }

    public OverrunProtectionMode SelectedOverrunMode { get; init; }

    public string BlockingRouteId { get; init; }
    public string BlockingCircuitId { get; init; }
    public string BlockingTurnoutId { get; init; }
}
```

---

# 9. `EvaluateRouteRequest`

PRCの事前計画用に、副作用を持たない

```csharp
EvaluateRouteRequest(...)
```

を追加してよい。

ただし、

```text
EvaluateRouteRequest
= 今なら成立しそう

TryRequestRoute
= 実際の安全照査と確保
```

であり、最終的な安全判断は必ず `TryRequestRoute` 内でも再実行する。

---

# 10. 競合の扱い

## 10.1 `conflictRouteIds`

既存の

```csharp
conflictRouteIds
```

は残す。

ただし意味を、

> 本進路同士が絶対に同時成立してはいけないHard Conflict

へ限定する。

Restricted等によって解決可能な「過走防護だけの競合」は `conflictRouteIds` に書かない。

---

## 10.2 動的Resource競合

進路要求時に、

```text
Main Route Resources
+
選択したモードの防護設備（Normal: 共通＋追加、Restricted: 共通）
```

をまとめて評価する。

概念上、

```csharp
RouteResourceSet
{
    TrackCircuits
    TurnoutPositions
}
```

を構築する。

### 軌道回路

同じ排他的回路を2つの進路・防護が要求する場合は競合。ただし、第10.3節の条件を満たす接続した後続進路との共同保持は、専用処理で扱う。

### 転てつ器

```text
Route A → P21 = Normal
Route B → P21 = Reverse
```

なら競合。

同じ位置を要求する場合、

```text
Route A → P21 = Normal
Route B → P21 = Normal
```

は、他に競合条件がなければ共有可能としてよい。

初期実装では専用のOwner Registryを作らず、

> Active Route + OverrunMode + 第10.3節の設備別の保持理由・共同保持関係から、現在保持すべきResourceを毎回導出する

方式でもよい。

---

## 10.3 自番線の後続進路との設備単位の共同保持（確定）

場内進路Aの過走防護と、同じ着点から先へ進む出発進路Bの本進路が重なる場合、重なる設備だけを共同で保持する。
BがAの過走防護設備をすべて含むことは要求しない。Bが使わない設備はAが引き続き保持する。
これは進行可能範囲の延長であり、Normal → Restrictedへの降格ではない。

### 受付条件

以下のすべてを満たす場合だけ共同保持を許可する。

- Aの着点とBの発点が一致し、順序付き経路と進行方向が連続している。回路IDの一致だけで判定しない。
- Aの過走防護が成立・保持中で、取消処理や過走検知による自動解錠停止、防護方式切替の途中ではない。
- 共有する転てつ器の要求位置が一致する。
- Bの設定・転換によって、Aに残る過走防護区間への防護経路や必要な転てつ器条件を損なわない。設備IDの集合の重なりだけで判断しない。
- Bの残りの本進路、およびB自身に必要な過走防護を確保できる。
- てっ査鎖錠、第三の進路との競合、Hard Conflictなど、ほかの照査条件を満たす。

過走防護用転てつ器の逆位置への変更、他番線からの出発への適用は初期実装では許可しない。
この例外で省略するのはAの過走予約とBの本進路予約の重複による拒否だけとする。
本進路同士の予約や実際の在線を無条件に共有可能にはしない。共有する着点・発点回路の扱いは、本進路の空き照査と予約範囲の定義に従う。

### 部分的に重なる例

```text
A Overrun Protection: 22T、23T、P12=定位
B Main Route:         22T、P12=定位
```

| 設備 | 保持する所有者・理由 |
| --- | --- |
| 22T | Aの過走防護 + Bの本進路 |
| 23T | Aの過走防護 |
| P12 | Aの過走防護 + Bの本進路。双方とも定位 |

Aの防護条件が全体として維持され、受付条件を満たすなら、Bが23Tを使用しなくても設定を許可する。

### 設定手順

```text
Bの要求時に、Aとの接続・共有設備の位置一致・残る防護の維持を照査
↓
Aの保持を残したまま、Bの本進路と必要な防護を一括予約
↓
Bの必要設備を転換・照査（Aの保持条件を侵さない）
↓
Bの本進路と必要な防護が成立
↓
成立した後続進路としてATCへ公開
```

Bの受付・成立だけを理由にAの保持理由を削除しない。共有設備はA・B双方の保持理由を持ち、非共有設備はAの保持を継続する。
A全体を引継ぎ済みとする `Transferred` 状態は設けない。Aの防護状態は引き続き成立・時素中などを表し、個々の設備の保持義務を別に管理する。

### 保持情報と解錠

各回路・転てつ器について、少なくとも次を判定できる情報を保持する。

- 設備ID
- 所有者の進路ID
- 保持理由（本進路、過走防護など）
- 転てつ器の場合は要求位置
- その所有者・理由に対応する解錠条件と、その成立状態

加えて、AとBが共同保持可能な後続進路関係であること、およびBが予約中か開通済みかを記録する。
専用のOwner Registryを作るか、進路状態と共同保持関係から設備別に導出するかは実装時に選択してよい。
ただし、進路状態が存在するだけで定義上の全設備を保持し続ける扱いにはせず、設備ごとの未解消の保持理由を反映する。

- Bの要求を拒否した場合：Aの保持を変更しない。
- 転換失敗やB取消：Bの保持理由はB自身の解錠条件に従って解除する。Aの保持理由は残す。
- Aの解錠条件が先に成立：Aの保持理由だけ解除する。Bの理由が残る22T・P12は保持する。
- Bの解錠条件が先に成立：Bの保持理由だけ解除する。Aの理由が残る22T・23T・P12は保持する。
- 設備のすべての保持理由が解消した場合だけ、その設備の予約・鎖錠を解放する。てっ査鎖錠など別の転換禁止条件も引き続き適用する。

Aの保持義務の解消は、Aの到着後解錠条件や取消解錠条件、または成立済みBに従う所定順序の通過に基づき判断する。
Bが使用しない23Tを、Bによって通過済みになったとは扱わない。23TはA自身の解錠条件を満たすまで保持する。
不明な場合は保持を継続し、共同保持成立をA全体の一括解錠条件にはしない。

成立済みBに従う共有区間の正当な通過は、Aの過走検知とは区別する。
この例外を非共有区間へ広げない。Bの成立前、取消後など進行が許可されない状態での進入も、共同保持を理由に正常扱いしない。

保持理由や共同保持関係から参照される進路状態は削除・別予約へ再利用しない。初期実装ではAに対する共同保持先Bは1進路に限定する。

---

# 11. 石神井公園型の条件を表現する例

これは特定駅の実設備仕様を断定するものではなく、Nakatetsu上のテストモデルとして使用する。

```text
1番場内 Normal
→ P22 = Normal

1番場内 Restricted
→ P22要求なし

2番出発 Main
→ P22 = Reverse
```

### 1番場内単独

```text
Main        OK
Normal      OK

→ Normal方式
```

### 1番場内 + 2番出発

```text
1Entry Normal:
P22=N

Departure2:
P22=R

→ Normalは競合

1Entry Restricted:
P22不要

→ Restrictedなら成立
```

したがって、

```text
1Entry = Restricted
Departure2 = Established
```

を同時成立させられる。

このように、

```text
if (routeA && routeB)
    specialCaseYY = true;
```

という駅固有の特例コードは作らない。

---

# 12. 過走防護の実行状態

```csharp
public enum OverrunProtectionPhase
{
    None,

    // 防護方式は選択されたが、転換・照査中
    Setting,

    // 防護成立
    Established,

    // 到着後、時間鎖錠中
    ReleaseTiming,

    // 防護解錠済み
    Released
}
```

---

# 13. `ProceedAllowed` の成立条件

原則:

```text
RouteLocked
AND
!CancelPending
AND
Main Routeが未進入
AND
Main用転てつ器実位置一致
AND
選択した過走防護が成立（Noneならこの条件は不要）
AND
Overrun用転てつ器実位置一致
AND
必要な回路が空き
AND
必要な運転制御を強制可能
```

のとき、

```csharp
ProceedAllowed = true;
```

とする。

---

# 14. 防護方式選択後の降格は禁止

進路要求を受け付けて防護方式を選択・予約した時点から、

```text
Normal → Restricted
```

への変更を禁止する。自動・手動を問わず、同じ進路予約の途中では降格しない。

この禁止は、転換待ち、進行許可前、進行許可後、列車進入後、取消処理中のいずれにも適用する。
`ProceedAllowed`、`PathEstablished`、進行許可の履歴など、他の状態を条件にしない。
RestrictedからNormalへ昇格した後も同じルールを適用する。

別進路が現在のNormal防護と競合する場合は、その別進路を拒否し、PRC側で待機・再要求する。
その要求を通すために、現在の防護をRestrictedへ変更してはならない。

初回要求時の候補評価は別に扱う。

```text
Normalを確保可能か評価
↓ 不可
利用条件を満たすRestrictedを評価
↓ 可能
Restrictedを選択して進路要求を受け付ける
```

これは、確保済み防護方式の降格には該当しない。

現在のNormal予約を取り直す必要がある場合は、

```text
取消
↓
必要な鎖錠・時素を維持
↓
本進路・過走防護を含む解錠条件成立
↓
元の進路予約を終了
↓
新しい進路要求として防護方式を再選択
```

の順に行う。再要求時も第7節の選択規則に従い、Restrictedの採用条件を省略しない。

降格禁止に進行許可履歴を使わないため、本仕様では `AuthorityIssued` を追加しない。
Restricted → Normalへの昇格は、第15節の条件で許可する。

---

# 15. RestrictedからNormalへの自動昇格

共通設備とRestrictedの追加制御を維持したまま、通常用追加設備を確保する。

```text
normalAdditionalを一括予約
↓
追加転てつ器を転換・実位置照査
↓
common ＋ normalAdditionalの成立を確認
↓
OverrunMode = Normal
↓
ATCへ条件改善を通知
```

共通設備は解放しない。Restrictedにしか存在しない設備もないため、旧設備の差し替えは不要。
追加設備の予約・転換中は `NormalUpgradePending` を保持し、追加設備も競合判定に含める。
同じ進路のNormalへの昇格を完了または安全に中止するまで、追加設備の予約を維持する。
完了までは `OverrunMode = Restricted` と追加制御を維持する。
失敗時は共通防護を維持し、昇格用の追加予約だけを安全に取り消す。
取消・解錠処理中は新たに昇格を開始しない。

---

# 16. 過走防護時間鎖錠

## 16.1 定義

```csharp
public enum OverrunReleaseMode
{
    WithRouteRelease,
    TimedAfterArrival
}

[Serializable]
public sealed class OverrunReleaseDefinition
{
    public OverrunReleaseMode mode;

    // TimedAfterArrival時
    public string triggerTrackCircuitId;

    [Min(0f)]
    public float releaseSeconds;
}
```

---

## 16.2 時素開始条件

時素は進路要求時から開始しない。

次の条件で開始する。

```text
OverrunProtectionPhase = Established
AND
当該進路が実際に進入済み
AND
releaseTriggerTrackCircuit が
この進路の通過状態として
NotEntered → Occupied
```

となった時点。

これにより、最初からホームに別列車が在線しているだけでタイマーが開始されることを防ぐ。

---

## 16.3 時素中

```text
OverrunProtectionPhase = ReleaseTiming
```

中も、

- 過走防護用転てつ器
- 過走防護用回路Reservation
- 他進路との競合

を維持する。

---

## 16.4 時素満了

次の条件を満たした場合にのみ解錠する。

```text
releaseSeconds満了
AND
過走防護対象回路が安全
AND
設備状態に異常なし
```

その後、

```text
OverrunProtectionPhase = Released
```

とする。

---

## 16.5 過走を実際に検知した場合

時素中に過走防護区間が在線した場合、

```text
時間満了だから自動解錠
```

してはならない。

原則、

```text
Overrun Lock保持
Automatic Release停止
```

とする。

必要なら後から異常時解錠・手動復旧仕様を追加する。

---

# 17. Main Route LockとOverrun Lockは独立する

到着列車について、

```text
進路後方
→ 列車通過に伴って解錠

過走防護
→ まだ時間鎖錠
```

を可能にする。

概念図:

```text
列車進入
   │
   ├──────── Main Route Lock
   │              ↓
   │       通過条件で解錠
   │
   └──────── Overrun Protection
                  ↓
           到着時素開始
                  ↓
            時素満了
                  ↓
                解錠
```

これにより、ホーム停車後も一定時間は駅先の転てつ器を過走防護として保持し、その後ほかの進路へ利用可能にできる。

---

# 18. 過走防護解錠後の自動引上げ

過走防護時素満了時に、

```text
YY → Y
```

のような現示変更命令を直接出してはならない。

処理は、

```text
他進路のOverrun Protection解錠
        ↓
Resourceが空く
        ↓
現在Restrictedの進路を再評価
        ↓
Normal方式を取得可能？
        ↓ YES
normalAdditionalを取得（commonは保持）
        ↓
実位置・安全条件照査
        ↓
OverrunMode:
Restricted → Normal
        ↓
信号 / ATC出力を再計算
```

とする。

つまり、

> 現示引上げは時素満了そのものではなく、安全条件再評価の結果

として発生する。

---

## 18.1 引上げ対象

基本的には、

```text
ProceedAllowed == true
AND
まだ本進路へ列車が進入していない
```

進路を対象とする。

既に場内信号を通過した後の地上信号現示を引き上げる必要はない。

ATCでは前方条件改善が運転情報更新に意味を持つ場合があるため、地上信号とは別に扱う。

---

# 19. 信号現示との関係

Interlocking内部で、

```text
SignalAspect.Restricted
```

を安全ロジックの本体にしない。

連動装置は、

```text
OverrunMode
MovementControlRequirement
ProceedAllowed
PathEstablished
```

を出力する。

Signal Controllerが、

```text
RestrictedApproach
→ YY

Normal
+ 次信号R
→ Y
```

のように現示へ変換する。

これにより、

> 信号現示そのもの

と

> その現示が必要となる安全条件

を分離する。

---

# 20. ATCとの接続

現在の `TrackAtcGroundSpecification.md` では、

```text
地上 → 車上
- 停止限界
- 進行順のATC Edge列
```

のみを送る仕様になっている。

Restricted方式が、

> 通常より厳しい動的な進入制御

を必要とする場合、現在の通信仕様だけでは安全を保証できない。

したがってRestricted方式を有効化する前に、ATCへ追加の運転制御条件を伝えられるよう仕様を拡張する。

例:

```csharp
public enum AtcMovementControlRequirement
{
    None,
    RestrictedApproach
}
```

地上ATC出力に、

```text
StopLimit
RouteEdges
MovementControlRequirement
```

を含める。

実際の速度パターン・制動曲線は車上側で生成する。

---

## 20.1 Restrictedを先に有効化しない

次の状態は禁止。

```text
Interlocking:
「Restrictedなので安全」

ATC:
「Restrictedという情報を知らない」
```

したがって実装順としては、

1. Normal Overrun Protection
2. 時間鎖錠
3. ATC/信号のRestricted強制機能
4. Restricted Overrun Protection

とする。

---

# 21. ATCへ渡す連動状態

現在の `TrackAtcRouteInput`:

```csharp
public bool ProceedAllowed;
public bool CancelPending;
public bool RouteLocked;
```

へ少なくとも以下を追加する。

```csharp
public bool PathEstablished;

public OverrunProtectionMode OverrunMode;

public MovementControlRequirement
    MovementControlRequirement;
```

意味:

```text
ProceedAllowed
→ 新しい列車が進路入口を越えてよい

PathEstablished
→ 現在の確保済み経路を進路内から辿れる

CancelPending
→ この進路に基づく進行許可を停止

RouteLocked
→ 解錠条件上、進路鎖錠を保持中
```

`RouteLocked` 単独から「経路が開通している」と判断しない。

これは現行ATC仕様書の方針とも一致する。

---

# 22. PRCとの責務分担

## PRC

担当:

- 列車追跡情報・ダイヤから進路要求時機を決定
- 進入順序・出発順序を決定
- 番線使用順序を管理
- Normalのみで要求するか、Restrictedも許容するか決定
- 拒否された進路を待機・再要求
- 運転整理

---

## Interlocking

担当:

- 本進路の安全照査
- Hard Conflict
- 軌道回路Reservation
- 転てつ器競合
- てっ査鎖錠
- 過走防護方式選択
- 過走防護時間鎖錠
- 防護方式昇格
- 進路・防護の解錠
- 最終的な安全保証

---

PRCは、

```text
どの列車を先に通すか
```

を決める。

Interlockingは、

```text
その要求をどう安全に成立させるか
```

を決める。

---

# 23. PRC要求処理

将来のPRC処理は概念的に、

```text
CTC列車追跡
      ↓
ダイヤ照合
      ↓
場内接近点 / 出発制御時機
      ↓
進路候補
      ↓
進入・出発順序照査
      ↓
EvaluateRouteRequest
      ↓
TryRequestRoute
      ↓
Requested / Setting / ProceedAllowedを監視
```

とする。

PRCは `TryRequestRoute()` が失敗しても異常終了せず、

```text
Waiting
```

として再評価する。

---

# 24. `routeLockTrackCircuitIds` の将来整理

現在の `routeLockTrackCircuitIds` は、

- 進路要求時の空き確認
- Active Route間のReservation競合
- 通過状態
- 進路解錠

を兼ねている。

将来、順次解錠を実装する際は、

```csharp
clearTrackCircuitIds
```

と、

```csharp
routeLockSections
```

へ分離することを推奨する。

例:

```csharp
[Serializable]
public sealed class RouteLockSectionDefinition
{
    public string sectionId;

    public List<string> trackCircuitIds = new();

    public List<TurnoutRequirement> lockedTurnouts = new();
}
```

これにより、

```text
入口側転てつ器
→ 後端通過後に先に解錠

ホーム先の過走防護
→ 到着後の時素で解錠
```

のような異なる解錠時機を表現できる。

本仕様の過走防護実装を先に行い、順次解錠は別段階でもよい。

---

# 25. 現行 `NtLineInterlocking.asset` との関係

現在の場内進路:

```text
21T-1RT
21T-2RT
```

は、両方とも入口の21Tおよび21号転てつ器を使用するため、本進路同士が競合する。

これはHard Conflictであり、

```text
Restricted方式だから同時設定可能
```

にはしない。

一方、

```text
21T-1RT
+
2RT-22T
```

のような

> 場内 + 他番線出発

は現在のAssetでは本進路Resourceが直接重ならない。

したがって、過走防護方式を追加したテストにはこのような組合せを利用できる。

---

# 26. 必須テスト

## 26.1 基本互換

### Normalのみ（追加制御なし）

```text
本進路空き
Normal防護空き
→ Accepted
→ OverrunMode = Normal
→ PathEstablished
→ ProceedAllowed
```

---

## 26.1a 共通と通常用追加分

- NormalはcommonとnormalAdditionalの両方を予約する。
- Restrictedはcommonだけを予約し、定義された追加制御を要求する。
- commonが競合する場合、どちらの方式も拒否する。
- normalAdditionalだけが競合する場合、Restrictedの条件を満たせば受け付ける。
- 追加制御未定義・強制不可ならRestrictedを選ばない。
- 共通と追加分の重複、転てつ器の要求位置矛盾は定義エラーにする。
- 過走防護定義なしはNone。共通設備が空でも、定義ありなら第7節の条件を適用する。

---

## 26.2 Normal競合・Restricted成立

```text
Main = OK
Normal = Resource Conflict
Restricted = OK
AllowRestricted = true

→ Restricted成立
```

---

## 26.3 Restricted禁止

```text
Normal = NG
Restricted = OK
NormalOnly

→ Reject
```

---

## 26.4 全防護不可

```text
Normal = NG
Restricted = NG

→ Reject:
OverrunProtectionUnavailable
```

---

## 26.5 Hard Conflict

```text
Route A conflictRouteIds contains B

→ Restricted方式があっても
A+Bは成立不可
```

---

## 26.6 防護方式成立前

転てつ器が転換途中なら、

```text
PathEstablished = false
ProceedAllowed = false
```

---

## 26.7 防護方式選択後の降格禁止

```text
Normalを選択して進路要求を受付・予約
↓
競合する別進路要求
↓
Normalを維持し、Restrictedへ変更しない
↓
別進路をReject/Waiting
```

以下の各状態で同じ結果になることを確認する。

- 転換待ち（Setting）
- 進行許可前
- 進行許可後
- 列車進入後
- 取消処理中で予約を保持している間
- RestrictedからNormalへ昇格した後

初回要求時にNormalが確保できずRestrictedを選ぶことは許可する。
取消・必要な解錠・元の予約終了を経た再要求では、防護方式を新たに選択する。

---

## 26.8 RestrictedからNormalへの昇格

```text
Route A Restricted
Route B Overrun LockがNormal資源を使用中
↓
Route Bの時素満了
↓
Resource解放
↓
Route AがNormalを取得
↓
防護方式をNormalへ変更
```

---

## 26.9 昇格時の原子性

```text
Restrictedを先に解放
↓
Normal取得
```

は禁止。

共通設備と追加制御を維持したまま通常用追加設備を予約し、実位置照査後にNormalへ変更する。共通設備を解放しない。

---

## 26.10 時素開始

進路設定だけでは時素を開始しない。

到着トリガ回路が、この進路について

```text
NotEntered → Occupied
```

になった時だけ開始。

---

## 26.11 時素中

時素中に競合する進路を要求しても、

```text
Overrun Protection Resource
```

はまだ使用中としてRejectする。

---

## 26.12 時素満了

```text
Timer = 0
Protection circuits = clear

→ Overrun Released
```

---

## 26.13 過走検知

時素中に過走防護回路が在線した場合、

```text
Timer満了
```

だけでは解錠しない。

---

## 26.14 Main / Overrun別解錠

```text
Main Route = Released
Overrun = ReleaseTiming
```

という状態を許可する。

---

## 26.15 取消

進入前取消:

```text
ProceedAllowed = false
Approach Lock条件を処理
必要な鎖錠解除条件成立後にOverrunも解放
```

進入後取消:

```text
ProceedAllowed = false
Main / Overrun Lockを即時解放しない
```

---

## 26.16 自番線の後続進路との共同保持

- Aが22T・23T・P12定位、Bが22T・P12定位を要求する例で、接続・防護条件を満たせばBを受け付ける。
- Bの予約・成立後も23TはAが保持し、第三の競合進路を拒否する。
- 22T・P12はAとB双方の保持理由を持ち、一方だけの解錠では解放しない。
- Aの保持理由が先に解消すると23Tはほかの保持理由がなければ解放し、22T・P12はBが保持する。
- Bの保持理由が先に解消しても、Aの理由が残る22T・23T・P12を保持する。
- BがAの防護をすべて含む場合も同じ規則で処理する。
- 要求位置が逆、接続方向が不一致、残る防護経路を損なう、他番線からの出発である場合は共同保持を拒否する。
- Bの予約・転換失敗、成立前後の取消によってAの保持を失わない。
- 正当なBの通過をAの共有区間での過走異常として扱わない。非共有区間への進入はこの例外に含めない。
- Bが使わない23TをBの通過によって解錠しない。
- 保持理由・共同保持関係が残る間、参照中の進路状態を削除・再利用しない。
- 設備が無予約になる瞬間を作らず、Normal → Restrictedへの降格も行わない。

---

# 27. 実装変更箇所

## `TrackInterlockingDefinition.cs`

追加:

- `OverrunProtectionDefinition`
- `OverrunProtectionResources`
- `OverrunReleaseDefinition`
- `OverrunProtectionMode`
- `MovementControlRequirement`
- `InterlockingRoute.overrunProtection`

---

## `TrackInterlockingContext.cs`

追加:

- `PathEstablished`
- `OverrunMode`
- `NormalUpgradePending`（昇格準備中の追加設備予約）
- `OverrunProtectionPhase`
- `OverrunReleaseRemainingSeconds`
- 第10.3節の設備別の所有者・保持理由・解錠条件と、後続進路との共同保持関係

---

## `TrackInterlockingLogic.cs`

既存処理を以下へ整理する。

```text
TryRequestRoute
├─ ValidateMainRoute
├─ SelectOverrunMode
├─ BuildResourceSet
├─ CheckHardConflicts
├─ CheckResourceConflicts（後続進路との共同保持条件を含む）
└─ CreateRouteState / ReserveSharedResources
```

更新処理:

```text
Calculate
├─ UpdateTrackCircuitState
├─ UpdateApproachLockState
├─ UpdateRouteLockState
├─ UpdateOverrunProtectionState
├─ UpdateSharedResourceHolds（設備別の保持理由を更新し、すべて解消した設備だけ解放）
├─ UpdatePathEstablished
├─ UpdateProceedAllowed
├─ TryUpgradeOverrunMode
└─ DeleteReleasedRouteStates
```

---

## `TrackInterlockingController.cs`

`ApplyOutput()` では、

```text
Main Route requiredTurnouts
+
共通防護のrequiredTurnouts
＋ Normal選択中または昇格準備中の通常用追加requiredTurnouts
```

のうち、保持中・転換が許可される設備を転換対象とする。

昇格準備中も通常用追加設備を転換対象・保持対象に含める。

---

## `TrackAtcContext.cs`

`TrackAtcRouteInput`へ追加:

```csharp
public bool PathEstablished;
public OverrunProtectionMode OverrunMode;
public MovementControlRequirement MovementControlRequirement;
```

---

## `TrackAtcController.cs`

Interlockingから上記状態を収集する。

---

## `TrackAtcGroundSpecification.md`

以下を追記する。

- `ProceedAllowed` と `PathEstablished` の違い
- 選択中のOverrunMode
- Restricted方式利用時の動的運転制御条件
- 車上へその条件を伝達する方法

---

# 28. 推奨実装順

### Phase 1: 状態分離

- `PathEstablished`
- 既存ATCとの契約整理

### Phase 2: Normal Overrun Protection

- 防護方式定義
- Main + Overrun一括競合判定
- 過走防護用転てつ器転換
- `PathEstablished`

### Phase 3: 過走防護時間鎖錠

- Arrival Trigger
- Release Timer
- Main Routeとの独立解錠
- 自番線の後続進路との部分的な共同保持と、設備別の解錠・取消処理

### Phase 4: 防護方式自動昇格

- Restricted → Normal再評価
- 共通設備を保持したまま通常用追加設備を予約・照査
- 現示/ATC条件再計算

### Phase 5: Restricted運転制御

- Signal側のYY等
- またはATC側Restricted Approach Control
- 車上への強制手段

### Phase 6: PRC連携

- `RouteRequestOptions`
- `RouteRequestResult`
- `EvaluateRouteRequest`
- Pending/Waiting
- 進入・出発順序

### Phase 7: 順次解錠

- `clearTrackCircuitIds`
- `routeLockSections`
- 通過順序照査
- 区分単位解錠

---

# 29. 今回採用しない設計

以下は採用しない。

### 信号機・転てつ器の相互参照

```text
Turnout.lockedSignals
Signal.lockedTurnouts
```

のような設備同士の蜘蛛の巣状依存関係。

---

### YY等をPRCが直接指示

```text
PRC → SignalAspect.YY
```

は禁止。

PRCは運転上の希望のみ指定し、安全条件はInterlockingが決定する。

---

### `conflictRouteIds` に条件付き競合を全部列挙

```text
1Entry + 2Departure
→ 特例でYY
```

のような方式は採用しない。

選択した防護設備の競合によって自然に導出する。

---

### `RouteLocked == PathEstablished`

この読み替えは行わない。

---

### 防護方式選択後のRestrictedへの降格

Normalを選択・予約した後は、他の状態にかかわらずRestrictedへの変更を禁止する。初回要求時の候補選択とは区別する。

---

### 車上へ伝わらないRestricted防護

列車側が制限を実際に強制できない状態ではRestricted方式を有効にしない。

---

# 30. 最終的な責務構造

```text
                        Timetable
                            │
                            ▼
                      CTC / PRC
                  「どの進路をいつ取る」
                            │
                            ▼
                 TrackInterlocking
          ┌──────────────────────────┐
          │ Main Route               │
          │ Overrun Protection       │
          │ Approach Lock            │
          │ Route Lock               │
          │ Track Circuit Lock       │
          │ Conflict / Resource Check│
          └─────────────┬────────────┘
                        │
              ┌─────────┴─────────┐
              ▼                   ▼
 TrackConnectionController     Ground ATC
      転てつ器実状態         経路・停止限界
              │                   │
              │                   ▼
              │               Onboard ATC
              │
              └──── 安全条件 ────┘
```

連動装置が扱う中心概念は、

> **「本進路 + その進路に現在適用している過走防護方式」**

とする。

進路要求時には両者を1セットとして安全に成立可能か判定する。

本進路と過走防護は、成立時には一体として照査するが、解錠時には別々の条件で管理する。

---

# 31. 要約

Nakatetsuの現行連動設計は大幅に作り直さない。

追加する中心仕様は次のとおり。

1. `ProceedAllowed` は新規進入許可として維持する。
2. `PathEstablished` を追加し、確保済み経路と進入許可を分離する。
3. 過走防護を本進路と分け、共通設備と通常用追加設備で定義する。
4. 進路要求時にNormal → Restrictedの順で成立可能性を評価する。
5. Main + Overrunを1セットでResource照査・予約する。
6. `conflictRouteIds` は絶対競合だけに使用する。
7. 過走防護は本進路とは別に解錠する。
8. 到着後は過走防護時間鎖錠を開始し、時素満了後に安全確認して解錠する。
9. 時素解錠によって上位防護方式が利用可能になった場合、Resourceを先に確保してからRestricted → Normalへ自動昇格する。
10. Normalを選択・予約した後のRestrictedへの降格は、進行許可の有無など他の状態にかかわらず禁止する。
11. 現示は直接変更せず、現在成立している防護方式から再計算する。
12. Restricted方式は、その制限を信号またはATCで実際に強制できる場合だけ使用する。
13. PRCは運転順序と要求時機を決め、安全成立の最終判断はInterlockingが行う。
14. 将来的に進路空き照査範囲と解錠区分を分離し、順次解錠へ拡張する。
15. 自番線の接続する後続進路と重なる設備は共同で保持し、重ならない過走防護設備は元の進路が保持する。各設備はすべての保持理由が解消したときだけ解放し、失敗・取消時も元の防護を失わない。

この構造により、通常の場内進路、場内同士の同時設定、場内と他番線出発の条件付き同時設定、到着後の過走防護解錠、解錠後の現示引上げを、駅固有の特例処理を追加せず同じ連動モデルで扱える。
