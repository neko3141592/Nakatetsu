package timetable_test

import (
	"path/filepath"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/driver/sqlite"
	"gorm.io/gorm"
	"gorm.io/gorm/logger"
)

func openDatabase(t *testing.T, path string) *gorm.DB {
	t.Helper()
	db, err := gorm.Open(sqlite.Open(path+"?_foreign_keys=on&_busy_timeout=5000"), &gorm.Config{
		Logger: logger.Default.LogMode(logger.Silent),
	})
	if err != nil {
		t.Fatal(err)
	}
	sqlDB, err := db.DB()
	if err != nil {
		t.Fatal(err)
	}
	sqlDB.SetMaxOpenConns(1)
	t.Cleanup(func() { _ = sqlDB.Close() })
	if err := timetable.MigrateModels(db); err != nil {
		t.Fatal(err)
	}
	return db
}

func create(t *testing.T, db *gorm.DB, value any) {
	t.Helper()
	if err := db.Create(value).Error; err != nil {
		t.Fatal(err)
	}
}

func intPointer(value int) *int          { return &value }
func stringPointer(value string) *string { return &value }

func seed(t *testing.T, db *gorm.DB) []timetable.Timetable {
	t.Helper()
	create(t, db, &timetable.TrainType{TrainTypeID: "local", DisplayName: "各停"})
	create(t, db, &[]timetable.Station{{StationID: "NT01", DisplayName: "中鉄中央"}, {StationID: "NT05", DisplayName: "東長野"}})
	create(t, db, &[]timetable.TrackCircuit{{TrackCircuitID: "origin"}, {TrackCircuitID: "between"}, {TrackCircuitID: "destination"}})
	create(t, db, &[]timetable.Route{{ControlStationID: "NT01", RouteID: "1R"}, {ControlStationID: "NT05", RouteID: "1R"}})
	result := []timetable.Timetable{{ID: "weekday", DisplayName: "平日"}, {ID: "holiday", DisplayName: "休日"}}
	for i := range result {
		result[i].Trains = []timetable.TimetableTrain{{ID: result[i].ID + "-1", TrainNumber: "0001", TrainTypeID: "local", Points: []timetable.TimetablePoint{
			{SequenceIndex: 2, StationID: stringPointer("NT05"), TrackCircuitID: "destination", Handling: timetable.HandlingStop, ArrivalSeconds: intPointer(90600)},
			{SequenceIndex: 0, StationID: stringPointer("NT01"), TrackCircuitID: "origin", Handling: timetable.HandlingStop, DepartureSeconds: intPointer(0), Routes: []timetable.TimetablePointRoute{{SequenceIndex: 1, ControlStationID: "NT05", RouteID: "1R"}, {ControlStationID: "NT01", RouteID: "1R"}}},
			{SequenceIndex: 1, TrackCircuitID: "between", Handling: timetable.HandlingPass, DepartureSeconds: intPointer(86430)},
		}}, {ID: result[i].ID + "-2", TrainNumber: "0002", TrainTypeID: "local"}}
		create(t, db, &result[i])
		plan := timetable.TimetableOperationPlan{ID: result[i].ID + "-plan", TimetableID: result[i].ID, DisplayName: "運用案"}
		create(t, db, &plan)
		operation := timetable.TimetableOperation{ID: result[i].ID + "-op", OperationPlanID: plan.ID, TimetableID: plan.TimetableID, OperationNumber: "A"}
		create(t, db, &operation)
		create(t, db, &[]timetable.TimetableOperationTrain{{OperationID: operation.ID, OperationPlanID: plan.ID, TimetableID: plan.TimetableID, TrainID: result[i].Trains[1].ID, SequenceIndex: 1}, {OperationID: operation.ID, OperationPlanID: plan.ID, TimetableID: plan.TimetableID, TrainID: result[i].Trains[0].ID}})
	}
	return result
}
func ordered(db *gorm.DB) *gorm.DB { return db.Order("sequence_index") }
func load(t *testing.T, db *gorm.DB, id string) timetable.Timetable {
	t.Helper()
	result, err := timetable.LoadTimetable(t.Context(), db, id)
	if err != nil {
		t.Fatal(err)
	}
	return result
}
func count(t *testing.T, db *gorm.DB, model any, want int64) {
	t.Helper()
	var n int64
	if err := db.Model(model).Count(&n).Error; err != nil || n != want {
		t.Fatalf("%T count=%d want=%d err=%v", model, n, want, err)
	}
}

