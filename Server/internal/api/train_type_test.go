package api

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
)

func trainTypeTestServer(t *testing.T) (*gorm.DB, http.Handler) {
	t.Helper()
	db, err := timetable.OpenDatabase(filepath.Join(t.TempDir(), "train-types.db"))
	if err != nil {
		t.Fatal(err)
	}
	sqlDB, err := db.DB()
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { _ = sqlDB.Close() })
	server := &ApiServer{DB: db}
	return db, server.Routes()
}

func trainTypeRequest(t *testing.T, handler http.Handler, method, path, body string, status int) *httptest.ResponseRecorder {
	t.Helper()
	w := httptest.NewRecorder()
	handler.ServeHTTP(w, httptest.NewRequest(method, path, strings.NewReader(body)))
	if w.Code != status {
		t.Fatalf("%s %s: status=%d, want=%d, body=%s", method, path, w.Code, status, w.Body.String())
	}
	return w
}

func TestTrainTypeCRUD(t *testing.T) {
	db, handler := trainTypeTestServer(t)
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/train-types", "", http.StatusOK)
	if w.Body.String() != "[]" || !strings.Contains(w.Header().Get("Content-Type"), "application/json") {
		t.Fatalf("unexpected empty list: %s", w.Body.String())
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"local","displayName":"普通"}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"express","displayName":"急行"}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"local","displayName":"重複"}`, http.StatusConflict)
	w = trainTypeRequest(t, handler, http.MethodGet, "/api/train-types", "", http.StatusOK)
	var list []timetable.TrainType
	if err := json.Unmarshal(w.Body.Bytes(), &list); err != nil {
		t.Fatal(err)
	}
	if len(list) != 2 || list[0].TrainTypeID != "express" || list[1].TrainTypeID != "local" || list[1].DisplayName != "普通" {
		t.Fatalf("unexpected train types: %+v", list)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", `{"displayName":"各駅停車"}`, http.StatusNoContent)
	w = trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/local", "", http.StatusOK)
	var saved timetable.TrainType
	if err := json.Unmarshal(w.Body.Bytes(), &saved); err != nil {
		t.Fatal(err)
	}
	if saved.TrainTypeID != "local" || saved.DisplayName != "各駅停車" {
		t.Fatalf("unexpected update: %+v", saved)
	}
	for _, body := range []string{
		`{`, `{}`, `null`, `{"trainTypeId":" ","displayName":"普通"}`,
		`{"trainTypeId":"new","displayName":"　"}`, `{"trainTypeId":1,"displayName":"普通"}`,
		`{"trainTypeId":"new","displayName":"普通","unknown":true}`,
		`{"trainTypeId":"new","displayName":"普通"}{}`,
	} {
		trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", body, http.StatusBadRequest)
	}
	for _, body := range []string{
		`{}`, `{"displayName":null}`, `{"displayName":" "}`, `{"displayName":1}`,
		`{"displayName":"変更","trainTypeId":"new"}`, `{"displayName":"変更"}{}`,
	} {
		trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", body, http.StatusBadRequest)
	}
	trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/missing", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/missing", `{"displayName":"普通"}`, http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodDelete, "/api/train-types/missing", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodDelete, "/api/train-types/express", "", http.StatusNoContent)
	trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/express", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodDelete, "/api/train-types/express", "", http.StatusNotFound)
	if err := db.First(&saved, "train_type_id = ?", "local").Error; err != nil {
		t.Fatal(err)
	}
	if saved.DisplayName != "各駅停車" {
		t.Fatalf("invalid request changed the train type: %+v", saved)
	}
	var count int64
	if err := db.Model(&timetable.TrainType{}).Count(&count).Error; err != nil || count != 1 {
		t.Fatalf("unexpected row count=%d, error=%v", count, err)
	}
}

func TestDeleteTrainTypeCascadeAndRestriction(t *testing.T) {
	db, handler := trainTypeTestServer(t)
	if err := db.Create(&[]timetable.Station{{StationID: "A", DisplayName: "A駅"}, {StationID: "B", DisplayName: "B駅"}}).Error; err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"local","displayName":"普通","runningTimes":[{"fromStationId":"A","toStationId":"B","runningSeconds":60}]}`, http.StatusCreated)
	reference := timetable.TimetableTrain{TrainNumber: "0001", TrainTypeID: "local"}
	if err := db.Create(&reference).Error; err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodDelete, "/api/train-types/local", "", http.StatusConflict)
	trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/local", "", http.StatusOK)
	var count int64
	if err := db.Model(&timetable.StationRunningTime{}).Count(&count).Error; err != nil || count != 1 {
		t.Fatalf("failed deletion changed running times: count=%d, error=%v", count, err)
	}
	if err := db.Delete(&reference).Error; err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodDelete, "/api/train-types/local", "", http.StatusNoContent)
	trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/local", "", http.StatusNotFound)
	if err := db.Model(&timetable.StationRunningTime{}).Count(&count).Error; err != nil || count != 0 {
		t.Fatalf("running times were not deleted: count=%d, error=%v", count, err)
	}
}

