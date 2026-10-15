# ダイヤデータのORM選定と実装方針

> 2026年10月15日：CTCサーバー再構築に向け、以下のEF Core/SQLite実装とサーバープロジェクトを削除した。この文書とER図は設計資料として残す。新サーバーのDBアクセス方式・スキーマ適用手順は再選定する。

2026年10月12日決定。ダイヤの永続化には **Entity Framework Core 10とSQLite** を採用する。ORMは.NET 10の運行管理サーバーだけに導入し、PRCには読み込み・検証済みのダイヤをメモリ上の値として渡す。現在のローカルサーバー1台での運用を対象とする。

基準となるER図は [TimetableDatabaseDesign.drawio](TimetableDatabaseDesign.drawio)。既存の [CTC実装計画](CTCImplementationPlan.md)、[連動管理部の設計](../Track/Interlocking/TrackInterlockingAggregationDesign.md)、[ディレクトリ構成](../Architecture/DirectoryStructure.md)に合わせ、設備の実状態と安全判断はUnity側、列車追跡・ダイヤ照合・自動進路要求はサーバー側のPRCが担当する。

## ORMとDBの選定理由

| 項目 | 採用方針 | 理由 |
| --- | --- | --- |
| ORM | EF Core 10 | サーバーの`net10.0`と対応し、関連・制約・更新・スキーマ変更を同じモデルで管理できる |
| DB | SQLite | ローカルの単一サーバーで、ダイヤ編集・読込みを扱う初期構成に適する。別のDBサーバーを常駐させずに運用できる |
| モデル定義 | Code FirstとFluent API | ER図の複合キー、運用内の列車順序、NULL可否、削除動作を明示する |
| スキーマ変更 | EF Core Migrations | 生成した変更をレビューし、既存データを維持して更新する |
| 計算用データ | ORMから独立した読み取り専用のダイヤ | PRCの更新処理がDBや変更追跡に依存しない |

EF Core 10は.NET 10を必要とするLTS版であり、現在のサーバー構成に対応する。[MicrosoftのEF Core 10仕様](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)

DapperもC#とSQLiteで利用できるが、SQLの実行と結果のマッピングを中心とするライブラリである。このER図では地点・使用進路の並び順、複合外部キー、引継ぎ列車、ダイヤ全体の保存が必要になるため、それらとスキーマ変更を一体で管理するEF Coreを選ぶ。初期実装にDapperを併用せず、性能上の必要が実測された場合に専用の読み取り処理を検討する。[Dapper公式仕様](https://github.com/DapperLib/Dapper)

