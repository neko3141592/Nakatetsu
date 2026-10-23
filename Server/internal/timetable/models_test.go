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
	// 別の制御駅で同じ進路IDを使用できる。
	create(t, db, &[]timetable.Route{{ControlStationID: "NT01", RouteID: "1R"}, {ControlStationID: "NT05", RouteID: "1R"}})

	operation := timetable.TimetableOperation{
		OperationNumber: "A",
		Trains: []timetable.TimetableOperationTrain{
			{TrainNumber: "0002", SequenceIndex: 1, Train: timetable.TimetableTrain{TrainNumber: "0002", TrainTypeID: "local"}},
			{TrainNumber: "0001", SequenceIndex: 0, Train: timetable.TimetableTrain{
				TrainNumber: "0001", TrainTypeID: "local",
				Points: []timetable.TimetablePoint{
					{SequenceIndex: 2, StationID: stringPointer("NT05"), TrackCircuitID: "destination", Handling: timetable.HandlingStop, ArrivalSeconds: intPointer(90600)},
					{SequenceIndex: 0, StationID: stringPointer("NT01"), TrackCircuitID: "origin", Handling: timetable.HandlingStop, DepartureSeconds: intPointer(0), Routes: []timetable.TimetablePointRoute{
						{SequenceIndex: 1, ControlStationID: "NT05", RouteID: "1R"},
						{SequenceIndex: 0, ControlStationID: "NT01", RouteID: "1R"},
					}},
					{SequenceIndex: 1, TrackCircuitID: "between", Handling: timetable.HandlingPass, DepartureSeconds: intPointer(86430)},
				},
			}},
		},
	}
	create(t, db, &operation)
	// 同じ列車定義を異なる運用内の異なる位置で使用できる。
	create(t, db, &timetable.TimetableOperation{
		OperationNumber: "B",
		Trains:          []timetable.TimetableOperationTrain{{TrainNumber: "0001", SequenceIndex: 5}},
	})
	result := []timetable.Timetable{
		{
			ID:          "weekday",
			DisplayName: "平日",
			Trains:      []timetable.TimetableTrain{{TrainNumber: "0001"}, {TrainNumber: "0002"}},
			Operations:  []timetable.TimetableOperation{{OperationNumber: "A"}},
		},
		{
			ID:          "holiday",
			DisplayName: "休日",
			Trains:      []timetable.TimetableTrain{{TrainNumber: "0001"}, {TrainNumber: "0002"}},
			Operations:  []timetable.TimetableOperation{{OperationNumber: "A"}},
		},
	}
	for i := range result {
		// 既存列車の定義は更新せず、ダイヤとの関連だけを保存する。
		create(t, db.Omit("Trains.*"), &result[i])
	}
	return result
}

func ordered(db *gorm.DB) *gorm.DB { return db.Order("sequence_index ASC") }

func load(t *testing.T, db *gorm.DB, id string) timetable.Timetable {
	t.Helper()
	var result timetable.Timetable
	err := db.Preload("Trains", func(db *gorm.DB) *gorm.DB { return db.Order("train_number ASC") }).
		Preload("Trains.TrainType").
		Preload("Trains.Points", ordered).
		Preload("Trains.Points.Routes", ordered).
		Preload("Operations").
		Preload("Operations.Trains", ordered).
		Preload("Operations.Trains.Train.Points", ordered).
		Preload("Operations.Trains.Train.Points.Routes", ordered).
		First(&result, "id = ?", id).Error
	if err != nil {
		t.Fatal(err)
	}
	return result
}

func count(t *testing.T, db *gorm.DB, model any, want int64) {
	t.Helper()
	var got int64
	if err := db.Model(model).Count(&got).Error; err != nil {
		t.Fatal(err)
	}
	if got != want {
		t.Fatalf("%T: count=%d, want=%d", model, got, want)
	}
}

