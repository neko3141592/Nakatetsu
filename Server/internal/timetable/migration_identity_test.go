package timetable_test

import (
	"database/sql"
	"path/filepath"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func legacyIdentityDB(t *testing.T, invalid bool) string {
	t.Helper()
	path := filepath.Join(t.TempDir(), "legacy.db")
	db, err := sql.Open("sqlite3", path)
	if err != nil {
		t.Fatal(err)
	}
	defer db.Close()
	_, err = db.Exec(`
 CREATE TABLE timetables(id TEXT PRIMARY KEY,display_name TEXT NOT NULL,revision INTEGER NOT NULL);
 CREATE TABLE timetable_trains(train_number TEXT PRIMARY KEY,train_type_id TEXT,origin_station_id TEXT,destination_station_id TEXT);
 CREATE TABLE timetable_operations(operation_number TEXT PRIMARY KEY);
 CREATE TABLE timetable_operation_trains(operation_number TEXT,train_number TEXT,sequence_index INTEGER);
 CREATE TABLE timetable_train_links(timetable_id TEXT,train_number TEXT);
 CREATE TABLE timetable_operation_links(timetable_id TEXT,operation_number TEXT);
 CREATE TABLE timetable_points(timetable_point_id INTEGER PRIMARY KEY AUTOINCREMENT,train_number TEXT,sequence_index INTEGER,section TEXT,station_id TEXT,track_circuit_id TEXT,handling TEXT,arrival_seconds INTEGER,departure_seconds INTEGER);
 CREATE TABLE timetable_point_routes(timetable_point_id INTEGER,sequence_index INTEGER,control_station_id TEXT,route_id TEXT);
 CREATE TABLE train_types(train_type_id TEXT PRIMARY KEY,display_name TEXT NOT NULL,color TEXT);
 CREATE TABLE stations(station_id TEXT PRIMARY KEY,display_name TEXT);
 CREATE TABLE track_circuits(track_circuit_id TEXT PRIMARY KEY,display_name TEXT);
 CREATE TABLE routes(control_station_id TEXT,route_id TEXT,PRIMARY KEY(control_station_id,route_id));
 INSERT INTO timetables VALUES('weekday','平日',3),('holiday','休日',2);
 INSERT INTO train_types VALUES('local','各停','#858585');
 INSERT INTO stations VALUES('A','A駅'),('B','B駅');
 INSERT INTO track_circuits VALUES('A1T','1番'),('B1T','1番');
 INSERT INTO routes VALUES('A','R');
 INSERT INTO timetable_trains VALUES('5101','local','A','B'),('5103','local','A','B');
 INSERT INTO timetable_points VALUES(10,'5101',0,'depot_exit','A','A1T','stop',NULL,0),(11,'5101',1,'service','B','B1T','stop',90000,NULL),(12,'5103',0,'service','A','A1T','stop',NULL,123);
 INSERT INTO timetable_point_routes VALUES(10,0,'A','R');
 INSERT INTO timetable_operations VALUES('01'),('02'),('orphan');
 INSERT INTO timetable_operation_trains VALUES('01','5101',0),('02','5101',0),('orphan','5103',0);
 INSERT INTO timetable_operation_links VALUES('weekday','01'),('weekday','02'),('holiday','01');
 INSERT INTO timetable_train_links VALUES('holiday','5101');
 `)
	if err != nil {
		t.Fatal(err)
	}
	if invalid {
		if _, err := db.Exec("UPDATE timetable_points SET track_circuit_id = 'missing' WHERE timetable_point_id=10"); err != nil {
			t.Fatal(err)
		}
	}
	return path
}
func TestLegacyIdentityResetPreservesMasters(t *testing.T) {
	path := legacyIdentityDB(t, false)
	db, err := timetable.OpenDatabase(path)
	if err != nil {
		t.Fatal(err)
	}
	sqlDB, _ := db.DB()
	defer sqlDB.Close()
	for _, model := range []any{&timetable.Timetable{}, &timetable.TimetableTrain{}, &timetable.TimetablePoint{}, &timetable.TimetablePointRoute{}, &timetable.TimetableOperationPlan{}, &timetable.TimetableOperation{}, &timetable.TimetableOperationTrain{}} {
		count(t, db, model, 0)
	}
	count(t, db, &timetable.TrainType{}, 1)
	count(t, db, &timetable.Station{}, 2)
	count(t, db, &timetable.TrackCircuit{}, 2)
	count(t, db, &timetable.Route{}, 1)
	create(t, db, &timetable.Timetable{ID: "new", DisplayName: "新規", Trains: []timetable.TimetableTrain{{TrainNumber: "5101", TrainTypeID: "local", Points: []timetable.TimetablePoint{{TrackCircuitID: "A1T", DepartureSeconds: intPointer(0)}}}}})
	before := load(t, db, "new")
	for range 2 {
		if err := timetable.MigrateModels(db); err != nil {
			t.Fatal(err)
		}
		if !reflect.DeepEqual(before, load(t, db, "new")) {
			t.Fatal("new data was reset on subsequent startup")
		}
	}
	if db.Migrator().HasTable("timetable_train_links") || db.Migrator().HasTable("timetable_operation_links") {
		t.Fatal("legacy link tables remain")
	}
	var violations []struct{ Table string }
	if err := db.Raw("PRAGMA foreign_key_check").Scan(&violations).Error; err != nil || len(violations) > 0 {
		t.Fatalf("foreign keys: %v %v", violations, err)
	}
	backups, _ := filepath.Glob(path + ".*bak")
	if len(backups) != 0 {
		t.Fatal("unexpected backup")
	}
}
