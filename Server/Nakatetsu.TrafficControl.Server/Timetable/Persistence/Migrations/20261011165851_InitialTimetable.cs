using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nakatetsu.TrafficControl.Server.Timetable.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialTimetable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Routes",
                columns: table => new
                {
                    ControlStationId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    RouteId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Routes", x => new { x.ControlStationId, x.RouteId });
                });

            migrationBuilder.CreateTable(
                name: "Stations",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stations", x => x.StationId);
                });

            migrationBuilder.CreateTable(
                name: "Timetables",
                columns: table => new
                {
                    TimetableId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    Revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Timetables", x => x.TimetableId);
                    table.CheckConstraint("CK_Timetables_Revision", "\"Revision\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "TrackCircuits",
                columns: table => new
                {
                    TrackCircuitId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackCircuits", x => x.TrackCircuitId);
                });

            migrationBuilder.CreateTable(
                name: "TrainTypes",
                columns: table => new
                {
                    TrainTypeId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainTypes", x => x.TrainTypeId);
                });

            migrationBuilder.CreateTable(
                name: "StationRunningTimes",
                columns: table => new
                {
                    TrainTypeId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    FromStationId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    ToStationId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    RunningSeconds = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StationRunningTimes", x => new { x.TrainTypeId, x.FromStationId, x.ToStationId });
                    table.CheckConstraint("CK_StationRunningTimes_RunningSeconds", "\"RunningSeconds\" > 0");
                    table.ForeignKey(
                        name: "FK_StationRunningTimes_Stations_FromStationId",
                        column: x => x.FromStationId,
                        principalTable: "Stations",
                        principalColumn: "StationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StationRunningTimes_Stations_ToStationId",
                        column: x => x.ToStationId,
                        principalTable: "Stations",
                        principalColumn: "StationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StationRunningTimes_TrainTypes_TrainTypeId",
                        column: x => x.TrainTypeId,
                        principalTable: "TrainTypes",
                        principalColumn: "TrainTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TrainRuns",
                columns: table => new
                {
                    TrainRunId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TimetableId = table.Column<long>(type: "INTEGER", nullable: false),
                    TrainNumber = table.Column<string>(type: "TEXT", nullable: false),
                    TrainTypeId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    NextTrainRunId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainRuns", x => x.TrainRunId);
                    table.UniqueConstraint("AK_TrainRuns_TimetableId_TrainRunId", x => new { x.TimetableId, x.TrainRunId });
                    table.CheckConstraint("CK_TrainRuns_NextTrainRunId", "\"NextTrainRunId\" IS NULL OR \"NextTrainRunId\" <> \"TrainRunId\"");
                    table.ForeignKey(
                        name: "FK_TrainRuns_Timetables_TimetableId",
                        column: x => x.TimetableId,
                        principalTable: "Timetables",
                        principalColumn: "TimetableId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainRuns_TrainRuns_TimetableId_NextTrainRunId",
                        columns: x => new { x.TimetableId, x.NextTrainRunId },
                        principalTable: "TrainRuns",
                        principalColumns: new[] { "TimetableId", "TrainRunId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TrainRuns_TrainTypes_TrainTypeId",
                        column: x => x.TrainTypeId,
                        principalTable: "TrainTypes",
                        principalColumn: "TrainTypeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TimetablePoints",
                columns: table => new
                {
                    TimetablePointId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrainRunId = table.Column<long>(type: "INTEGER", nullable: false),
                    SequenceIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    StationId = table.Column<string>(type: "TEXT", nullable: true, collation: "BINARY"),
                    TrackCircuitId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    Handling = table.Column<int>(type: "INTEGER", nullable: false),
                    ArrivalSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    DepartureSeconds = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetablePoints", x => x.TimetablePointId);
                    table.CheckConstraint("CK_TimetablePoints_ArrivalDepartureOrder", "\"ArrivalSeconds\" IS NULL OR \"DepartureSeconds\" IS NULL OR \"ArrivalSeconds\" <= \"DepartureSeconds\"");
                    table.CheckConstraint("CK_TimetablePoints_ArrivalSeconds", "\"ArrivalSeconds\" IS NULL OR \"ArrivalSeconds\" >= 0");
                    table.CheckConstraint("CK_TimetablePoints_DepartureSeconds", "\"DepartureSeconds\" IS NULL OR \"DepartureSeconds\" >= 0");
                    table.CheckConstraint("CK_TimetablePoints_Handling", "\"Handling\" IN (1, 2)");
                    table.CheckConstraint("CK_TimetablePoints_SequenceIndex", "\"SequenceIndex\" >= 0");
                    table.ForeignKey(
                        name: "FK_TimetablePoints_Stations_StationId",
                        column: x => x.StationId,
                        principalTable: "Stations",
                        principalColumn: "StationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetablePoints_TrackCircuits_TrackCircuitId",
                        column: x => x.TrackCircuitId,
                        principalTable: "TrackCircuits",
                        principalColumn: "TrackCircuitId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetablePoints_TrainRuns_TrainRunId",
                        column: x => x.TrainRunId,
                        principalTable: "TrainRuns",
                        principalColumn: "TrainRunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimetablePointRoutes",
                columns: table => new
                {
                    TimetablePointId = table.Column<long>(type: "INTEGER", nullable: false),
                    SequenceIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    ControlStationId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY"),
                    RouteId = table.Column<string>(type: "TEXT", nullable: false, collation: "BINARY")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimetablePointRoutes", x => new { x.TimetablePointId, x.SequenceIndex });
                    table.CheckConstraint("CK_TimetablePointRoutes_SequenceIndex", "\"SequenceIndex\" >= 0");
                    table.ForeignKey(
                        name: "FK_TimetablePointRoutes_Routes_ControlStationId_RouteId",
                        columns: x => new { x.ControlStationId, x.RouteId },
                        principalTable: "Routes",
                        principalColumns: new[] { "ControlStationId", "RouteId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimetablePointRoutes_TimetablePoints_TimetablePointId",
                        column: x => x.TimetablePointId,
                        principalTable: "TimetablePoints",
                        principalColumn: "TimetablePointId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StationRunningTimes_FromStationId",
                table: "StationRunningTimes",
                column: "FromStationId");

            migrationBuilder.CreateIndex(
                name: "IX_StationRunningTimes_ToStationId",
                table: "StationRunningTimes",
                column: "ToStationId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePointRoutes_ControlStationId_RouteId",
                table: "TimetablePointRoutes",
                columns: new[] { "ControlStationId", "RouteId" });

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePoints_StationId",
                table: "TimetablePoints",
                column: "StationId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePoints_TrackCircuitId",
                table: "TimetablePoints",
                column: "TrackCircuitId");

            migrationBuilder.CreateIndex(
                name: "IX_TimetablePoints_TrainRunId_SequenceIndex",
                table: "TimetablePoints",
                columns: new[] { "TrainRunId", "SequenceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainRuns_TimetableId_NextTrainRunId",
                table: "TrainRuns",
                columns: new[] { "TimetableId", "NextTrainRunId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainRuns_TrainTypeId",
                table: "TrainRuns",
                column: "TrainTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StationRunningTimes");

            migrationBuilder.DropTable(
                name: "TimetablePointRoutes");

            migrationBuilder.DropTable(
                name: "Routes");

            migrationBuilder.DropTable(
                name: "TimetablePoints");

            migrationBuilder.DropTable(
                name: "Stations");

            migrationBuilder.DropTable(
                name: "TrackCircuits");

            migrationBuilder.DropTable(
                name: "TrainRuns");

            migrationBuilder.DropTable(
                name: "Timetables");

            migrationBuilder.DropTable(
                name: "TrainTypes");
        }
    }
}
