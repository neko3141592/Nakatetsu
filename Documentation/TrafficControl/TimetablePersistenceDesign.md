# ダイヤ・列車・運用計画の保存構造

2026-11-03更新。Go / GORM / SQLiteの現行実装に対応する。

## 所有関係

ダイヤは列車の運行計画を持つ。運用計画は対象ダイヤを参照し、列車を同じ編成が担当する順番を持つ。ダイヤから運用定義を共有する旧方式は廃止した。

```text
Timetable ──< TimetableTrain ──< TimetablePoint ──< TimetablePointRoute
    ↑                ↑
TimetableOperationPlan ──< TimetableOperation ──< TimetableOperationTrain
```

列車・運用計画・運用はUUIDで識別する。列番は表示・検索用の値であり、主キーでも一意キーでもない。同じ列番の列車を別ダイヤに登録しても、時刻・番線・使用進路を共有しない。同じダイヤ内で列番が重複しても、DB・編集API・SVGの選択は内部IDで区別する。

ER図は `TimetableDatabaseDesign.drawio`。1ページ目はダイヤと運用計画、2ページ目は共有マスターを含む全16テーブル。`TimetableDatabaseDesign.png` は1ページ目のプレビュー。

## テーブル

| テーブル | 主キー | 内容 |
| --- | --- | --- |
| `timetables` | `id` | ダイヤ名とRevision |
| `timetable_trains` | `id`（UUID） | 所属ダイヤ、列番、種別、始発・終着駅 |
| `timetable_points` | `timetable_point_id` | 列車ID、順序、駅・軌道回路、着発時刻、通過／停車、通常／出庫／入庫区分 |
| `timetable_point_routes` | `timetable_point_id + sequence_index` | 各地点からの使用進路。制御駅ID＋進路IDでマスターを参照 |
| `timetable_operation_plans` | `id`（UUID） | 対象ダイヤID、運用計画名。同じダイヤに複数の運用計画を作成可能 |
| `timetable_operations` | `id`（UUID） | 所属する運用計画、対象ダイヤ、運用番号 |
| `timetable_operation_trains` | `operation_id + train_id` | 列車の割り当てと運用内の順序 |

旧 `timetable_train_links` / `timetable_operation_links` は廃止。`temporaryTrainNumbers` と `operationNumbers` によるダイヤの保存も廃止した。

## DB制約

- 列車は `timetable_id` により、必ず1つのダイヤに所属する。
- 地点の `train_id + sequence_index` は一意。
- 運用番号は `operation_plan_id + operation_number` の範囲で一意。
- 同じ運用内の `sequence_index` は一意。
- `operation_plan_id + train_id` の一意制約により、1つの運用計画で同じ列車を二重に割り当てない。別の運用計画では同じ列車を使える。
- 運用と計画、割り当てと運用、割り当てと列車にそれぞれ複合外部キーを張る。補助列 `timetable_id` / `operation_plan_id` により、他ダイヤの列車を紛れ込ませる操作をDBでも拒否する。
- UUID単独の主キーに加え、複合外部キーの参照先として `(id, timetable_id)` などの一意インデックスを設ける。
- 駅・番線・種別・軌道回路・進路は共有マスター。参照中の設備は `RESTRICT` で保護する。

時刻は運行日0時からの秒数。`NULL` と0時を区別し、24時以降も保持する。地点区分は `service` / `depot_exit` / `depot_entry`。使用進路は従来どおりGo APIで生成し、通常時刻表は場内・出発、出庫・入庫は入換に限定する。候補なし・複数候補の保存は拒否する。

## 削除

| 削除対象 | 削除されるもの | 残るもの |
| --- | --- | --- |
| 運用 | その運用と割り当て | ダイヤ・列車・地点・使用進路 |
| 運用計画 | その計画の運用と割り当て | 対象ダイヤと列車 |
| 列車 | 列車・地点・使用進路 | 別の列車と共有マスター。APIは運用割り当て中の削除を409で拒否 |
| ダイヤ | そのダイヤの列車・地点・使用進路・運用計画・運用・割り当て | 他ダイヤと共有マスター |

所有関係の外部キーは `CASCADE`。APIでの列車削除は割り当て確認と削除を同一トランザクションで行う。運用の割り当て更新も、削除・再登録を1トランザクションで行い、エラー時は元の並びを保持する。

## API