func TestModelsPersistSharedTimetableHierarchy(t *testing.T) {
	path := filepath.Join(t.TempDir(), "timetable.sqlite")
	db := openDatabase(t, path)
	timetables := seed(t, db)
	sqlDB, _ := db.DB()
	if err := sqlDB.Close(); err != nil {
		t.Fatal(err)
	}
	// 再接続と再度のマイグレーション後も、共有定義と関連が維持される。
	db = openDatabase(t, path)
	for _, expected := range timetables {
		got := load(t, db, expected.ID)
		if got.DisplayName != expected.DisplayName || got.Revision != 0 || len(got.Operations) != 1 {
			t.Fatalf("unexpected timetable: %+v", got)
		}
		if len(got.Trains) != 2 || got.Trains[0].TrainNumber != "0001" || got.Trains[1].TrainNumber != "0002" {
			t.Fatalf("unexpected timetable trains: %+v", got.Trains)
		}
		operation := got.Operations[0]
		if operation.OperationNumber != "A" || len(operation.Trains) != 2 || operation.Trains[0].TrainNumber != "0001" || operation.Trains[1].TrainNumber != "0002" {
			t.Fatalf("unexpected operation: %+v", operation)
		}
		points := operation.Trains[0].Train.Points
		if len(points) != 3 {
			t.Fatalf("unexpected points: %+v", points)
		}
		if len(got.Trains[0].Points) != 3 || got.Trains[0].Points[0].TimetablePointID != points[0].TimetablePointID {
			t.Fatalf("timetable and operation do not refer to the same train points: %+v", got.Trains[0])
		}
		ids := make(map[int64]bool)
		for i, point := range points {
			if point.SequenceIndex != i || point.TimetablePointID <= 0 || ids[point.TimetablePointID] {
				t.Fatalf("unexpected point order or generated ID: %+v", points)
			}
			ids[point.TimetablePointID] = true
		}
		if points[0].ArrivalSeconds != nil || points[0].DepartureSeconds == nil || *points[0].DepartureSeconds != 0 || points[1].StationID != nil || points[2].ArrivalSeconds == nil || *points[2].ArrivalSeconds != 90600 || points[2].DepartureSeconds != nil {
			t.Fatalf("nullable station or operating-day times were not preserved: %+v", points)
		}
		if len(points[0].Routes) != 2 || points[0].Routes[0].ControlStationID != "NT01" || points[0].Routes[1].ControlStationID != "NT05" {
			t.Fatalf("route order or composite equipment reference was not preserved: %+v", points[0].Routes)
		}
	}
	count(t, db, &timetable.TimetableOperation{}, 2)
	count(t, db, &timetable.TimetableTrain{}, 2)
	count(t, db, &timetable.TimetablePoint{}, 3)
	var links int64
	if err := db.Table("timetable_operation_links").Count(&links).Error; err != nil || links != 2 {
		t.Fatalf("automatic many2many links: count=%d, error=%v", links, err)
	}
	var alternative timetable.TimetableOperation
	if err := db.Preload("Trains", ordered).First(&alternative, "operation_number = ?", "B").Error; err != nil {
		t.Fatal(err)
	}
	if len(alternative.Trains) != 1 || alternative.Trains[0].TrainNumber != "0001" || alternative.Trains[0].SequenceIndex != 5 {
		t.Fatalf("shared train lost operation-specific order: %+v", alternative)
	}
}

func TestDeleteOperationPreservesTrainDefinitions(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	timetables := seed(t, db)
	if err := db.Delete(&timetable.TimetableOperation{}, "operation_number = ?", "A").Error; err != nil {
		t.Fatal(err)
	}
	count(t, db, &timetable.TimetableOperation{}, 1)
	count(t, db, &timetable.TimetableOperationTrain{}, 1)
	count(t, db, &timetable.TimetableTrain{}, 2)
	count(t, db, &timetable.TimetablePoint{}, 3)
	count(t, db, &timetable.TimetablePointRoute{}, 2)
	for _, expected := range timetables {
		got := load(t, db, expected.ID)
		if len(got.Operations) != 0 {
			t.Fatalf("deleted operation is still linked: %+v", got)
		}
		if len(got.Trains) != 2 || got.Trains[0].TrainNumber != "0001" || got.Trains[1].TrainNumber != "0002" || len(got.Trains[0].Points) != 3 {
			t.Fatalf("deleting the operation removed timetable train membership: %+v", got.Trains)
		}
	}
	var operation timetable.TimetableOperation
	if err := db.Preload("Trains.Train.Points").First(&operation, "operation_number = ?", "B").Error; err != nil || len(operation.Trains) != 1 || len(operation.Trains[0].Train.Points) != 3 {
		t.Fatalf("other operation lost its shared train: %+v, error=%v", operation, err)
	}
}

