package api

import (
	"encoding/json"
	"net/http"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func getRunningPattern(t *testing.T, handler http.Handler, id string) timetable.RunningPattern {
	t.Helper()
	var result timetable.RunningPattern
	response := trainTypeRequest(t, handler, http.MethodGet, "/api/running-patterns/"+id, "", http.StatusOK)
	if err := json.Unmarshal(response.Body.Bytes(), &result); err != nil {
		t.Fatal(err)
	}
	return result
}

func TestRunningPatternCRUD(t *testing.T) {
	db, handler := trainTestServer(t)
	if err := db.Create(&timetable.Station{StationID: "C", DisplayName: "C駅"}).Error; err != nil {
		t.Fatal(err)
	}
	if err := db.Create(&timetable.TrackCircuit{TrackCircuitID: "MID"}).Error; err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	beforeTrain := getTrain(t, handler, "0001")
	trainTypeRequest(t, handler, http.MethodGet, "/api/running-patterns", "", http.StatusOK)
	body := `{"id":"p1","trainTypeId":"local","displayName":"A→B","points":[
 {"sequenceIndex":2,"stationId":"B","trackCircuitId":"B1T","handling":"stop","arrivalOffsetSeconds":600},
 {"sequenceIndex":0,"stationId":"A","trackCircuitId":"A1T","handling":"stop","departureOffsetSeconds":0},
 {"sequenceIndex":1,"stationId":"C","trackCircuitId":"MID","handling":"pass"}]}`
	trainTypeRequest(t, handler, http.MethodPost, "/api/running-patterns", body, http.StatusCreated)
	before := getRunningPattern(t, handler, "p1")
	if len(before.Points) != 3 || before.Points[0].SequenceIndex != 0 || *before.Points[0].DepartureOffsetSeconds != 0 || before.Points[1].DepartureOffsetSeconds != nil || before.Points[1].Handling != timetable.HandlingPass || *before.Points[2].ArrivalOffsetSeconds != 600 {
		t.Fatalf("unexpected pattern: %+v", before)
	}
	for _, p := range before.Points {
		if p.RunningPatternID != "p1" {
			t.Fatal("wrong point parent")
		}
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/running-patterns", body, http.StatusConflict)
	trainTypeRequest(t, handler, http.MethodGet, "/api/running-patterns/missing", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/running-patterns/missing", `{"points":[]}`, http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodDelete, "/api/running-patterns/missing", "", http.StatusNotFound)

	for _, edit := range []func(*timetable.RunningPattern){
		func(p *timetable.RunningPattern) { p.Points = []timetable.RunningPatternPoint{} },
		func(p *timetable.RunningPattern) { v := 1; p.Points[0].DepartureOffsetSeconds = &v },
		func(p *timetable.RunningPattern) { p.Points[1].StationID = "missing" },
		func(p *timetable.RunningPattern) { p.Points[1].TrackCircuitID = "missing" },
		func(p *timetable.RunningPattern) { p.Points[1].Handling = timetable.HandlingUnspecified },
		func(p *timetable.RunningPattern) { p.Points[1].SequenceIndex = 0 },
		func(p *timetable.RunningPattern) { v := -1; p.Points[1].DepartureOffsetSeconds = &v },
		func(p *timetable.RunningPattern) { v := 700; p.Points[1].DepartureOffsetSeconds = &v },
		func(p *timetable.RunningPattern) { v := 0; p.Points[1].ArrivalOffsetSeconds = &v },
		func(p *timetable.RunningPattern) { p.Points[0].Handling = timetable.HandlingPass },
		func(p *timetable.RunningPattern) { v := 700; p.Points[2].DepartureOffsetSeconds = &v },
		func(p *timetable.RunningPattern) {
			p.Points[0].TrackCircuitID = "B1T"
			p.Points[2].TrackCircuitID = "A1T"
		},
	} {
		candidate := getRunningPattern(t, handler, "p1")
		edit(&candidate)
		payload, err := json.Marshal(map[string]any{"displayName": "失敗", "points": candidate.Points})
		if err != nil {
			t.Fatal(err)
		}
		trainTypeRequest(t, handler, http.MethodPatch, "/api/running-patterns/p1", string(payload), http.StatusBadRequest)
		if after := getRunningPattern(t, handler, "p1"); !reflect.DeepEqual(after, before) {
			t.Fatal("invalid patch changed pattern")
		}
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/running-patterns/p1", `{"displayName":"変更後"}`, http.StatusNoContent)
	renamed := getRunningPattern(t, handler, "p1")
	if renamed.DisplayName != "変更後" || !reflect.DeepEqual(renamed.Points, before.Points) {
		t.Fatal("name-only patch changed points")
	}
	replacement := `[{"runningPatternId":"injected","sequenceIndex":0,"stationId":"A","trackCircuitId":"A1T","handling":"stop","departureOffsetSeconds":0},{"sequenceIndex":1,"stationId":"B","trackCircuitId":"B1T","handling":"stop","arrivalOffsetSeconds":720}]`
	trainTypeRequest(t, handler, http.MethodPatch, "/api/running-patterns/p1", `{"points":`+replacement+`}`, http.StatusNoContent)
	changed := getRunningPattern(t, handler, "p1")
	if len(changed.Points) != 2 || changed.Points[0].RunningPatternID != "p1" || *changed.Points[1].ArrivalOffsetSeconds != 720 {
		t.Fatalf("replace failed: %+v", changed)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/running-patterns", `{"id":"p2","trainTypeId":"express","displayName":"急行","points":`+replacement+`}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/running-patterns", `{"id":"bad","trainTypeId":"missing","displayName":"失敗","points":`+replacement+`}`, http.StatusBadRequest)
	trainTypeRequest(t, handler, http.MethodGet, "/api/running-patterns/bad", "", http.StatusNotFound)
	for _, id := range []string{"local", "express"} {
		response := trainTypeRequest(t, handler, http.MethodGet, "/api/running-patterns?trainTypeId="+id, "", http.StatusOK)
		var rows []timetable.RunningPattern
		if err := json.Unmarshal(response.Body.Bytes(), &rows); err != nil || len(rows) != 1 || rows[0].TrainTypeID != id {
			t.Fatalf("wrong type filter: %s, %v", response.Body, err)
		}
	}
	trainTypeRequest(t, handler, http.MethodDelete, "/api/running-patterns/p1", "", http.StatusNoContent)
	trainTypeRequest(t, handler, http.MethodGet, "/api/running-patterns/p1", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodDelete, "/api/train-types/express", "", http.StatusNoContent)
	var count int64
	if err := db.Model(&timetable.RunningPatternPoint{}).Count(&count).Error; err != nil || count != 0 {
		t.Fatalf("cascade left points: %d, %v", count, err)
	}
	if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, beforeTrain) {
		t.Fatal("pattern edits changed a train")
	}
}
