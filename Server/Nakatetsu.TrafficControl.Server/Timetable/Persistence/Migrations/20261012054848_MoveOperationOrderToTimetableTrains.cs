using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoveOperationOrderToTimetableTrains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 必須の所属先を失う列車がある場合は、変更前に移行を止める。
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__DirectOperationTrainData" AS
                SELECT train."TimetableId", train."TrainNumber", train."TrainTypeId",
                    member."OperationNumber", member."SequenceIndex"
                FROM "TimetableTrains" AS train
                INNER JOIN "TimetableOperationTrains" AS member
                    ON member."TimetableId" = train."TimetableId"
                    AND member."TrainNumber" = train."TrainNumber";

                CREATE TEMP TABLE "__DirectOperationCoverage" (
                    "Complete" INTEGER NOT NULL,
                    CONSTRAINT "CK_TimetableTrains_AllTrainsMustBelongToOperation" CHECK ("Complete" = 1)
                );
                INSERT INTO "__DirectOperationCoverage" ("Complete")
                SELECT CASE WHEN (SELECT COUNT(*) FROM "__DirectOperationTrainData") =
                    (SELECT COUNT(*) FROM "TimetableTrains") THEN 1 ELSE 0 END;
                DROP TABLE "__DirectOperationCoverage";
                """);

            BackupChildren(migrationBuilder);
            migrationBuilder.Sql("""
                DROP TABLE "TimetableOperationTrains";
                DROP TABLE "TimetablePointRoutes";
                DROP TABLE "TimetablePoints";
                DROP TABLE "TimetableTrains";

                CREATE TABLE "TimetableTrains" (
                    "TimetableId" INTEGER NOT NULL,
                    "TrainNumber" TEXT COLLATE BINARY NOT NULL,
                    "OperationNumber" TEXT COLLATE BINARY NOT NULL,
                    "SequenceIndex" INTEGER NOT NULL,
                    "TrainTypeId" TEXT COLLATE BINARY NOT NULL,
                    CONSTRAINT "PK_TimetableTrains" PRIMARY KEY ("TimetableId", "TrainNumber"),
                    CONSTRAINT "CK_TimetableTrains_SequenceIndex" CHECK ("SequenceIndex" >= 0),
                    CONSTRAINT "FK_TimetableTrains_Timetables_TimetableId"
                        FOREIGN KEY ("TimetableId") REFERENCES "Timetables" ("TimetableId") ON DELETE CASCADE,
                    CONSTRAINT "FK_TimetableTrains_TimetableOperations_TimetableId_OperationNumber"
                        FOREIGN KEY ("TimetableId", "OperationNumber")
                        REFERENCES "TimetableOperations" ("TimetableId", "OperationNumber") ON DELETE CASCADE,
                    CONSTRAINT "FK_TimetableTrains_TrainTypes_TrainTypeId"
                        FOREIGN KEY ("TrainTypeId") REFERENCES "TrainTypes" ("TrainTypeId") ON DELETE RESTRICT
                );
                INSERT INTO "TimetableTrains" (
                    "TimetableId", "TrainNumber", "OperationNumber", "SequenceIndex", "TrainTypeId")
                SELECT "TimetableId", "TrainNumber", "OperationNumber", "SequenceIndex", "TrainTypeId"
                FROM "__DirectOperationTrainData";
                DROP TABLE "__DirectOperationTrainData";

                CREATE UNIQUE INDEX "IX_TimetableTrains_TimetableId_OperationNumber_SequenceIndex"
                    ON "TimetableTrains" ("TimetableId", "OperationNumber", "SequenceIndex");
                """);
            RestoreChildren(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 直接保持した所属・順序を中間テーブルに戻し、子データも保持する。
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__DirectOperationTrainData" AS
                SELECT "TimetableId", "TrainNumber", "TrainTypeId", "OperationNumber", "SequenceIndex"
                FROM "TimetableTrains";
                """);
            BackupChildren(migrationBuilder);
            migrationBuilder.Sql("""
                DROP TABLE "TimetablePointRoutes";
                DROP TABLE "TimetablePoints";
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
                SELECT "TimetableId", "TrainNumber", "TrainTypeId" FROM "__DirectOperationTrainData";

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
                SELECT "TimetableId", "OperationNumber", "SequenceIndex", "TrainNumber" FROM "__DirectOperationTrainData";
                DROP TABLE "__DirectOperationTrainData";
                CREATE UNIQUE INDEX "IX_TimetableOperationTrains_TimetableId_TrainNumber"
                    ON "TimetableOperationTrains" ("TimetableId", "TrainNumber");
                """);
            RestoreChildren(migrationBuilder);
        }

        private static void BackupChildren(MigrationBuilder migrationBuilder)
        {
            // FKを有効にした同じトランザクションで再構築し、削除済み地点の採番履歴も残す。
            migrationBuilder.Sql("""
                CREATE TEMP TABLE "__OperationPointData" AS
                SELECT "TimetablePointId", "TimetableId", "TrainNumber", "SequenceIndex", "StationId",
                    "TrackCircuitId", "Handling", "ArrivalSeconds", "DepartureSeconds" FROM "TimetablePoints";
                CREATE TEMP TABLE "__OperationRouteData" AS
                SELECT "TimetablePointId", "SequenceIndex", "ControlStationId", "RouteId" FROM "TimetablePointRoutes";
                CREATE TEMP TABLE "__OperationPointSequence" AS
                SELECT COALESCE(MAX("seq"), 0) AS "seq" FROM "sqlite_sequence" WHERE "name" = 'TimetablePoints';
                """);
        }

        private static void RestoreChildren(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
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

                CREATE INDEX "IX_TimetableTrains_TrainTypeId" ON "TimetableTrains" ("TrainTypeId");
                CREATE UNIQUE INDEX "IX_TimetablePoints_TimetableId_TrainNumber_SequenceIndex"
                    ON "TimetablePoints" ("TimetableId", "TrainNumber", "SequenceIndex");
                CREATE INDEX "IX_TimetablePoints_StationId" ON "TimetablePoints" ("StationId");
                CREATE INDEX "IX_TimetablePoints_TrackCircuitId" ON "TimetablePoints" ("TrackCircuitId");
                CREATE INDEX "IX_TimetablePointRoutes_ControlStationId_RouteId"
                    ON "TimetablePointRoutes" ("ControlStationId", "RouteId");
                """);
        }
    }
}
