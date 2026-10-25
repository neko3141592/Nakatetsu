package timetable_test

import (
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func TestMigrateTrainEndpoints(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "legacy.db"))
	seed(t, db)
	before := load(t, db, "weekday")
	if err := db.Exec("PRAGMA foreign_keys = OFF").Error; err != nil {
		t.Fatal(err)
	}
	for _, relation := range []string{"OriginStation", "DestinationStation"} {
		if err := db.Migrator().DropConstraint(&timetable.TimetableTrain{}, relation); err != nil {
			t.Fatal(err)
		}
	}
	for _, column := range []string{"origin_station_id", "destination_station_id"} {
		if err := db.Migrator().DropConstraint(&timetable.TimetableTrain{}, "chk_timetable_trains_"+column); err != nil {
			t.Fatal(err)
		}
		if err := db.Migrator().DropColumn(&timetable.TimetableTrain{}, column); err != nil {
			t.Fatal(err)
		}
	}
	if err := db.Exec("PRAGMA foreign_keys = ON").Error; err != nil {
		t.Fatal(err)
	}
	for range 2 {
		if err := timetable.MigrateModels(db); err != nil {
			t.Fatal(err)
		}
		if after := load(t, db, "weekday"); !reflect.DeepEqual(after, before) {
			t.Fatal("adding train endpoints changed existing data")
		}
	}
	for _, train := range []timetable.TimetableTrain{
		{TrainNumber: "9001", OriginStationID: "missing"},
		{TrainNumber: "9002", DestinationStationID: "missing"},
	} {
		if err := db.Create(&train).Error; err == nil {
			t.Fatal("missing endpoint station was accepted")
		}
	}
}

func TestMigrateOptionalTrainType(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "legacy.db"))
	var schema string
	if err := db.Raw("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'timetable_trains'").Row().Scan(&schema); err != nil {
		t.Fatal(err)
	}
	legacy := strings.Replace(schema, "`train_type_id` text DEFAULT null", "`train_type_id` text NOT NULL", 1)
	if legacy == schema {
		t.Fatal("fresh schema does not allow an unassigned train type")
	}
	if err := db.Migrator().DropTable(&timetable.TimetableTrain{}); err != nil {
		t.Fatal(err)
	}
	if err := db.Exec(legacy).Error; err != nil {
		t.Fatal(err)
	}
	seed(t, db)
	before := load(t, db, "weekday")
	for range 2 {
		if err := timetable.MigrateModels(db); err != nil {
			t.Fatal(err)
		}
		if after := load(t, db, "weekday"); !reflect.DeepEqual(after, before) {
			t.Fatal("migration changed existing trains or their relationships")
		}
	}
	create(t, db, &timetable.TimetableTrain{TrainNumber: "9001"})
	if err := db.Create(&timetable.TimetableTrain{TrainNumber: "9002", TrainTypeID: "missing"}).Error; err == nil {
		t.Fatal("migration lost the train type foreign key")
	}
}

func TestMigrateRunningTimeDeleteConstraint(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "legacy.db"))
	var schema string
	if err := db.Raw("SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'station_running_times'").Row().Scan(&schema); err != nil {
		t.Fatal(err)
	}
	legacy := strings.Replace(schema, "ON DELETE CASCADE", "ON DELETE RESTRICT", 1)
	if legacy == schema {
		t.Fatal("fresh schema does not use CASCADE")
	}
	if err := db.Migrator().DropTable(&timetable.StationRunningTime{}); err != nil {
		t.Fatal(err)
	}
	if err := db.Exec(legacy).Error; err != nil {
		t.Fatal(err)
	}
	create(t, db, &timetable.TrainType{TrainTypeID: "local", DisplayName: "普通"})
	create(t, db, &[]timetable.Station{{StationID: "A", DisplayName: "A駅"}, {StationID: "B", DisplayName: "B駅"}})
	create(t, db, &timetable.StationRunningTime{TrainTypeID: "local", FromStationID: "A", ToStationID: "B", RunningSeconds: 60})
	for range 2 {
		if err := timetable.MigrateModels(db); err != nil {
			t.Fatal(err)
		}
		count(t, db, &timetable.StationRunningTime{}, 1)
	}
	if err := db.Delete(&timetable.TrainType{}, "train_type_id = ?", "local").Error; err != nil {
		t.Fatal(err)
	}
	count(t, db, &timetable.StationRunningTime{}, 0)
	if err := db.Create(&timetable.StationRunningTime{TrainTypeID: "missing", FromStationID: "A", ToStationID: "B", RunningSeconds: 60}).Error; err == nil {
		t.Fatal("foreign key enforcement was lost after migration")
	}
}
