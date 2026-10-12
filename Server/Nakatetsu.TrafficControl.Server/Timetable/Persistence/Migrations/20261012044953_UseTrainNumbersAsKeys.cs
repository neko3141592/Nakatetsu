using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UseTrainNumbersAsKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 旧IDを参照できる間に列番へ変換する。全処理を同じトランザクションで行う。
            // 使用進路を一時退避してから地点を再構築し、Cascadeによる消失を防ぐ。
            migrationBuilder.Sql("""
                CREATE TABLE "TimetableTrains" (
                    "TimetableId" INTEGER NOT NULL,
                    "TrainNumber" TEXT COLLATE BINARY NOT NULL,
                    "TrainTypeId" TEXT COLLATE BINARY NOT NULL,
                    "NextTrainNumber" TEXT COLLATE BINARY NULL,
                    CONSTRAINT "PK_TimetableTrains" PRIMARY KEY ("TimetableId", "TrainNumber"),
                    CONSTRAINT "CK_TimetableTrains_NextTrainNumber" CHECK (
                        "NextTrainNumber" IS NULL OR "NextTrainNumber" <> "TrainNumber"),
                    CONSTRAINT "FK_TimetableTrains_Timetables_TimetableId"
                        FOREIGN KEY ("TimetableId") REFERENCES "Timetables" ("TimetableId") ON DELETE CASCADE,
                    CONSTRAINT "FK_TimetableTrains_TrainTypes_TrainTypeId"
                        FOREIGN KEY ("TrainTypeId") REFERENCES "TrainTypes" ("TrainTypeId") ON DELETE RESTRICT,
                    CONSTRAINT "FK_TimetableTrains_TimetableTrains_TimetableId_NextTrainNumber"
                        FOREIGN KEY ("TimetableId", "NextTrainNumber")
                        REFERENCES "TimetableTrains" ("TimetableId", "TrainNumber") ON DELETE RESTRICT
                );

                INSERT INTO "TimetableTrains" ("TimetableId", "TrainNumber", "TrainTypeId", "NextTrainNumber")
                SELECT current."TimetableId", current."TrainNumber", current."TrainTypeId", next."TrainNumber"
                FROM "TrainRuns" AS current
                LEFT JOIN "TrainRuns" AS next
                    ON next."TimetableId" = current."TimetableId"
                    AND next."TrainRunId" = current."NextTrainRunId";

                CREATE TABLE "__TimetablePoints_TrainNumbers" (
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

                INSERT INTO "__TimetablePoints_TrainNumbers" (
                    "TimetablePointId", "TimetableId", "TrainNumber", "SequenceIndex", "StationId",
                    "TrackCircuitId", "Handling", "ArrivalSeconds", "DepartureSeconds")
                SELECT point."TimetablePointId", train."TimetableId", train."TrainNumber",
                    point."SequenceIndex", point."StationId", point."TrackCircuitId", point."Handling",
                    point."ArrivalSeconds", point."DepartureSeconds"
                FROM "TimetablePoints" AS point
                INNER JOIN "TrainRuns" AS train ON train."TrainRunId" = point."TrainRunId";

                CREATE TEMP TABLE "__TimetablePointRoutes_TrainNumbers" AS
                SELECT "TimetablePointId", "SequenceIndex", "ControlStationId", "RouteId"
                FROM "TimetablePointRoutes";

                CREATE TEMP TABLE "__TimetablePoints_Sequence" AS
                SELECT COALESCE(MAX("seq"), 0) AS "seq"
                FROM "sqlite_sequence" WHERE "name" = 'TimetablePoints';

                DROP TABLE "TimetablePointRoutes";
                DROP TABLE "TimetablePoints";
                UPDATE "TrainRuns" SET "NextTrainRunId" = NULL;
                DROP TABLE "TrainRuns";

                ALTER TABLE "__TimetablePoints_TrainNumbers" RENAME TO "TimetablePoints";

                INSERT INTO "sqlite_sequence" ("name", "seq")
                SELECT 'TimetablePoints', "seq" FROM "__TimetablePoints_Sequence"
                WHERE NOT EXISTS (SELECT 1 FROM "sqlite_sequence" WHERE "name" = 'TimetablePoints');
                UPDATE "sqlite_sequence"
                SET "seq" = MAX("seq", (SELECT "seq" FROM "__TimetablePoints_Sequence"))
                WHERE "name" = 'TimetablePoints';
                DROP TABLE "__TimetablePoints_Sequence";

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
                SELECT "TimetablePointId", "SequenceIndex", "ControlStationId", "RouteId"
                FROM "__TimetablePointRoutes_TrainNumbers";
                DROP TABLE "__TimetablePointRoutes_TrainNumbers";

                CREATE UNIQUE INDEX "IX_TimetableTrains_TimetableId_NextTrainNumber"
                    ON "TimetableTrains" ("TimetableId", "NextTrainNumber");
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
            // 削除した旧TrainRunIdは復元できないため、移行前のバックアップで復元する。
            throw new NotSupportedException(
                "元の列車計画IDを復元できません。移行前のDBバックアップを復元してください。");
        }
    }
}
