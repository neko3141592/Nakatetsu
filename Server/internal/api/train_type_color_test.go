package api

import (
	"encoding/json"
	"net/http"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func TestTrainTypeColor(t *testing.T) {
	_, handler := trainTestServer(t)
	if got := getTrainTypeDetail(t, handler, "local"); got.Color != timetable.DefaultTrainTypeColor {
		t.Fatalf("unexpected default color: %s", got.Color)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"semi","displayName":"準急","color":"#16a34a"}`, http.StatusCreated)
	if got := getTrainTypeDetail(t, handler, "semi"); got.Color != "#16a34a" {
		t.Fatalf("color was not saved: %s", got.Color)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", `{"color":"#DC2626"}`, http.StatusNoContent)
	for _, color := range []string{"", "red", "#123", "#12345678", "#12345z"} {
		body, _ := json.Marshal(map[string]string{"color": color})
		trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", string(body), http.StatusBadRequest)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/train-types", `{"trainTypeId":"bad","displayName":"不正","color":"red"}`, http.StatusBadRequest)
	trainTypeRequest(t, handler, http.MethodGet, "/api/train-types/bad", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/train-types/local", `{"color":"#111111","stationHandlings":[{"stationId":"missing","handling":"stop"}]}`, http.StatusBadRequest)
	if got := getTrainTypeDetail(t, handler, "local"); got.Color != "#DC2626" {
		t.Fatalf("failed request changed color: %s", got.Color)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	got := getTimetable(t, handler, "test")
	if len(got.Trains) != 1 || got.Trains[0].TrainType.Color != "#DC2626" {
		t.Fatalf("missing train color: %+v", got)
	}
}