SQLiteへの書込みは同時に1つとなる。ダイヤの編集・読込みを中心とし、設備状態を毎tick書き込まない今回の使い方には適する。複数のサーバーインスタンスや大量の同時編集が必要になった段階ではPostgreSQLへの移行を判断する。SQLiteファイルはサーバーのローカルディスクに置く。[SQLiteの用途と同時書込みの制約](https://www.sqlite.org/whentouse.html)

## 永続化する情報と実行時の情報

ER図の10テーブルをダイヤ・運用と参照マスターとして保存する。`Stations`はダイヤ上の駅、`TrackCircuits`と`Routes`はUnityの設備を参照するIDのカタログとする。線形、進路構成、鎖錠条件などの設備定義をダイヤDBへ複製して編集する機能は作らない。

`UnitySnapshotStore`が保持する最新状態、在線、鎖錠、通信鮮度、PRCの追跡状態と要求待ちは、初期実装ではメモリ上で扱う。`(TimetableId, TrainNumber)`はダイヤ内の列車計画のキー、`(TimetableId, OperationNumber)`は順に担当する列車をまとめる運用のキー、Unityの`TrainId`は実際の編成のIDとして分ける。編成と運用・現在の列車計画の割当てはセッションの実行時データで管理する。運行記録の永続化が必要になった場合は、ダイヤ定義とは別の保存処理を追加する。

DBの設備カタログは、初期にはNtLineの明示した定義データから登録する。後にUnityの設備定義一覧を取得する通信へ接続する。取得不能の設備が最新スナップショットから消えたことを、設備削除としてDBに反映しない。カタログに存在することと、現在利用可能であることは別に判定する。

## ER図の具体化

図に書かれたキーとNULL可否を基本とし、型が未指定の項目は次のように確定する。列車計画は同じダイヤ内の列番で識別する方針に変更し、テーブルを`TimetableTrains`、C#の永続化用クラスを`TimetableTrainEntity`とする。他のテーブル名はER図と同じ複数形とし、永続化用クラス名は単数形とする。

| テーブル | キーと主な型 | 関連と制約 |
| --- | --- | --- |
| `Timetables` | `TimetableId: long`、`DisplayName: string` | ダイヤの保存単位。追加項目として`Revision: long`を持つ |
| `TimetableTrains` | `PK(TimetableId: long, TrainNumber: string)`、`OperationNumber: string`、`SequenceIndex: int`、`TrainTypeId: string` | `FK(TimetableId, OperationNumber)`で所属運用を参照する。`UQ(TimetableId, OperationNumber, SequenceIndex)`。地点一覧を所有する |
| `TimetableOperations` | `PK(TimetableId: long, OperationNumber: string)` | ダイヤ内の運用。運用内の列車一覧を所有する |
| `TrainTypes` | `TrainTypeId: string`、`DisplayName: string` | 種別のマスター |
| `TimetablePoints` | `TimetablePointId: long`、`TimetableId: long`、`TrainNumber: string`、`SequenceIndex: int`、`StationId: string?`、`TrackCircuitId: string`、`Handling: enum`、`ArrivalSeconds/DepartureSeconds: int?` | `FK(TimetableId, TrainNumber)`、`UQ(TimetableId, TrainNumber, SequenceIndex)`。駅参照は任意、回路参照は必須 |
| `TimetablePointRoutes` | `PK(TimetablePointId: long, SequenceIndex: int)`、`ControlStationId: string`、`RouteId: string` | 地点を参照し、`FK(ControlStationId, RouteId)`で進路を参照する |
| `TrackCircuits` | `TrackCircuitId: string` | 回路IDの参照マスター。初期仕様では対象設備全体で一意 |
| `StationRunningTimes` | `PK(TrainTypeId: string, FromStationId: string, ToStationId: string)`、`RunningSeconds: int` | 種別と発着駅を参照する。逆方向は別レコード |
| `Stations` | `StationId: string`、`DisplayName: string` | ダイヤ上の駅のマスター |
| `Routes` | `PK(ControlStationId: string, RouteId: string)` | 連動制御駅内で進路を識別する |

ダイヤ・地点の内部IDはDB生成の整数とする。列車計画に別の内部IDは持たせず、保存前に指定する`TrainNumber`と所属ダイヤの`TimetableId`を複合主キーにする。同じダイヤ内の同じ列番は同じ列車計画を表し、別ダイヤでは同じ列番を使用できる。運用番号も同じダイヤ内で一意とし、別ダイヤでは同じ番号を使用できる。駅・回路・進路・種別は既存の文字列IDを維持し、列番・運用番号を含む文字列キーは`BINARY`照合で大文字小文字を区別する。空文字と空白だけのキーは拒否し、NULLを空文字に置き換えない。

次の点は図の曖昧な箇所を補う実装上の決定とする。

- **回路と地点は1対多。** 図の線は1対1にも見えるが、同じ回路を複数の列車計画・地点から参照できる必要がある。`TimetablePoints.TrackCircuitId`には一意制約を付けない。
- **進路IDは制御駅との組で扱う。** 同名の`RouteId`を別の制御駅に持てる。進路要求への変換では`ControlStationId`を既存の`InterlockingRouteRequest.StationId`へ渡す。これはダイヤ上の駅を示す`TimetablePoints.StationId`とは別の意味であり、両者を自動的に同一視しない。
- **列車の引継ぎは運用内の並び順で表す。** `TimetableTrains`に必須の`OperationNumber`と`SequenceIndex`を持たせ、所属運用と順序を直接保存する。運用内の`SequenceIndex`は0始まりの連続した順序とし、次の要素の列車が引継ぎ先となる。各列車計画は同じダイヤの1つの運用に必ず所属する。次列車の列や運用と列車を結ぶ中間テーブルは持たない。列車0件の運用と運用0件のダイヤは保存できる。
- **ダイヤにRevisionを追加する。** 運用・地点・使用進路を含む編集でダイヤの`Revision`を必ず増やし、編集開始時のRevisionを更新条件に使う。これは通信の`SchemaVersion`とは別の値である。

複合主キーと複合外部キーはFluent APIで明示する。プロパティの順序は参照先と参照元でそろえる。[EF Coreの外部キーと参照先キー](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/foreign-and-principal-keys)

ダイヤDB・CTC要求・地上ATCの連動進路参照には、このER図と同じ駅ID・駅内進路IDの複合キーを採用する。要求APIは駅IDで振り分けてから進路IDを検索し、異なる駅の同名進路を許容する。地上ATCの生成元と生成済み定義は`interlockingStationId`と`interlockingRouteId`を持ち、実行時状態もその組で参照する。回路IDは現在のUnity側で単独のキーとして扱われるため、回路の駅別同名対応は別途通信・設備定義を含めて設計する。

## 時刻と地点の検証

`ArrivalSeconds`と`DepartureSeconds`は運行日0時からの整数秒とし、24時以降を認める。例えば25時10分は`90600`秒となる。時刻を`DateTime`や`TimeOnly`で保存せず、システムの現在時刻やタイムゾーンで補正しない。PRCはUnityから受けた`WorldTimeSeconds`と同じ運行日の基準で照合し、24時間で剰余を取らない。

`Handling`は図どおり`Stop = 1`、`Pass = 2`を整数列に保存する。以下を初期の時刻表現として採用する。

| 地点 | ArrivalSeconds | DepartureSeconds |
| --- | --- | --- |
| 始発の停車地点 | NULL可 | 発時刻 |
| 中間の停車地点 | 着時刻 | 発時刻 |
| 終着の停車地点 | 着時刻 | NULL可 |
| 通過地点 | NULL | 通過時刻 |

編集中は未入力時刻をNULLで保存できる。自動運行に使う前には、必要な時刻、地点の順序、進路参照を検証し、不完全なダイヤを有効化しない。駅以外の地点は`StationId = NULL`で表す。

DBのCHECKでは運用内列車・地点・使用進路の`SequenceIndex >= 0`、`Handling IN (1, 2)`、時刻がNULLまたは0以上、着発が両方ある場合は`ArrivalSeconds <= DepartureSeconds`、`RunningSeconds > 0`を保証する。連番は0始まりとし、重複はDBのキーで防ぐ。運用内列車の欠番はStoreで拒否し、地点・使用進路の欠番と地点間の時刻の逆転は検証Logicで防ぐ。終端地点の使用進路は0件とし、各地点の進路列はその地点から次地点までの使用順を表す。

運用内で次の列車の始発時刻は前の列車の終着時刻以降とし、引継ぎ地点の整合も検証する。回送移動が必要なら回送の列車計画を運用の間に置く。地点間の進路列が配線上で利用できることは設備定義との照合で確認し、外部キーの存在だけで接続が正しいと判断しない。初期のNtLineでは、設備定義に対応する明示した経路で検証する。

`StationRunningTimes`は種別と発着駅だけで決まる初期の標準所要時間とする。未登録の値を0秒に補わない。経路や番線によって所要時間を変える必要が生じた場合はキーを拡張する。現状の種別enumと`TrainTypes`を二重に正本として扱わず、既存の文字列`TrainTypeId`に合わせてマスターを正本とする。

## サーバーでの責務と配置

既存の`TrainRunDefinition`と`TimetablePoint`は計算用の値モデルとして拡張する。EF用のEntityと設定クラスは別に作り、EF属性、追跡中のEntity、ナビゲーション参照をPRCのContextや共有通信型へ渡さない。Unityと共有する`Shared/Contracts`には必要な通信DTOだけを置き、EF Coreへの参照を追加しない。

初期実装は既存のサーバープロジェクト内で次の責務に分ける。ファイル数が少ないうちは必要以上にクラスやAssemblyを分割しない。

| 場所 | 責務 |
| --- | --- |
| `Timetable/Scripts` | 既存の列車計画・地点などの計算用モデル |
| `Timetable/Persistence/Interfaces` | ダイヤ単位の読込み・保存を提供する`ITimetableStore` |
| `Timetable/Persistence/Scripts` | `TimetableDbContext`、`TimetableStore`、`TimetableMasterStore`、取得・保存用DTO |
| `Timetable/Persistence/Entities/Scripts` | 10テーブルに対応するEntity |
| `Timetable/Persistence/Mapping/Scripts` | `IEntityTypeConfiguration<T>`によるキー・関連・制約の設定 |
| `Timetable/Persistence/Migrations` | EFの生成するスキーマ変更とModelSnapshot |
| `Timetable/Compilation/Scripts` | 順序・時刻・引継ぎ・設備参照の検証と計算用ダイヤの構築 |
| `Timetable/Import/Scripts` | NtLineの定義・試験ダイヤなどの入力と一括保存 |

`TimetableStore`の公開単位はテーブル別CRUDではなく、ダイヤ内の運用と所属列車の階層とする。参照マスターの新規登録・一覧取得・削除は別の`TimetableMasterStore`で扱う。外部へ`IQueryable`を公開する汎用Repositoryや、EFの上に重ねる汎用Unit of Workは追加しない。データ入力の窓口が増えても、同じダイヤ検証と保存処理を通す。

ダイヤEntityと`TimetableData`は運用一覧`Operations`を持つ。運用Entityの`Trains`は`ICollection<TimetableTrainEntity>`とし、列車本体を直接保持する。運用DTOの`TimetableOperationData`も`OperationNumber`と列車一覧`Trains`を持ち、各`TimetableTrainData`に`SequenceIndex`・`TrainNumber`・`TrainTypeId`・地点一覧を含める。各地点は使用進路一覧を保持し、ダイヤから運用・列車・地点・使用進路までを1つの階層で表す。列車Entityは`OperationNumber`で所属運用を参照し、地点Entityは列車への参照に`TimetableId`と`TrainNumber`を保持する。既存の計算用`TrainRunDefinition`のクラス名は変更しない。

`Program.cs`では`AddDbContextFactory<TimetableDbContext>`と両Storeを登録する。`TimetableMasterStore`もSingletonとし、Factoryから処理ごとに短命のContextを作成・破棄する。現在の`CTCController`はSingletonなので、Contextそのものを保持させない。同じContextで並列クエリを実行しない。[EF CoreのContext寿命とFactory](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/)

## 読込みとPRCへの接続

処理は「DBから読込み → 検証・順序付け → 読み取り専用ダイヤを構築 → PRCの更新境界で有効化」の順に行う。

1. 選択したダイヤのRevision・運用・所属列車・地点・使用進路と必要なマスターを読み取りトランザクション内で取得する。複数のクエリを使っても同じ時点のデータとして扱う。
2. `AsNoTracking`・`AsSplitQuery`と`Include`／`ThenInclude`で、`Operations → Trains → Points → Routes`を取得する。ダイヤ直下の`Trains`を取得する分岐は追加しない。地点・使用進路・運用内の列車をそれぞれ`SequenceIndex`で並べ、運用から列車、地点、使用進路までの階層をDTOへコピーする。遅延ロードは使わず、地点や運用ごとの追加クエリを発生させない。空の運用と、運用0件のダイヤも返す。
3. 検証Logicで完全性・運用内の順序・時刻を確認し、変更できない値のコピーを構築する。運用内の列車本体を順に参照し、隣接する列車計画の引継ぎを照合する。
4. Unityの対象セッションの設備定義と照合する。DBにある進路IDだけを根拠に自動要求を許可しない。設備定義一覧の通信が整うまでは、明示したNtLineカタログを使う。
5. 初期仕様では、シミュレーションとPRCの自動要求を停止してからダイヤを有効化する。有効なRevisionをPRCのContextへ固定し、実行中の更新処理はそのメモリ上の値だけを読む。

編集の保存は有効なダイヤを即時に差し替えない。再度の有効化時には変更後のRevisionを読み込み、検証に失敗したら既存ダイヤを維持する。切替えの更新境界で旧ダイヤの自動要求待ちを破棄し、列車割当て・追跡状態を作り直してから自動要求を再開する。送信済みで結果未確認の要求がある場合は、結果確認まで切替えを保留する。DBへの保存や切替えを理由に、既に成立した進路を自動取消しない。

`WorldTimeSeconds`は現在の通信DTOで`float`となっている。DBの計画時刻は整数秒のままとし、PRCで秒未満の精密な照合が必要になった場合は、世界時刻の通信精度を別の契約変更として扱う。

## 保存と同時編集

ダイヤ・列車計画・地点・使用進路・運用の変更を1つのトランザクションにまとめる。`CreateAsync`は取得と同じ運用階層のDTOを受け取り、各運用の列車・地点・使用進路をEntityへ変換する。各列車の`OperationNumber`には親運用の番号、`SequenceIndex`には入力の順序を設定し、運用Entityの`Trains`へ追加する。Storeは列番・運用番号が空でないこと、同一ダイヤ全体でそれぞれ重複しないこと、運用内の順序が0始まりで連続することを確認する。空の運用と運用0件のダイヤも作成できる。1回の`SaveChangesAsync`で保存して確定でき、保存後の内部IDによる引継ぎ先の対応付けは不要とする。時刻・引継ぎ地点などの業務上の検証はCompilationの工程で行う。途中で失敗したら全体を戻す。

既存ダイヤの更新と削除は次のStoreメソッドで行う。この機能追加ではスキーマ・DTO・ER図を変更せず、追加Migrationは不要とする。

| シグネチャ | 入力と戻り値 |
| --- | --- |
| `Task<long> UpdateAsync(TimetableData timetableData, CancellationToken cancellationToken = default)` | `TimetableId`のダイヤ全体を置き換える。入力の`Revision`を期待値として使い、保存後の新しいRevisionを返す |
| `Task DeleteAsync(long timetableId, long expectedRevision, CancellationToken cancellationToken = default)` | IDと期待するRevisionが一致するダイヤを子データごと削除する。戻り値なし |

`UpdateAsync`は対象IDが正数、入力Revisionが0以上であることを確認し、`CreateAsync`と共通の入力検証を行う。`checked(timetableData.Revision + 1)`で新しいRevisionを計算し、オーバーフローを拒否する。`TimetableId`と期待するRevisionを条件に、`ExecuteUpdateAsync`でダイヤの表示名とRevisionを更新する。更新件数が1件の場合だけ、同じトランザクション内で既存の運用・列車・地点・使用進路を削除し、入力の階層を再登録する。ダイヤ本体のIDは保持し、後続の保存を含めて失敗したら表示名・Revision・子データをすべて元に戻す。

`DeleteAsync`もIDが正数、期待するRevisionが0以上であることを確認する。`TimetableId`と`expectedRevision`を条件に`ExecuteDeleteAsync`を実行し、ダイヤ本体とCascade対象の運用・列車・地点・使用進路を削除する。マスターと他のダイヤは削除しない。更新・削除とも、対象が存在しない場合やRevisionが一致しない場合は`DbUpdateConcurrencyException`を投げ、自動的に上書きしない。

`Timetables.Revision`をアプリ管理の楽観的同時実行トークンにする。子テーブルだけの編集でもダイヤのRevisionを更新し、保存・削除時に期待するRevisionとの一致をDBの更新条件で確認する。HTTP 409への変換は今後の編集API層の方針であり、Store自身は例外を通知する。SQLiteにはDB生成の同時実行トークンがないため、この方式を使う。[SQLiteプロバイダーの制約](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)

`TimetableMasterStore.cs`は次の4種のマスターを新規登録・一覧取得・削除する。入出力DTOは同じディレクトリの`TimetableMasterData.cs`に定義する。

| DTO | 新規登録 | 一覧取得 | 削除のキー |
| --- | --- | --- | --- |
| `TrainTypeData(TrainTypeId, DisplayName)` | `RegisterTrainTypeAsync` | `ListTrainTypesAsync` | `DeleteTrainTypeAsync(trainTypeId)` |
| `StationData(StationId, DisplayName)` | `RegisterStationAsync` | `ListStationsAsync` | `DeleteStationAsync(stationId)` |
| `TrackCircuitData(TrackCircuitId)` | `RegisterTrackCircuitAsync` | `ListTrackCircuitsAsync` | `DeleteTrackCircuitAsync(trackCircuitId)` |
| `RouteData(ControlStationId, RouteId)` | `RegisterRouteAsync` | `ListRoutesAsync` | `DeleteRouteAsync(controlStationId, routeId)` |

登録メソッドはDTOと省略可能な`CancellationToken`を受け取り、`Task`を返す。同じキーが既に存在する場合は`DbUpdateException`で拒否し、既存値を上書きしない。DTOの`null`、各キーと表示名の空文字・空白だけの値を拒否し、前後空白の除去や大文字小文字の変換は行わない。一覧メソッドは省略可能な`CancellationToken`を受け取り、`Task<IReadOnlyList<各DTO>>`を返す。SQLiteの`BINARY`照合でIDの昇順に並べ、進路は`ControlStationId`、`RouteId`の順に並べる。

削除メソッドは主キーの文字列と省略可能な`CancellationToken`を受け取り、`Task<bool>`を返す。成功は`true`、不存在や既に並行削除された場合は`false`とする。`null`・空文字・空白だけのキーを`ArgumentException`で拒否し、前後空白の除去や大文字小文字の変換は行わない。新しいContextでキーだけのEntityを`Remove`して保存し、事前のSELECTは行わない。`DbUpdateConcurrencyException`だけを`false`へ変換し、他の`DbUpdateException`は呼び出し元へ通知する。ダイヤや所要時間から参照中のマスターは外部キーのRestrictで削除を拒否し、マスターと参照元データを保持する。成功する削除は未参照マスターが対象なので、ダイヤのRevisionは変更しない。

今回のマスターStoreはこの4種だけを対象とし、所要時間の登録、編集API、Unityとの同期は別工程とする。具体的なNtLineなどの実データ登録も別工程とし、実DBへの初期データ投入は行わない。参照中のマスターIDは変更しない。将来、所要時間など計算用ダイヤに影響するマスター値を更新する場合は、参照するダイヤのRevisionも同じトランザクションで更新し、同じRevisionの意味が変わることを防ぐ。

`UpdateAsync`は子データ全体を削除してから再登録するため、列番・運用番号や運用内順序の入替えで一意制約に一時的に衝突しない。`CreateAsync`と`UpdateAsync`は入力の`TimetablePointId`を保存せず、DBで地点IDを採番する。更新でも地点IDは再発行されるため、編集要求や実行中の追跡に使う場合はダイヤのRevisionと組で識別する。

所有関係の`Timetables → TimetableOperations → TimetableTrains → TimetablePoints → TimetablePointRoutes`をCascade削除とし、参照マスターの外部キーにはRestrictを指定する。運用を削除すると所属列車・地点・使用進路も削除する。運用の削除や列車計画の所属替えを含む編集は、`UpdateAsync`へ渡すダイヤ全体の階層で表す。マスター削除で利用中のダイヤが消える動作は作らない。

## パッケージとDB運用

サーバーに`Microsoft.EntityFrameworkCore.Sqlite`と`Microsoft.EntityFrameworkCore.Design`を追加し、Designは`PrivateAssets=all`とする。`dotnet-ef`はローカルtool manifestで固定する。導入時点の安定した10.0系パッチを選び、EFパッケージとtoolのバージョンをそろえる。

DBファイルは`Server/Nakatetsu.TrafficControl.Server/Data/TrafficControl.sqlite`を既定とし、実際のパスはサーバーのContentRoot基準で解決する。DB、`-wal`、`-shm`ファイルはGit対象外とし、Migrationsと再現可能な入力データを管理する。配布するサーバーには書込み可能な永続ディレクトリを設定する。

WALと外部キーの有効化を確認する。SQLiteのI/Oは`Async`メソッドでも同期実行されるため、DB呼出しをPRCの更新処理やそのlock内に置かない。書込みトランザクションは短くし、DBのロック待ちにはタイムアウトを設ける。[Microsoft.Data.Sqliteの非同期I/O制約](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async)

`EnsureCreated`とMigrationsを混在させない。開発環境では起動前の`dotnet ef database update`、配布環境ではバックアップと起動前の専用更新手順でスキーマを適用する。SQLiteの変更にはテーブル再構築を伴うものがあり、冪等なSQLスクリプトにも制約がある。[SQLiteのMigrations制約](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)

列番を主キーにする更新では、初回Migrationを残し、追加Migration `20261012044953_UseTrainNumbersAsKeys`で旧`TrainRuns`を`TimetableTrains`へ移す。外部キーを有効にしたまま1つのトランザクションで処理し、旧列車計画の行を参照して引継ぎ先を列番へ置き換え、地点にも所属ダイヤと列番を設定する。地点の再構築前に使用進路を一時テーブルへ退避し、既存の地点・使用進路・引継ぎの関連を保持する。同じダイヤ内に同じ列番を持つ複数の旧列車計画がある場合は、主キーの重複で更新全体を取り消して既存データを残す。重複する列車計画を自動的に統合しない。旧列車計画の数値IDを削除するため同じIDへ復元できず、`Down`は変更前に`NotSupportedException`を投げる。元のスキーマへ戻す場合は更新前のバックアップを復元する。

運用を追加する更新は過去のMigrationを維持し、`20261012052633_AddTimetableOperations`を追加する。外部キーを有効にした1つのトランザクションで、旧次列車の参照でつながる列車計画を1つの運用へ変換し、先頭の列番に`legacy:`を付けた運用番号と、0始まりの連続した順序を生成する。参照のない単独の列車計画も1列車の運用へ変換する。全列車を運用へ変換できたことをCHECKで確認し、旧参照に循環がある場合は既存行の削除前に更新全体を取り消す。列車・地点・使用進路と地点IDの採番状態を一時退避して保持し、変換後は列車計画の次列車列と自己参照を削除する。運用番号や空の運用は旧次列車方式で表現できないため、`Down`は変更前に`NotSupportedException`を投げる。元のスキーマへ戻す場合は更新前のバックアップを復元する。

運用内の順序を列車計画へ移す更新は`20261012054848_MoveOperationOrderToTimetableTrains`を追加する。旧中間テーブルの所属運用と順序を`TimetableTrains`の必須列へ移し、既存の列車・地点・使用進路と地点IDの採番状態を保持する。運用に未所属の既存列車がある場合は、列車を削除したり自動割当てしたりせず、全列車の所属を確認するCHECKで変更を拒否して既存データを残す。このMigrationの`Down`は列車の直接所属と順序を旧中間テーブルへ戻し、同じデータを保持できる。前述の2件のMigrationの`Down`不可とは別の更新として扱う。

PostgreSQLへの移行時にも計算用モデルとStoreの契約は維持する。ただし接続先の差替えだけで完了するとは扱わず、プロバイダー別のMigrations、データ移送、照合順序・制約・同時編集の試験を行う。[複数プロバイダーのMigrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/providers)

## 実装順と受入条件

| 順序 | 実装する部分 | 確認する動作 |
| --- | --- | --- |
| 1 | EF導入、10 Entity、Fluent設定、Migrations | 空のSQLiteへ10テーブルとキー・CHECK・外部キーを作成できる |
| 2 | NtLineカタログ、試験ダイヤの取込み、Store | マスター・2列車・地点・使用進路・運用を一括保存し、失敗時に部分データを残さない |
| 3 | ダイヤ検証と計算用モデルへの変換 | 連番重複・欠番、未知設備、運用内の列車重複・未知列番を拒否し、引継ぎ地点・時刻と25時以降・NULL時刻を正しく扱う |
| 4 | 編集時のRevisionと再読込み | 並行編集の古いRevisionを拒否し、子テーブルの編集も競合検出できる |
| 5 | PRCのダイヤ有効化と照合 | 更新中にDBを呼ばず、切替え時に異なるRevisionの地点や要求を混ぜない |

DBの統合試験は実際のSQLiteプロバイダーを使う。特に別制御駅の同名進路を許可し、存在しない複合進路参照、存在しない同一ダイヤの運用への所属、運用内順序の重複を拒否することを確認する。旧次列車参照からの変換では、複数の連鎖・単独列車の関連を保持し、循環による更新失敗で部分データを残さないことを確認する。中間テーブル廃止時には所属と順序を保持し、未所属列車による更新失敗で部分データを残さないことを確認する。EFのInMemoryプロバイダーだけでは、今回の外部キー・一意制約・トランザクションの受入試験を代替しない。