func TestDeleteTimetablePreservesSharedOperations(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	timetables := seed(t, db)
	if err := db.Delete(&timetable.Timetable{}, "id = ?", timetables[0].ID).Error; err != nil {
		t.Fatal(err)
	}
	if got := load(t, db, timetables[1].ID); len(got.Operations) != 1 || len(got.Operations[0].Trains) != 2 || len(got.Trains) != 2 {
		t.Fatalf("deleting one timetable changed the other: %+v", got)
	}
	var remainingLinks int64
	if err := db.Table("timetable_train_links").Where("timetable_id = ?", timetables[0].ID).Count(&remainingLinks).Error; err != nil || remainingLinks != 0 {
		t.Fatalf("deleted timetable still has train links: count=%d, error=%v", remainingLinks, err)
	}
	count(t, db, &timetable.TimetableOperation{}, 2)
	count(t, db, &timetable.TimetableTrain{}, 2)
}

func TestTimetablePersistsTrainsWithoutOperations(t *testing.T) {
	path := filepath.Join(t.TempDir(), "timetable.sqlite")
	db := openDatabase(t, path)
	create(t, db, &timetable.TrainType{TrainTypeID: "local", DisplayName: "各停"})
	create(t, db, &timetable.TrackCircuit{TrackCircuitID: "origin"})
	create(t, db, &timetable.Route{ControlStationID: "NT01", RouteID: "1R"})
	draft := timetable.Timetable{
		ID:          "draft",
		DisplayName: "運用作成前の平日ダイヤ",
		Trains: []timetable.TimetableTrain{{
			TrainNumber: "0001", TrainTypeID: "local",
			Points: []timetable.TimetablePoint{{
				TrackCircuitID: "origin", Handling: timetable.HandlingStop, DepartureSeconds: intPointer(3600),
				Routes: []timetable.TimetablePointRoute{{ControlStationID: "NT01", RouteID: "1R"}},
			}},
		}},
	}
	create(t, db, &draft)
	// 運用が未作成でも、同じ列車を別のダイヤから共有できる。
	holiday := timetable.Timetable{
		ID:          "holiday",
		DisplayName: "休日",
		Trains:      []timetable.TimetableTrain{{TrainNumber: "0001"}},
	}
	create(t, db.Omit("Trains.*"), &holiday)
	sqlDB, _ := db.DB()
	if err := sqlDB.Close(); err != nil {
		t.Fatal(err)
	}
	db = openDatabase(t, path)
	for _, id := range []string{draft.ID, holiday.ID} {
		got := load(t, db, id)
		if len(got.Operations) != 0 || len(got.Trains) != 1 {
			t.Fatalf("unassigned train was not saved in timetable: %+v", got)
		}
		train := got.Trains[0]
		if train.TrainNumber != "0001" || train.TrainType.DisplayName != "各停" || len(train.Points) != 1 {
			t.Fatalf("unassigned train definition was not loaded: %+v", train)
		}
		point := train.Points[0]
		if point.DepartureSeconds == nil || *point.DepartureSeconds != 3600 || len(point.Routes) != 1 || point.Routes[0].RouteID != "1R" {
			t.Fatalf("unassigned train lost its times or routes: %+v", point)
		}
	}
	count(t, db, &timetable.TimetableOperation{}, 0)
	count(t, db, &timetable.TimetableOperationTrain{}, 0)
	count(t, db, &timetable.TimetableTrain{}, 1)
	// 既存列車への関連だけを保存する場合も、存在しない列番は拒否する。
	invalid := timetable.Timetable{
		ID:          "invalid",
		DisplayName: "不正な参照",
		Trains:      []timetable.TimetableTrain{{TrainNumber: "missing"}},
	}
	if err := db.Omit("Trains.*").Create(&invalid).Error; err == nil {
		t.Fatal("linked a timetable to a missing train")
	}
	count(t, db, &timetable.Timetable{}, 2)

	// 一方のダイヤから所属を外しても、列車本体と他ダイヤへの所属は維持する。
	train := timetable.TimetableTrain{TrainNumber: "0001"}
	if err := db.Model(&draft).Association("Trains").Delete(&train); err != nil {
		t.Fatal(err)
	}
	if got := load(t, db, draft.ID); len(got.Trains) != 0 {
		t.Fatalf("train is still linked to the edited timetable: %+v", got)
	}
	if got := load(t, db, holiday.ID); len(got.Trains) != 1 || len(got.Trains[0].Points) != 1 {
		t.Fatalf("removing membership changed the other timetable: %+v", got)
	}
	count(t, db, &timetable.TimetableTrain{}, 1)
	count(t, db, &timetable.TimetablePoint{}, 1)
	count(t, db, &timetable.TimetablePointRoute{}, 1)
}

