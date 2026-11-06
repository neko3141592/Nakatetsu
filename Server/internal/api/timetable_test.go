package api

import (
	"encoding/json"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"net/http"
	"testing"
)

func getTimetable(t *testing.T, h http.Handler, id string) timetable.Timetable {
	t.Helper()
	w := trainTypeRequest(t, h, "GET", "/api/timetable/"+id, "", 200)
	var result timetable.Timetable
	json.Unmarshal(w.Body.Bytes(), &result)
	return result
}
func TestTimetableCRUD(t *testing.T) {
	_, h := trainTypeTestServer(t)
	trainTypeRequest(t, h, "POST", "/api/timetable/", `{"id":"weekday","displayName":"平日"}`, 201)
	trainTypeRequest(t, h, "POST", "/api/timetable/", `{"id":"weekday","displayName":"別名"}`, 409)
	trainTypeRequest(t, h, "PATCH", "/api/timetable/weekday", `{"displayName":"改正案"}`, 204)
	got := getTimetable(t, h, "weekday")
	if got.DisplayName != "改正案" || got.Trains == nil || len(got.Trains) != 0 {
		t.Fatalf("%+v", got)
	}
	for _, body := range []string{`null`, `{}`, `{"displayName":" "}`, `{"operationNumbers":[]}`, `{"temporaryTrainNumbers":[]}`, `{"displayName":"x"}{}`} {
		trainTypeRequest(t, h, "PATCH", "/api/timetable/weekday", body, 400)
	}
	trainTypeRequest(t, h, "DELETE", "/api/timetable/weekday", "", 204)
	trainTypeRequest(t, h, "GET", "/api/timetable/weekday", "", 404)
}