| API | 動作 |
| --- | --- |
| `GET /api/timetable` | ダイヤ一覧 |
| `GET /api/timetable/{id}` | そのダイヤの全列車、種別、地点、使用進路。運用計画は含めない |
| `POST /api/timetable/` | `id`, `displayName`, `revision` で空のダイヤを作成 |
| `PATCH /api/timetable/{id}` | 名称を更新 |
| `DELETE /api/timetable/{id}` | ダイヤと所有データを削除 |
| `GET /api/trains?timetableId=...` | 対象ダイヤの列車一覧。クエリ省略時は全列車。UUID・区間・始発時刻を返す |
| `GET /api/trains/{id}` | 列車UUIDで詳細取得 |
| `POST /api/trains` | `timetableId`, 列番・種別・発着駅・地点を登録。UUIDはGoで生成 |
| `PATCH /api/trains/{id}` | 列番・種別・発着駅・地点を更新。所属ダイヤは変更しない |
| `DELETE /api/trains/{id}` | UUIDで削除 |
| `GET /api/operation-plans?timetableId=...` | 対象ダイヤの運用計画一覧 |
| `GET /api/operation-plans/{id}` | 計画・運用・割り当ての詳細 |
| `POST /api/operation-plans` | `timetableId`, `displayName` で作成 |
| `PATCH /api/operation-plans/{id}` | 計画名を更新 |
| `DELETE /api/operation-plans/{id}` | 計画と運用を削除 |
| `GET /api/operations?operationPlanId=...` | 計画内の運用一覧 |
| `GET /api/operations/{id}` | 運用詳細 |
| `POST /api/operations` | `operationPlanId`, `operationNumber`, `trains: [{trainId, sequenceIndex}]` で作成 |
| `PATCH /api/operations/{id}` | 運用番号・割り当て順を更新 |
| `DELETE /api/operations/{id}` | 運用を削除 |

`/api/trains/{...}` と `/api/operations/{...}` は旧列番・運用番号ではなくUUIDを要求する。旧URLへの番号フォールバックは設けない。列車と運用の作成・更新で送られた不正な参照や重複割り当ては、トランザクションと外部キー／一意制約で拒否する。

列車の作成・編集・削除時は所属ダイヤのRevisionを増やす。実行中のPRCへのホットリロードは行わない。

## Web編集

- ダイヤ編集：選択したダイヤの列車を `EditorTable` に表示。＋で新規列車、既存列車を複製して追加、選択行またはスジで列車編集。
- 列車の新規作成前にダイヤを保存する。列車は保存した時点でそのダイヤに所属する。
- 複製は元列車の時刻・地点を新UUIDで登録し直す。使用進路はGo側で再検証する。
- 列番は登録後も変更可能。行・スジ・ウインドウはUUIDで区別する。
- 運用編集：対象ダイヤ→運用計画→運用の順に選択。所属列車の＋では対象ダイヤの未割り当て列車だけを候補にし、↑↓で順序を変更する。
- 運用と運用計画の削除には確認Dialogを表示。列車は残す。
- 運用計画の名称にも既存の未保存変更ガードを適用する。リロード・タブ切替・選択変更時の確認を維持する。

## PRCとの境界

Go起動時に `-timetable <ダイヤID>` と、必要に応じて `-operation-plan <運用計画ID>` を指定する。運用計画を指定しない場合は列車だけを読み込み、指定時は対象ダイヤ一致を検証する。

現在のUnity/CTCの在線プロトコルとPRCは列番で実列車を照合する。そのため、編集・保存では許可する「同じダイヤ内の列番重複」はPRC読込み時に明確なエラーで拒否する。別ダイヤに同じ列番があっても問題ない。DB内部ID化に伴って実列車の識別プロトコルまで暗黙に変更せず、番号重複で片方の列車を上書きしない。

## 旧DBの扱い

ユーザー指定により、旧データはコピー・バックアップしない。旧 `timetable_trains` にUUIDの `id` 列がない場合だけ、旧ダイヤ・列車・地点・使用進路・運用と中間テーブルを削除し、新スキーマを作成する。駅・番線・設備・種別・走行パターン・標準運転時分は保持する。

判定は `PRAGMA table_info` の正確な列名で行う。リセットとスキーマ作成は同一トランザクション。新スキーマ適用後の再起動では、新しく保存したダイヤを消さない。

## 主な実装ファイル

- `Server/internal/timetable/models.go`：UUID、所有関係、複合外部キー、一意制約。
- `Server/internal/timetable/migration_identity.go` / `database.go`：旧スキーマの初回リセットと新モデルの作成。
- `Server/internal/timetable/loader.go`：ダイヤと運用計画の分離取得、PRCへの変換と対象確認。
- `Server/internal/api/train.go` / `timetable.go` / `operation.go` / `operation_plan.go` / `routes.go`：新API契約、UUID、絞り込み、トランザクション。
- `Server/main.go`：任意の `-operation-plan` 起動引数。
- `client/src/types/{train,timetable,operation,timetableDiagram}.ts`：新レスポンス型。
- `client/src/app/timetables/*actions.ts`：Go APIの新しいURLとペイロード。
- `client/src/components/timetable/{TimetableEditor,TimetableInput,OperationInput,TrainEditWindow,DiagramViewer}.tsx`：列車主体の編集とUUIDでの選択。
- `client/src/components/timetable/{TimetableSelect,DeleteConfirmation}.tsx`：共通のダイヤ選択と削除確認。
- `client/src/components/timetable/UnsavedChanges.tsx`：運用計画の変更ガード。

## 検証

SQLiteの実DBを使うテストで、列番重複時の内部ID、時刻・進路の独立性、複合外部キーによる対象ダイヤ一致、二重割り当て拒否、更新失敗時のロールバック、削除時の所有範囲、旧DBリセット時のマスター保持、再起動時の新データ保持を確認する。既存の進路自動生成・PRC・CTCのテストも継続実行する。
