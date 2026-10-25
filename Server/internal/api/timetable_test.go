package api

import (
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func TestPostTimetableDuplicateReturnsConflict(t *testing.T) {
	db, err := timetable.OpenDatabase(filepath.Join(t.TempDir(), "timetable.db"))
	if err != nil {
		t.Fatal(err)
	}
	sqlDB, err := db.DB()
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = sqlDB.Close() })
	server := &ApiServer{DB: db}
	handler := server.Routes()
	for _, request := range []struct {
		body string
		want int
	}{
		{`{"id":"weekday","displayName":"平日"}`, http.StatusCreated},
		{`{"id":"weekday","displayName":"変更"}`, http.StatusConflict},
	} {
		r := httptest.NewRequest(http.MethodPost, "/api/timetable/", strings.NewReader(request.body))
		w := httptest.NewRecorder()
		handler.ServeHTTP(w, r)
		if w.Code != request.want {
			t.Fatalf("status=%d, want=%d, body=%s", w.Code, request.want, w.Body.String())
		}
	}
	var saved timetable.Timetable
	if err := db.First(&saved, "id = ?", "weekday").Error; err != nil {
		t.Fatal(err)
	}
	if saved.DisplayName != "平日" {
		t.Fatalf("duplicate request overwrote the existing timetable: %+v", saved)
	}
}

func TestPatchTimetable(t *testing.T) {
	db, err := timetable.OpenDatabase(filepath.Join(t.TempDir(), "timetable.db"))
	if err != nil {
		t.Fatal(err)
	}
	sqlDB, err := db.DB()
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = sqlDB.Close() })
	if err := db.Create(&[]timetable.Timetable{
		{ID: "weekday", DisplayName: "平日", Revision: 7},
		{ID: "holiday", DisplayName: "休日", Revision: 4},
	}).Error; err != nil {
		t.Fatal(err)
	}
	server := &ApiServer{DB: db}
	handler := server.Routes()
	for _, patch := range []struct {
		body        string
		displayName string
		revision    int64
	}{
		{`{"displayName":"平日改正"}`, "平日改正", 7},
		{`{"revision":0}`, "平日改正", 0},
		{`{"revision":0}`, "平日改正", 0},
		{`{"displayName":"","revision":2}`, "", 2},
	} {
		w := httptest.NewRecorder()
		handler.ServeHTTP(w, httptest.NewRequest(http.MethodPatch, "/api/timetable/weekday", strings.NewReader(patch.body)))
		if w.Code != http.StatusNoContent || w.Body.Len() != 0 {
			t.Fatalf("status=%d, body=%s", w.Code, w.Body.String())
		}
		var saved timetable.Timetable
		if err := db.First(&saved, "id = ?", "weekday").Error; err != nil {
			t.Fatal(err)
		}
		if saved.DisplayName != patch.displayName || saved.Revision != patch.revision {
			t.Fatalf("unexpected saved timetable: %+v", saved)
		}
	}
	for _, body := range []string{
		`{`, `{}`, `null`, `{"revision":-1}`, `{"revision":"2"}`,
		`{"displayName":null}`, `{"revision":9}{}`, `{"id":"holiday"}`,
		`{"revision":9,"trains":[]}`, `{"operations":[]}`,
	} {
		w := httptest.NewRecorder()
		handler.ServeHTTP(w, httptest.NewRequest(http.MethodPatch, "/api/timetable/weekday", strings.NewReader(body)))
		if w.Code != http.StatusBadRequest {
			t.Fatalf("body=%s: status=%d, response=%s", body, w.Code, w.Body.String())
		}
	}
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, httptest.NewRequest(http.MethodPatch, "/api/timetable/missing", strings.NewReader(`{"displayName":"新規"}`)))
	if w.Code != http.StatusNotFound {
		t.Fatalf("missing timetable: status=%d, body=%s", w.Code, w.Body.String())
	}
	var saved []timetable.Timetable
	if err := db.Order("id ASC").Find(&saved).Error; err != nil {
		t.Fatal(err)
	}
	if len(saved) != 2 || saved[0].ID != "holiday" || saved[0].DisplayName != "休日" || saved[0].Revision != 4 || saved[1].ID != "weekday" || saved[1].DisplayName != "" || saved[1].Revision != 2 {
		t.Fatalf("unexpected timetable changes: %+v", saved)
	}
}

func TestDeleteTimetablePreservesDefinitions(t *testing.T) {
	db, err := timetable.OpenDatabase(filepath.Join(t.TempDir(), "timetable.db"))
	if err != nil {
		t.Fatal(err)
	}
	sqlDB, err := db.DB()
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = sqlDB.Close() })
	if err := db.Create(&timetable.TrainType{TrainTypeID: "local", DisplayName: "普通"}).Error; err != nil {
		t.Fatal(err)
	}
	trains := []timetable.TimetableTrain{
		{TrainNumber: "0001", TrainTypeID: "local"},
		{TrainNumber: "0002", TrainTypeID: "local"},
	}
	operations := []timetable.TimetableOperation{
		{OperationNumber: "01", Trains: []timetable.TimetableOperationTrain{{TrainNumber: "0001"}}},
		{OperationNumber: "02", Trains: []timetable.TimetableOperationTrain{{TrainNumber: "0002"}}},
	}
	for _, plan := range []timetable.Timetable{
		{ID: "weekday", DisplayName: "平日", Trains: trains, Operations: operations},
		{ID: "holiday", DisplayName: "休日", Trains: trains[:1], Operations: operations[:1]},
	} {
		if err := db.Create(&plan).Error; err != nil {
			t.Fatal(err)
		}
	}
	server := &ApiServer{DB: db}
	handler := server.Routes()
	for _, request := range []struct {
		id   string
		want int
	}{
		{"missing", http.StatusNotFound},
		{"weekday", http.StatusNoContent},
		{"weekday", http.StatusNotFound},
	} {
		w := httptest.NewRecorder()
		handler.ServeHTTP(w, httptest.NewRequest(http.MethodDelete, "/api/timetable/"+request.id, nil))
		if w.Code != request.want || (w.Code == http.StatusNoContent && w.Body.Len() != 0) {
			t.Fatalf("delete %s: status=%d, body=%s", request.id, w.Code, w.Body.String())
		}
	}
	for table, want := range map[string]int64{
		"timetables":                 1,
		"timetable_trains":           2,
		"timetable_operations":       2,
		"timetable_operation_trains": 2,
	} {
		var count int64
		if err := db.Table(table).Count(&count).Error; err != nil {
			t.Fatal(err)
		}
		if count != want {
			t.Fatalf("%s: count=%d, want=%d", table, count, want)
		}
	}
	for _, table := range []string{"timetable_train_links", "timetable_operation_links"} {
		var count int64
		if err := db.Table(table).Where("timetable_id = ?", "weekday").Count(&count).Error; err != nil {
			t.Fatal(err)
		}
		if count != 0 {
			t.Fatalf("deleted timetable still has links in %s", table)
		}
	}
	var holiday timetable.Timetable
	if err := db.Preload("Trains").Preload("Operations").First(&holiday, "id = ?", "holiday").Error; err != nil {
		t.Fatal(err)
	}
	if len(holiday.Trains) != 1 || holiday.Trains[0].TrainNumber != "0001" || len(holiday.Operations) != 1 || holiday.Operations[0].OperationNumber != "01" {
		t.Fatalf("other timetable links were changed: %+v", holiday)
	}
}