func TestIndependentTrainsAndOperationPlans(t *testing.T) {
	path := filepath.Join(t.TempDir(), "db.sqlite")
	db := openDatabase(t, path)
	seed(t, db)
	first := load(t, db, "weekday")
	other := load(t, db, "holiday")
	if first.Trains[0].ID == other.Trains[0].ID || len(first.Trains[0].Points) != 3 || first.Trains[0].Points[0].DepartureSeconds == nil || *first.Trains[0].Points[0].DepartureSeconds != 0 || first.Trains[0].Points[0].ArrivalSeconds != nil || *first.Trains[0].Points[2].ArrivalSeconds != 90600 || len(first.Trains[0].Points[0].Routes) != 2 {
		t.Fatalf("lost independent identities or times: %+v", first)
	}
	create(t, db, &timetable.TimetableTrain{TimetableID: "weekday", TrainNumber: "0001", TrainTypeID: "local"})
	if err := db.Model(&timetable.TimetablePoint{}).Where("train_id = ? AND sequence_index = 2", first.Trains[0].ID).Update("arrival_seconds", 91000).Error; err != nil {
		t.Fatal(err)
	}
	if *load(t, db, "holiday").Trains[0].Points[2].ArrivalSeconds != 90600 {
		t.Fatal("edit leaked to another timetable")
	}
	if err := db.Delete(&timetable.TimetableOperationPlan{}, "id = ?", "weekday-plan").Error; err != nil {
		t.Fatal(err)
	}
	count(t, db, &timetable.TimetableTrain{}, 5)
	count(t, db, &timetable.TimetablePoint{}, 6)
	if err := db.Delete(&timetable.Timetable{}, "id = ?", "weekday").Error; err != nil {
		t.Fatal(err)
	}
	count(t, db, &timetable.TimetableTrain{}, 2)
	count(t, db, &timetable.TimetableOperation{}, 1)
	count(t, db, &timetable.TimetablePoint{}, 3)
	if err := timetable.MigrateModels(db); err != nil {
		t.Fatal(err)
	}
}
func TestOperationScopeConstraints(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "db.sqlite"))
	seed(t, db)
	create(t, db, &timetable.TimetableOperation{ID: "weekday-op2", OperationPlanID: "weekday-plan", TimetableID: "weekday", OperationNumber: "B"})
	for _, row := range []timetable.TimetableOperationTrain{
		{OperationID: "weekday-op2", OperationPlanID: "weekday-plan", TimetableID: "weekday", TrainID: "weekday-1"},
		{OperationID: "weekday-op2", OperationPlanID: "weekday-plan", TimetableID: "holiday", TrainID: "holiday-1"},
		{OperationID: "weekday-op2", OperationPlanID: "holiday-plan", TimetableID: "holiday", TrainID: "holiday-1"},
		{OperationID: "weekday-op2", OperationPlanID: "weekday-plan", TimetableID: "weekday", TrainID: "missing"},
	} {
		if err := db.Create(&row).Error; err == nil {
			t.Fatalf("accepted invalid assignment %+v", row)
		}
	}
	create(t, db, &timetable.TimetableOperationPlan{ID: "alternative", TimetableID: "weekday", DisplayName: "別案"})
	create(t, db, &timetable.TimetableOperation{ID: "alternative-op", OperationPlanID: "alternative", TimetableID: "weekday", OperationNumber: "A"})
	create(t, db, &timetable.TimetableOperationTrain{OperationID: "alternative-op", OperationPlanID: "alternative", TimetableID: "weekday", TrainID: "weekday-1"})
}
func TestPointConstraints(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "db.sqlite"))
	seed(t, db)
	for _, row := range []timetable.TimetablePoint{
		{TrainID: "weekday-1", TrackCircuitID: "origin", SequenceIndex: 0},
		{TrainID: "missing", TrackCircuitID: "origin"},
		{TrainID: "weekday-2", TrackCircuitID: "missing"},
		{TrainID: "weekday-2", TrackCircuitID: "origin", Handling: "invalid"},
		{TrainID: "weekday-2", TrackCircuitID: "origin", Section: "invalid"},
		{TrainID: "weekday-2", TrackCircuitID: "origin", ArrivalSeconds: intPointer(-1)},
		{TrainID: "weekday-2", TrackCircuitID: "origin", ArrivalSeconds: intPointer(2), DepartureSeconds: intPointer(1)},
	} {
		if err := db.Create(&row).Error; err == nil {
			t.Fatalf("invalid point saved: %+v", row)
		}
	}
}

func TestStationRunningTimesAreDirectional(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	seed(t, db)
	create(t, db, &[]timetable.StationRunningTime{
		{TrainTypeID: "local", FromStationID: "NT01", ToStationID: "NT05", RunningSeconds: 120},
		{TrainTypeID: "local", FromStationID: "NT05", ToStationID: "NT01", RunningSeconds: 150},
	})
	count(t, db, &timetable.StationRunningTime{}, 2)
	for _, invalid := range []timetable.StationRunningTime{
		{TrainTypeID: "local", FromStationID: "NT01", ToStationID: "NT05", RunningSeconds: 999},
		{TrainTypeID: "local", FromStationID: "NT01", ToStationID: "NT01", RunningSeconds: 0},
		{TrainTypeID: "local", FromStationID: "NT01", ToStationID: "NT01", RunningSeconds: -1},
		{TrainTypeID: "missing", FromStationID: "NT01", ToStationID: "NT05", RunningSeconds: 120},
		{TrainTypeID: "local", FromStationID: "missing", ToStationID: "NT05", RunningSeconds: 120},
	} {
		if err := db.Create(&invalid).Error; err == nil {
			t.Fatalf("invalid running time was saved: %+v", invalid)
		}
	}
}
