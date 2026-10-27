package api

import (
	"encoding/json"
	"net/http"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func TestGetStation(t *testing.T) {
	db, handler := trainTypeTestServer(t)
	for _, rows := range []any{
		&[]timetable.Station{{StationID: "S1", DisplayName: "駅1"}, {StationID: "S2", DisplayName: "駅2"}, {StationID: "empty", DisplayName: "番線未登録"}},
		&[]timetable.TrackCircuit{{TrackCircuitID: "T1"}, {TrackCircuitID: "T2"}},
		&[]timetable.StationTrack{
			{StationID: "S1", TrackID: "2", DisplayName: "2番線", TrackCircuitID: "T2"},
			{StationID: "S2", TrackID: "1", DisplayName: "別駅1番線", TrackCircuitID: "T2"},
			{StationID: "S1", TrackID: "1", DisplayName: "1番線", TrackCircuitID: "T1"},
		},
	} {
		if err := db.Create(rows).Error; err != nil {
			t.Fatal(err)
		}
	}
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/stations/S1", "", http.StatusOK)
	if w.Header().Get("Content-Type") != "application/json; charset=utf-8" {
		t.Fatalf("unexpected Content-Type: %s", w.Header().Get("Content-Type"))
	}
	var got struct {
		timetable.Station
		Tracks []timetable.StationTrack `json:"tracks"`
	}
	if err := json.Unmarshal(w.Body.Bytes(), &got); err != nil {
		t.Fatal(err)
	}
	want := []timetable.StationTrack{
		{StationID: "S1", TrackID: "1", DisplayName: "1番線", TrackCircuitID: "T1"},
		{StationID: "S1", TrackID: "2", DisplayName: "2番線", TrackCircuitID: "T2"},
	}
	if got.StationID != "S1" || got.DisplayName != "駅1" || !reflect.DeepEqual(got.Tracks, want) {
		t.Fatalf("unexpected station: %+v", got)
	}
	w = trainTypeRequest(t, handler, http.MethodGet, "/api/stations/empty", "", http.StatusOK)
	if err := json.Unmarshal(w.Body.Bytes(), &got); err != nil || got.Tracks == nil || len(got.Tracks) != 0 {
		t.Fatalf("station without tracks should return []: %s, error=%v", w.Body.String(), err)
	}
	trainTypeRequest(t, handler, http.MethodGet, "/api/stations/missing", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodGet, "/api/stations/%20", "", http.StatusBadRequest)
	trainTypeRequest(t, handler, http.MethodGet, "/api/stations", "", http.StatusOK)
	sqlDB, err := db.DB()
	if err != nil {
		t.Fatal(err)
	}
	if err := sqlDB.Close(); err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodGet, "/api/stations/S1", "", http.StatusInternalServerError)
}
