package api

import (
	"encoding/json"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
	"net/http"
	"reflect"
	"testing"
)

func trainTestServer(t *testing.T) (*gorm.DB, http.Handler) {
	t.Helper()
	db, _ := trainTypeTestServer(t)
	for _, rows := range []any{
		&timetable.Timetable{ID: "test", DisplayName: "test"},
		&[]timetable.TrainType{{TrainTypeID: "local", DisplayName: "普通"}, {TrainTypeID: "express", DisplayName: "急行"}},
		&[]timetable.Station{{StationID: "A", DisplayName: "A駅"}, {StationID: "B", DisplayName: "B駅"}},
		&[]timetable.TrackCircuit{{TrackCircuitID: "A1T"}, {TrackCircuitID: "B1T"}},
		&[]timetable.Route{{ControlStationID: "A", RouteID: "R1"}, {ControlStationID: "A", RouteID: "R2"}},
	} {
		if err := db.Create(rows).Error; err != nil {
			t.Fatal(err)
		}
	}
	server := &ApiServer{
		DB: db,
		CircuitDefinitions: map[string]ctc.TrackCircuitDefinition{
			"A1T": {ControlKind: ctc.TrackControlInterlocking},
			"MID": {ControlKind: ctc.TrackControlInterlocking},
			"B1T": {ControlKind: ctc.TrackControlBlock},
		},
		RouteDefinitions: []ctc.TrackRouteDefinition{
			{InterlockingStationID: "A", InterlockingRouteID: "R1", Kind: ctc.RouteKindDeparture, TrackCircuitIDs: []string{"A1T", "MID"}},
			{InterlockingStationID: "A", InterlockingRouteID: "R2", Kind: ctc.RouteKindHome, TrackCircuitIDs: []string{"MID", "B1T"}},
		},
	}
	return db, server.Routes()
}

func trainID(t *testing.T, handler http.Handler, number string) string {
	t.Helper()
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/trains", "", 200)
	var rows []timetable.TimetableTrain
	if err := json.Unmarshal(w.Body.Bytes(), &rows); err != nil {
		t.Fatal(err)
	}
	for _, row := range rows {
		if row.TrainNumber == number {
			return row.ID
		}
	}
	return number
}
func getTrain(t *testing.T, handler http.Handler, number string) timetable.TimetableTrain {
	t.Helper()
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/trains/"+trainID(t, handler, number), "", 200)
	var result timetable.TimetableTrain
	if err := json.Unmarshal(w.Body.Bytes(), &result); err != nil {
		t.Fatal(err)
	}
	return result
}

const trainBody = `{"timetableId":"test","trainNumber":"0001","trainTypeId":"local","originStationId":"A","destinationStationId":"B","points":[
	{"sequenceIndex":1,"stationId":"B","trackCircuitId":"B1T","handling":"stop","arrivalSeconds":90000,"departureSeconds":90060},
	{"sequenceIndex":0,"stationId":"A","trackCircuitId":"A1T","arrivalSeconds":0,"departureSeconds":60,"routes":[
		{"sequenceIndex":1,"controlStationId":"A","routeId":"R2"},
		{"sequenceIndex":0,"controlStationId":"A","routeId":"R1"}]}]}`

func TestTrainIdentityCRUD(t *testing.T) {
	_, handler := trainTestServer(t)
	firstResponse := trainTypeRequest(t, handler, "POST", "/api/trains", trainBody, 201)
	var first timetable.TimetableTrain
	json.Unmarshal(firstResponse.Body.Bytes(), &first)
	secondResponse := trainTypeRequest(t, handler, "POST", "/api/trains", trainBody, 201)
	var second timetable.TimetableTrain
	json.Unmarshal(secondResponse.Body.Bytes(), &second)
	if first.ID == second.ID || first.ID == "" || len(first.Points) != 2 || first.Points[0].TrainID != first.ID {
		t.Fatal("lost independent identity")
	}
	before := getTrainByID(t, handler, second.ID)
	trainTypeRequest(t, handler, "PATCH", "/api/trains/"+first.ID, `{"trainNumber":"renamed","trainTypeId":"express","points":[]}`, 204)
	after := getTrainByID(t, handler, first.ID)
	if after.TrainNumber != "renamed" || after.TrainTypeID != "express" || after.Points == nil || len(after.Points) != 0 {
		t.Fatalf("patch failed %+v", after)
	}
	if !reflect.DeepEqual(before, getTrainByID(t, handler, second.ID)) {
		t.Fatal("another train changed")
	}
	trainTypeRequest(t, handler, "DELETE", "/api/trains/"+first.ID, "", 204)
	trainTypeRequest(t, handler, "GET", "/api/trains/"+first.ID, "", 404)
	for _, body := range []string{`null`, `{}`, `{"trainNumber":"x"}`, `{"timetableId":"missing","trainNumber":"x","trainTypeId":"local","originStationId":"A","destinationStationId":"B"}`} {
		trainTypeRequest(t, handler, "POST", "/api/trains", body, 400)
	}
	trainTypeRequest(t, handler, "PATCH", "/api/trains/"+second.ID, `{"trainNumber":" "}`, 400)
}
func getTrainByID(t *testing.T, handler http.Handler, id string) timetable.TimetableTrain {
	t.Helper()
	w := trainTypeRequest(t, handler, "GET", "/api/trains/"+id, "", 200)
	var result timetable.TimetableTrain
	json.Unmarshal(w.Body.Bytes(), &result)
	return result
}