func getTrainTypeDetail(t *testing.T, handler http.Handler, id string) trainTypeDetail {
	t.Helper()
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/"+id, "", http.StatusOK)
	var result trainTypeDetail
	if err := json.Unmarshal(w.Body.Bytes(), &result); err != nil {
		t.Fatal(err)
	}
	return result
}

func TestTrainTypeRunningTimes(t *testing.T) {
	db, handler := trainTypeTestServer(t)
	if err := db.Create(&[]timetable.Station{
		{StationID: "A", DisplayName: "A駅"}, {StationID: "B", DisplayName: "B駅"}, {StationID: "C", DisplayName: "C駅"},
	}).Error; err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"local","displayName":"普通","runningTimes":[{"fromStationId":"B","toStationId":"A","runningSeconds":130},{"fromStationId":"A","toStationId":"B","runningSeconds":120}]}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"express","displayName":"急行","runningTimes":[{"fromStationId":"A","toStationId":"C","runningSeconds":80}]}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"local","displayName":"重複","runningTimes":[]}`, http.StatusConflict)
	got := getTrainTypeDetail(t, handler, "local")
	want := []trainTypeRunningTime{{"A", "B", 120}, {"B", "A", 130}}
	if got.DisplayName != "普通" || !reflect.DeepEqual(got.RunningTimes, want) {
		t.Fatalf("unexpected detail: %+v", got)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", `{"displayName":"各駅停車"}`, http.StatusNoContent)
	got = getTrainTypeDetail(t, handler, "local")
	if got.DisplayName != "各駅停車" || !reflect.DeepEqual(got.RunningTimes, want) {
		t.Fatalf("omitted running times were changed: %+v", got)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", `{"displayName":"改正後","runningTimes":[{"fromStationId":"A","toStationId":"C","runningSeconds":90}]}`, http.StatusNoContent)
	before := getTrainTypeDetail(t, handler, "local")
	if before.DisplayName != "改正後" || !reflect.DeepEqual(before.RunningTimes, []trainTypeRunningTime{{"A", "C", 90}}) {
		t.Fatalf("replacement failed: %+v", before)
	}
	for _, body := range []string{
		`{"displayName":"失敗","runningTimes":[{"fromStationId":"A","toStationId":"missing","runningSeconds":60}]}`,
		`{"runningTimes":[{"fromStationId":"A","toStationId":"B","runningSeconds":60},{"fromStationId":"A","toStationId":"B","runningSeconds":70}]}`,
		`{"runningTimes":[{"fromStationId":"A","toStationId":"B","runningSeconds":0}]}`,
		`{"runningTimes":[{"fromStationId":"A","toStationId":"B","runningSeconds":-1}]}`,
		`{"runningTimes":[{"fromStationId":" ","toStationId":"B","runningSeconds":60}]}`,
		`{"runningTimes":[{"fromStationId":"A","toStationId":"B","runningSeconds":60,"trainTypeId":"express"}]}`,
	} {
		trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", body, http.StatusBadRequest)
		if after := getTrainTypeDetail(t, handler, "local"); !reflect.DeepEqual(after, before) {
			t.Fatalf("failed patch changed the detail: %+v", after)
		}
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"invalid","displayName":"失敗","runningTimes":[{"fromStationId":"missing","toStationId":"B","runningSeconds":60}]}`, http.StatusBadRequest)
	trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/invalid", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/missing", `{"runningTimes":[]}`, http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", `{"runningTimes":[]}`, http.StatusNoContent)
	got = getTrainTypeDetail(t, handler, "local")
	if got.DisplayName != "改正後" || got.RunningTimes == nil || len(got.RunningTimes) != 0 {
		t.Fatalf("clear or omitted name failed: %+v", got)
	}
	if other := getTrainTypeDetail(t, handler, "express"); !reflect.DeepEqual(other.RunningTimes, []trainTypeRunningTime{{"A", "C", 80}}) {
		t.Fatalf("another train type was changed: %+v", other)
	}
}