func TestModelsRejectInvalidReferencesAndOrder(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	timetables := seed(t, db)
	pointID := load(t, db, timetables[0].ID).Operations[0].Trains[0].Train.Points[0].TimetablePointID
	cases := []struct {
		name  string
		value any
	}{
		{"duplicate operation train", &timetable.TimetableOperationTrain{OperationNumber: "A", TrainNumber: "0001", SequenceIndex: 2}},
		{"duplicate operation sequence", &timetable.TimetableOperationTrain{OperationNumber: "B", TrainNumber: "0002", SequenceIndex: 5}},
		{"negative operation sequence", &timetable.TimetableOperationTrain{OperationNumber: "B", TrainNumber: "0002", SequenceIndex: -1}},
		{"missing operation", &timetable.TimetableOperationTrain{OperationNumber: "missing", TrainNumber: "0002"}},
		{"missing train", &timetable.TimetableOperationTrain{OperationNumber: "B", TrainNumber: "missing"}},
		{"duplicate point sequence", &timetable.TimetablePoint{TrainNumber: "0001", SequenceIndex: 0, TrackCircuitID: "between"}},
		{"negative point sequence", &timetable.TimetablePoint{TrainNumber: "0002", SequenceIndex: -1, TrackCircuitID: "between"}},
		{"point missing train", &timetable.TimetablePoint{TrainNumber: "missing", TrackCircuitID: "between"}},
		{"missing station", &timetable.TimetablePoint{TrainNumber: "0002", TrackCircuitID: "between", StationID: stringPointer("missing")}},
		{"missing circuit", &timetable.TimetablePoint{TrainNumber: "0002", TrackCircuitID: "missing"}},
		{"invalid handling", &timetable.TimetablePoint{TrainNumber: "0002", TrackCircuitID: "between", Handling: "invalid"}},
		{"negative arrival", &timetable.TimetablePoint{TrainNumber: "0002", TrackCircuitID: "between", ArrivalSeconds: intPointer(-1)}},
		{"negative departure", &timetable.TimetablePoint{TrainNumber: "0002", TrackCircuitID: "between", DepartureSeconds: intPointer(-1)}},
		{"arrival after departure", &timetable.TimetablePoint{TrainNumber: "0002", TrackCircuitID: "between", ArrivalSeconds: intPointer(100), DepartureSeconds: intPointer(99)}},
		{"missing train type", &timetable.TimetableTrain{TrainNumber: "0003", TrainTypeID: "missing"}},
		{"blank train number", &timetable.TimetableTrain{TrainNumber: " ", TrainTypeID: "local"}},
		{"blank operation number", &timetable.TimetableOperation{OperationNumber: " "}},
		{"empty timetable ID", &timetable.Timetable{DisplayName: "invalid"}},
		{"blank timetable ID", &timetable.Timetable{ID: "  ", DisplayName: "invalid"}},
		{"duplicate timetable ID", &timetable.Timetable{ID: "weekday", DisplayName: "duplicate"}},
		{"negative revision", &timetable.Timetable{ID: "invalid-revision", DisplayName: "invalid", Revision: -1}},
		{"missing route station", &timetable.TimetablePointRoute{TimetablePointID: pointID, SequenceIndex: 2, ControlStationID: "missing", RouteID: "1R"}},
		{"missing route", &timetable.TimetablePointRoute{TimetablePointID: pointID, SequenceIndex: 2, ControlStationID: "NT01", RouteID: "missing"}},
		{"missing point", &timetable.TimetablePointRoute{TimetablePointID: pointID + 1000, ControlStationID: "NT01", RouteID: "1R"}},
		{"duplicate point route sequence", &timetable.TimetablePointRoute{TimetablePointID: pointID, ControlStationID: "NT01", RouteID: "1R"}},
		{"negative route sequence", &timetable.TimetablePointRoute{TimetablePointID: pointID, SequenceIndex: -1, ControlStationID: "NT01", RouteID: "1R"}},
	}
	for _, test := range cases {
		t.Run(test.name, func(t *testing.T) {
			if err := db.Create(test.value).Error; err == nil {
				t.Fatalf("invalid %T was saved: %+v", test.value, test.value)
			}
		})
	}
	// 参照されている列車・設備を単独で削除して関連を壊せない。
	if err := db.Delete(&timetable.TimetableTrain{}, "train_number = ?", "0001").Error; err == nil {
		t.Fatal("deleted a train still referenced by operations")
	}
	if err := db.Delete(&timetable.Route{}, "control_station_id = ? AND route_id = ?", "NT01", "1R").Error; err == nil {
		t.Fatal("deleted a route still referenced by timetable points")
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
