package api

import (
	"encoding/json"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"testing"
)

func TestTimetableOwnedTrainIsolation(t *testing.T) {
	_, h := trainTestServer(t)
	trainTypeRequest(t, h, "POST", "/api/timetable/", `{"id":"holiday","displayName":"休日"}`, 201)
	var input map[string]any
	json.Unmarshal([]byte(trainBody), &input)
	trainTypeRequest(t, h, "POST", "/api/trains", trainBody, 201)
	input["timetableId"] = "holiday"
	b, _ := json.Marshal(input)
	w := trainTypeRequest(t, h, "POST", "/api/trains", string(b), 201)
	var other timetable.TimetableTrain
	json.Unmarshal(w.Body.Bytes(), &other)
	a, before := getTimetable(t, h, "test"), getTimetable(t, h, "holiday")
	if len(a.Trains) != 1 || len(before.Trains) != 1 || a.Trains[0].ID == other.ID {
		t.Fatal("not independent")
	}
	trainTypeRequest(t, h, "DELETE", "/api/timetable/test", "", 204)
	got := getTimetable(t, h, "holiday")
	if len(got.Trains) != 1 || len(got.Trains[0].Points) != 2 || len(got.Trains[0].Points[0].Routes) != 2 {
		t.Fatal("deleting one timetable changed the other")
	}
}
