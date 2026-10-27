package timetable_test

import (
	"database/sql"
	"path/filepath"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func TestMigrateTrainTypeColor(t *testing.T) {
	path := filepath.Join(t.TempDir(), "old.sqlite")
	old, err := sql.Open("sqlite3", path)
	if err != nil {
		t.Fatal(err)
	}
	if _, err := old.Exec(`CREATE TABLE train_types (train_type_id TEXT PRIMARY KEY NOT NULL, display_name TEXT NOT NULL);
		INSERT INTO train_types VALUES ('express', '急行')`); err != nil {
		_ = old.Close()
		t.Fatal(err)
	}
	if err := old.Close(); err != nil {
		t.Fatal(err)
	}
	db := openDatabase(t, path)
	var got timetable.TrainType
	if err := db.First(&got, "train_type_id = ?", "express").Error; err != nil {
		t.Fatal(err)
	}
	if got.DisplayName != "急行" || got.Color != timetable.DefaultTrainTypeColor {
		t.Fatalf("existing type was not preserved with a default color: %+v", got)
	}
}
