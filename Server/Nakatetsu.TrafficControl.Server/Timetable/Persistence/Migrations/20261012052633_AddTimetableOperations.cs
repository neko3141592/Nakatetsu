using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 旧引継ぎを先頭列車ごとの運用へ変換する。循環がある場合は変更全体を取り消す。
            // 地点・使用進路・採番履歴を退避し、FKを有効にしたまま同じトランザクションで再構築する。
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__LegacyOperationTrains" AS
                WITH RECURSIVE chains("TimetableId", "OperationNumber", "SequenceIndex", "TrainNumber", "NextTrainNumber") AS (
                    SELECT train."TimetableId", 'legacy:' || train."TrainNumber", 0,
                        train."TrainNumber", train."NextTrainNumber"
                    FROM "TimetableTrains" AS train
                    WHERE NOT EXISTS (
                        SELECT 1 FROM "TimetableTrains" AS previous
                        WHERE previous."TimetableId" = train."TimetableId"
                            AND previous."NextTrainNumber" = train."TrainNumber")
                    UNION ALL
                    SELECT chains."TimetableId", chains."OperationNumber", chains."SequenceIndex" + 1,
                        train."TrainNumber", train."NextTrainNumber"
                    FROM chains
                    INNER JOIN "TimetableTrains" AS train
                        ON train."TimetableId" = chains."TimetableId"
                        AND train."TrainNumber" = chains."NextTrainNumber"
                    LIMIT (SELECT COUNT(*) FROM "TimetableTrains") + 1
                )
                SELECT "TimetableId", "OperationNumber", "SequenceIndex", "TrainNumber" FROM chains;

                CREATE TEMP TABLE "__LegacyOperationCoverage" (
                    "Complete" INTEGER NOT NULL,
                    CONSTRAINT "CK_TimetableOperations_LegacyChains_Acyclic" CHECK ("Complete" = 1)
                );
                INSERT INTO "__LegacyOperationCoverage" ("Complete")
                SELECT CASE WHEN (SELECT COUNT(*) FROM "__LegacyOperationTrains") =
                    (SELECT COUNT(*) FROM "TimetableTrains") THEN 1 ELSE 0 END;
                DROP TABLE "__LegacyOperationCoverage";

                CREATE TEMP TABLE "__OperationTrainData" AS
                SELECT "TimetableId", "TrainNumber", "TrainTypeId" FROM "TimetableTrains";
                CREATE TEMP TABLE "__OperationPointData" AS
                SELECT "TimetablePointId", "TimetableId", "TrainNumber", "SequenceIndex", "StationId",
                    "TrackCircuitId", "Handling", "ArrivalSeconds", "DepartureSeconds" FROM "TimetablePoints";
                CREATE TEMP TABLE "__OperationRouteData" AS
                SELECT "TimetablePointId", "SequenceIndex", "ControlStationId", "RouteId" FROM "TimetablePointRoutes";
                CREATE TEMP TABLE "__OperationPointSequence" AS
                SELECT COALESCE(MAX("seq"), 0) AS "seq" FROM "sqlite_sequence" WHERE "name" = 'TimetablePoints';

                DROP TABLE "TimetablePointRoutes";
                DROP TABLE "TimetablePoints";
                UPDATE "TimetableTrains" SET "NextTrainNumber" = NULL;
                DROP TABLE "TimetableTrains";

                CREATE TABLE "TimetableTrains" (
                    "TimetableId" INTEGER NOT NULL,
                    "TrainNumber" TEXT COLLATE BINARY NOT NULL,
                    "TrainTypeId" TEXT COLLATE BINARY NOT NULL,
                    CONSTRAINT "PK_TimetableTrains" PRIMARY KEY ("TimetableId", "TrainNumber"),
                    CONSTRAINT "FK_TimetableTrains_Timetables_TimetableId"
                        FOREIGN KEY ("TimetableId") REFERENCES "Timetables" ("TimetableId") ON DELETE CASCADE,
                    CONSTRAINT "FK_TimetableTrains_TrainTypes_TrainTypeId"
                        FOREIGN KEY ("TrainTypeId") REFERENCES "TrainTypes" ("TrainTypeId") ON DELETE RESTRICT
                );
                INSERT INTO "TimetableTrains" ("TimetableId", "TrainNumber", "TrainTypeId")
                SELECT "TimetableId", "TrainNumber", "TrainTypeId" FROM "__OperationTrainData";
                DROP TABLE "__OperationTrainData";

                CREATE TABLE "TimetablePoints" (
                    "TimetablePointId" INTEGER NOT NULL CONSTRAINT "PK_TimetablePoints" PRIMARY KEY AUTOINCREMENT,
                    "TimetableId" INTEGER NOT NULL,
                    "TrainNumber" TEXT COLLATE BINARY NOT NULL,
                    "SequenceIndex" INTEGER NOT NULL,
                    "StationId" TEXT COLLATE BINARY NULL,
                    "TrackCircuitId" TEXT COLLATE BINARY NOT NULL,
                    "Handling" INTEGER NOT NULL,
                    "ArrivalSeconds" INTEGER NULL,
                    "DepartureSeconds" INTEGER NULL,
                    CONSTRAINT "CK_TimetablePoints_SequenceIndex" CHECK ("SequenceIndex" >= 0),
                    CONSTRAINT "CK_TimetablePoints_Handling" CHECK ("Handling" IN (1, 2)),
                    CONSTRAINT "CK_TimetablePoints_ArrivalSeconds" CHECK (
                        "ArrivalSeconds" IS NULL OR "ArrivalSeconds" >= 0),
                    CONSTRAINT "CK_TimetablePoints_DepartureSeconds" CHECK (
                        "DepartureSeconds" IS NULL OR "DepartureSeconds" >= 0),
                    CONSTRAINT "CK_TimetablePoints_ArrivalDepartureOrder" CHECK (
                        "ArrivalSeconds" IS NULL OR "DepartureSeconds" IS NULL
                        OR "ArrivalSeconds" <= "DepartureSeconds"),
                    CONSTRAINT "FK_TimetablePoints_Stations_StationId"
                        FOREIGN KEY ("StationId") REFERENCES "Stations" ("StationId") ON DELETE RESTRICT,
                    CONSTRAINT "FK_TimetablePoints_TrackCircuits_TrackCircuitId"
                        FOREIGN KEY ("TrackCircuitId") REFERENCES "TrackCircuits" ("TrackCircuitId") ON DELETE RESTRICT,
                    CONSTRAINT "FK_TimetablePoints_TimetableTrains_TimetableId_TrainNumber"
                        FOREIGN KEY ("TimetableId", "TrainNumber")
                        REFERENCES "TimetableTrains" ("TimetableId", "TrainNumber") ON DELETE CASCADE
                );

                INSERT INTO "TimetablePoints" (
                    "TimetablePointId", "TimetableId", "TrainNumber", "SequenceIndex", "StationId",
                    "TrackCircuitId", "Handling", "ArrivalSeconds", "DepartureSeconds")
                SELECT "TimetablePointId", "TimetableId", "TrainNumber", "SequenceIndex", "StationId",
                    "TrackCircuitId", "Handling", "ArrivalSeconds", "DepartureSeconds" FROM "__OperationPointData";
                DROP TABLE "__OperationPointData";

                INSERT INTO "sqlite_sequence" ("name", "seq")
                SELECT 'TimetablePoints', "seq" FROM "__OperationPointSequence"
                WHERE NOT EXISTS (SELECT 1 FROM "sqlite_sequence" WHERE "name" = 'TimetablePoints');
                UPDATE "sqlite_sequence"
                SET "seq" = MAX("seq", (SELECT "seq" FROM "__OperationPointSequence"))
                WHERE "name" = 'TimetablePoints';
                DROP TABLE "__OperationPointSequence";

                CREATE TABLE "TimetablePointRoutes" (
                    "TimetablePointId" INTEGER NOT NULL,
                    "SequenceIndex" INTEGER NOT NULL,
                    "ControlStationId" TEXT COLLATE BINARY NOT NULL,
                    "RouteId" TEXT COLLATE BINARY NOT NULL,
                    CONSTRAINT "PK_TimetablePointRoutes" PRIMARY KEY ("TimetablePointId", "SequenceIndex"),
                    CONSTRAINT "CK_TimetablePointRoutes_SequenceIndex" CHECK ("SequenceIndex" >= 0),
                    CONSTRAINT "FK_TimetablePointRoutes_TimetablePoints_TimetablePointId"
                        FOREIGN KEY ("TimetablePointId") REFERENCES "TimetablePoints" ("TimetablePointId") ON DELETE CASCADE,
                    CONSTRAINT "FK_TimetablePointRoutes_Routes_ControlStationId_RouteId"
                        FOREIGN KEY ("ControlStationId", "RouteId")
                        REFERENCES "Routes" ("ControlStationId", "RouteId") ON DELETE RESTRICT
                );

                INSERT INTO "TimetablePointRoutes" ("TimetablePointId", "SequenceIndex", "ControlStationId", "RouteId")
                SELECT "TimetablePointId", "SequenceIndex", "ControlStationId", "RouteId" FROM "__OperationRouteData";
                DROP TABLE "__OperationRouteData";

                CREATE TABLE "TimetableOperations" (
                    "TimetableId" INTEGER NOT NULL,
                    "OperationNumber" TEXT COLLATE BINARY NOT NULL,
                    CONSTRAINT "PK_TimetableOperations" PRIMARY KEY ("TimetableId", "OperationNumber"),
                    CONSTRAINT "FK_TimetableOperations_Timetables_TimetableId"
                        FOREIGN KEY ("TimetableId") REFERENCES "Timetables" ("TimetableId") ON DELETE CASCADE
                );
                INSERT INTO "TimetableOperations" ("TimetableId", "OperationNumber")
                SELECT DISTINCT "TimetableId", "OperationNumber" FROM "__LegacyOperationTrains";

                CREATE TABLE "TimetableOperationTrains" (
                    "TimetableId" INTEGER NOT NULL,
                    "OperationNumber" TEXT COLLATE BINARY NOT NULL,
                    "SequenceIndex" INTEGER NOT NULL,
                    "TrainNumber" TEXT COLLATE BINARY NOT NULL,
                    CONSTRAINT "PK_TimetableOperationTrains" PRIMARY KEY ("TimetableId", "OperationNumber", "SequenceIndex"),
                    CONSTRAINT "CK_TimetableOperationTrains_SequenceIndex" CHECK ("SequenceIndex" >= 0),
                    CONSTRAINT "FK_TimetableOperationTrains_TimetableOperations_TimetableId_OperationNumber"
                        FOREIGN KEY ("TimetableId", "OperationNumber")
                        REFERENCES "TimetableOperations" ("TimetableId", "OperationNumber") ON DELETE CASCADE,
                    CONSTRAINT "FK_TimetableOperationTrains_TimetableTrains_TimetableId_TrainNumber"
                        FOREIGN KEY ("TimetableId", "TrainNumber")
                        REFERENCES "TimetableTrains" ("TimetableId", "TrainNumber") ON DELETE RESTRICT
                );
                INSERT INTO "TimetableOperationTrains" ("TimetableId", "OperationNumber", "SequenceIndex", "TrainNumber")
                SELECT "TimetableId", "OperationNumber", "SequenceIndex", "TrainNumber" FROM "__LegacyOperationTrains";
                DROP TABLE "__LegacyOperationTrains";

                CREATE UNIQUE INDEX "IX_TimetableOperationTrains_TimetableId_TrainNumber"
                    ON "TimetableOperationTrains" ("TimetableId", "TrainNumber");
                CREATE INDEX "IX_TimetableTrains_TrainTypeId" ON "TimetableTrains" ("TrainTypeId");
                CREATE UNIQUE INDEX "IX_TimetablePoints_TimetableId_TrainNumber_SequenceIndex"
                    ON "TimetablePoints" ("TimetableId", "TrainNumber", "SequenceIndex");
                CREATE INDEX "IX_TimetablePoints_StationId" ON "TimetablePoints" ("StationId");
                CREATE INDEX "IX_TimetablePoints_TrackCircuitId" ON "TimetablePoints" ("TrackCircuitId");
                CREATE INDEX "IX_TimetablePointRoutes_ControlStationId_RouteId"
                    ON "TimetablePointRoutes" ("ControlStationId", "RouteId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 運用番号と空運用は旧スキーマへ戻せないため、移行前のバックアップで復元する。
            throw new NotSupportedException(
                "運用番号や空運用を旧スキーマで保持できません。移行前のDBバックアップを復元してください。");
        }
    }
}
