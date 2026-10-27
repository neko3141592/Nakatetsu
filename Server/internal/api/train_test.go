package api

import (
	"encoding/json"
	"net/http"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
)

func trainTestServer(t *testing.T) (*gorm.DB, http.Handler) {
	t.Helper()
	db, handler := trainTypeTestServer(t)
	for _, rows := range []any{
		&[]timetable.TrainType{{TrainTypeID: "local", DisplayName: "普通"}, {TrainTypeID: "express", DisplayName: "急行"}},
		&[]timetable.Station{{StationID: "A", DisplayName: "A駅"}, {StationID: "B", DisplayName: "B駅"}},
		&[]timetable.TrackCircuit{{TrackCircuitID: "A1T"}, {TrackCircuitID: "B1T"}},
		&[]timetable.Route{{ControlStationID: "A", RouteID: "R1"}, {ControlStationID: "A", RouteID: "R2"}},
	} {
		if err := db.Create(rows).Error; err != nil {
			t.Fatal(err)
		}
	}
	return db, handler
}

func getTrain(t *testing.T, handler http.Handler, number string) timetable.TimetableTrain {
	t.Helper()
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/trains/"+number, "", http.StatusOK)
	var result timetable.TimetableTrain
	if err := json.Unmarshal(w.Body.Bytes(), &result); err != nil {
		t.Fatal(err)
	}
	return result
}

const trainBody = `{"trainNumber":"0001","trainTypeId":"local","originStationId":"A","destinationStationId":"B","points":[
	{"sequenceIndex":1,"stationId":"B","trackCircuitId":"B1T","handling":"stop","arrivalSeconds":90000,"departureSeconds":90060},
	{"sequenceIndex":0,"stationId":"A","trackCircuitId":"A1T","arrivalSeconds":0,"departureSeconds":60,"routes":[
		{"sequenceIndex":1,"controlStationId":"A","routeId":"R2"},
		{"sequenceIndex":0,"controlStationId":"A","routeId":"R1"}]}]}`

func TestTrainCRUD(t *testing.T) {
	db, handler := trainTestServer(t)
	if w := trainTypeRequest(t, handler, http.MethodGet, "/api/trains", "", http.StatusOK); w.Body.String() != "[]" {
		t.Fatalf("unexpected empty list: %s", w.Body.String())
	}
	w := trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	var created timetable.TimetableTrain
	if err := json.Unmarshal(w.Body.Bytes(), &created); err != nil {
		t.Fatal(err)
	}
	if len(created.Points) != 2 || created.Points[0].TimetablePointID <= 0 || created.Points[1].TrainNumber != "0001" ||
		created.Points[1].Routes[0].TimetablePointID != created.Points[1].TimetablePointID {
		t.Fatalf("missing generated identities: %+v", created)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"trainNumber":"0002","trainTypeId":"local","originStationId":"A","destinationStationId":"B","points":[{"trackCircuitId":"A1T"}]}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"trainNumber":"0001","trainTypeId":"express","originStationId":"A","destinationStationId":"B"}`, http.StatusConflict)
	before := getTrain(t, handler, "0001")
	if before.TrainType.DisplayName != "普通" || len(before.Points) != 2 || before.Points[0].SequenceIndex != 0 ||
		before.Points[0].Handling != timetable.HandlingUnspecified || *before.Points[0].ArrivalSeconds != 0 ||
		before.Points[1].SequenceIndex != 1 || *before.Points[1].ArrivalSeconds != 90000 ||
		len(before.Points[0].Routes) != 2 || before.Points[0].Routes[0].RouteID != "R1" || before.Points[0].Routes[1].RouteID != "R2" {
		t.Fatalf("unexpected detail/order: %+v", before)
	}
	w = trainTypeRequest(t, handler, http.MethodGet, "/api/trains", "", http.StatusOK)
	var list []struct{ TrainNumber, TrainTypeID string }
	if err := json.Unmarshal(w.Body.Bytes(), &list); err != nil || len(list) != 2 || list[0].TrainNumber != "0001" || list[1].TrainNumber != "0002" {
		t.Fatalf("unexpected list: %s, error=%v", w.Body.String(), err)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/0001", `{"trainTypeId":"express"}`, http.StatusNoContent)
	got := getTrain(t, handler, "0001")
	if got.TrainTypeID != "express" || !reflect.DeepEqual(got.Points, before.Points) {
		t.Fatalf("type-only patch changed points: %+v", got)
	}
	other := getTrain(t, handler, "0002")
	replacement := timetable.TimetablePoint{
		TimetablePointID: other.Points[0].TimetablePointID, TrainNumber: "0002", TrackCircuitID: "B1T",
		Routes: []timetable.TimetablePointRoute{{TimetablePointID: other.Points[0].TimetablePointID, ControlStationID: "A", RouteID: "R2"}},
	}
	body, err := json.Marshal(map[string]any{"points": []timetable.TimetablePoint{replacement}})
	if err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/0001", string(body), http.StatusNoContent)
	got = getTrain(t, handler, "0001")
	if got.TrainTypeID != "express" || len(got.Points) != 1 || got.Points[0].TrackCircuitID != "B1T" ||
		got.Points[0].TrainNumber != "0001" || got.Points[0].TimetablePointID == replacement.TimetablePointID ||
		len(got.Points[0].Routes) != 1 || got.Points[0].Routes[0].TimetablePointID != got.Points[0].TimetablePointID {
		t.Fatalf("replacement/identity reset failed: %+v", got)
	}
	if after := getTrain(t, handler, "0002"); !reflect.DeepEqual(after, other) {
		t.Fatalf("another train was changed: %+v", after)
	}
	for table, want := range map[string]int64{"timetable_points": 2, "timetable_point_routes": 1} {
		var count int64
		if err := db.Table(table).Count(&count).Error; err != nil || count != want {
			t.Fatalf("%s: count=%d, want=%d, error=%v", table, count, want, err)
		}
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/0001", `{"points":[]}`, http.StatusNoContent)
	got = getTrain(t, handler, "0001")
	if got.TrainTypeID != "express" || got.Points == nil || len(got.Points) != 0 {
		t.Fatalf("clear failed: %+v", got)
	}
	for _, method := range []string{http.MethodGet, http.MethodPatch, http.MethodDelete} {
		trainTypeRequest(t, handler, method, "/api/trains/missing", `{"points":[]}`, http.StatusNotFound)
	}
	trainTypeRequest(t, handler, http.MethodDelete, "/api/trains/0001", "", http.StatusNoContent)
	trainTypeRequest(t, handler, http.MethodDelete, "/api/trains/0001", "", http.StatusNotFound)
}

func TestTrainRequiredBasics(t *testing.T) {
	db, handler := trainTestServer(t)
	valid := map[string]any{"trainNumber": "9000", "trainTypeId": "local", "originStationId": "A", "destinationStationId": "B"}
	for _, field := range []string{"trainNumber", "trainTypeId", "originStationId", "destinationStationId"} {
		for _, value := range []any{nil, "", " "} {
			body := make(map[string]any)
			for key, value := range valid {
				body[key] = value
			}
			body[field] = value
			data, err := json.Marshal(body)
			if err != nil {
				t.Fatal(err)
			}
			trainTypeRequest(t, handler, http.MethodPost, "/api/trains", string(data), http.StatusBadRequest)
		}
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	before := getTrain(t, handler, "0001")
	for _, body := range []string{
		`{"trainTypeId":""}`, `{"originStationId":""}`, `{"destinationStationId":" "}`,
		`{"originStationId":"missing"}`, `{"destinationStationId":"missing"}`,
	} {
		trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/0001", body, http.StatusBadRequest)
		if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, before) {
			t.Fatal("invalid patch changed the train")
		}
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/0001", `{"originStationId":"B","destinationStationId":"A"}`, http.StatusNoContent)
	after := getTrain(t, handler, "0001")
	if after.OriginStationID != "B" || after.DestinationStationID != "A" || !reflect.DeepEqual(after.Points, before.Points) {
		t.Fatalf("unexpected endpoint update: %+v", after)
	}
	if err := db.Create(&timetable.TimetableTrain{TrainNumber: "legacy"}).Error; err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/legacy", `{"trainTypeId":"local"}`, http.StatusBadRequest)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/legacy", `{"trainTypeId":"local","originStationId":"A","destinationStationId":"B"}`, http.StatusNoContent)
	for _, field := range []string{"originStationId", "destinationStationId"} {
		body := make(map[string]any)
		for key, value := range valid {
			body[key] = value
		}
		body[field] = "missing"
		data, err := json.Marshal(body)
		if err != nil {
			t.Fatal(err)
		}
		trainTypeRequest(t, handler, http.MethodPost, "/api/trains", string(data), http.StatusBadRequest)
	}
}

func TestTrainValidationAndRollback(t *testing.T) {
	_, handler := trainTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	before := getTrain(t, handler, "0001")
	for _, body := range []string{
		`{`, `{}`, `null`, `{"trainTypeId":" "}`, `{"trainTypeId":"missing"}`, `{"points":[]}{}`,
		`{"trainNumber":"0002"}`, `{"points":[{"trackCircuitId":"A1T","unknown":1}]}`,
		`{"trainTypeId":"express","points":[{"trackCircuitId":"missing"}]}`,
		`{"trainTypeId":"express","points":[{"trackCircuitId":"A1T","stationId":"missing"}]}`,
		`{"trainTypeId":"express","points":[{"trackCircuitId":"A1T","routes":[{"controlStationId":"B","routeId":"R1"}]}]}`,
		`{"points":[{"trackCircuitId":" "}]}`, `{"points":[{"trackCircuitId":"A1T","stationId":" "}]}`,
		`{"points":[{"trackCircuitId":"A1T","sequenceIndex":-1}]}`,
		`{"points":[{"trackCircuitId":"A1T"},{"trackCircuitId":"B1T"}]}`,
		`{"points":[{"trackCircuitId":"A1T","handling":"invalid"}]}`,
		`{"points":[{"trackCircuitId":"A1T","arrivalSeconds":-1}]}`,
		`{"points":[{"trackCircuitId":"A1T","departureSeconds":-1}]}`,
		`{"points":[{"trackCircuitId":"A1T","arrivalSeconds":60,"departureSeconds":0}]}`,
		`{"points":[{"trackCircuitId":"A1T","routes":[{"controlStationId":"A","routeId":" "}]}]}`,
		`{"points":[{"trackCircuitId":"A1T","routes":[{"controlStationId":"A","routeId":"R1","sequenceIndex":-1}]}]}`,
		`{"points":[{"trackCircuitId":"A1T","routes":[{"controlStationId":"A","routeId":"R1"},{"controlStationId":"A","routeId":"R2"}]}]}`,
	} {
		trainTypeRequest(t, handler, http.MethodPatch, "/api/trains/0001", body, http.StatusBadRequest)
		if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, before) {
			t.Fatalf("failed patch changed data, body=%s: %+v", body, after)
		}
	}
	for _, body := range []string{
		`{}`, `null`, `{"trainNumber":" ","trainTypeId":"local","originStationId":"A","destinationStationId":"B"}`, `{"trainNumber":"bad","trainTypeId":"missing","originStationId":"A","destinationStationId":"B"}`,
		`{"trainNumber":"bad","trainTypeId":"local","originStationId":"A","destinationStationId":"B","points":[{"trackCircuitId":"missing"}]}`,
		`{"trainNumber":"bad","trainTypeId":"local","originStationId":"A","destinationStationId":"B","points":[{"trackCircuitId":"A1T","routes":[{"controlStationId":"A","routeId":"missing"}]}]}`,
	} {
		trainTypeRequest(t, handler, http.MethodPost, "/api/trains", body, http.StatusBadRequest)
	}
	trainTypeRequest(t, handler, http.MethodGet, "/api/trains/bad", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"trainNumber":"0002","trainTypeId":"local","originStationId":"A","destinationStationId":"B","trainType":{"trainTypeId":"injected","displayName":"変更"},"points":[{"trackCircuitId":"A1T","stationId":"A","station":{"stationId":"A","displayName":"変更"},"trackCircuit":{"trackCircuitId":"injected"},"routes":[{"controlStationId":"A","routeId":"R1","route":{"controlStationId":"injected","routeId":"new"}}]}]}`, http.StatusCreated)
	if after := getTrainTypeDetail(t, handler, "local"); after.DisplayName != "普通" {
		t.Fatalf("train input modified the type: %+v", after)
	}
}

func TestDeleteTrainCascadeAndRestriction(t *testing.T) {
	db, handler := trainTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	if err := db.Create(&timetable.TimetableOperation{
		OperationNumber: "01", Trains: []timetable.TimetableOperationTrain{{TrainNumber: "0001"}},
	}).Error; err != nil {
		t.Fatal(err)
	}
	if err := db.Create(&timetable.Timetable{
		ID: "weekday", DisplayName: "平日", Trains: []timetable.TimetableTrain{{TrainNumber: "0001", TrainTypeID: "local"}},
	}).Error; err != nil {
		t.Fatal(err)
	}
	before := getTrain(t, handler, "0001")
	trainTypeRequest(t, handler, http.MethodDelete, "/api/trains/0001", "", http.StatusConflict)
	if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, before) {
		t.Fatalf("rejected deletion changed data: %+v", after)
	}
	if err := db.Where("operation_number = ?", "01").Delete(&timetable.TimetableOperationTrain{}).Error; err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodDelete, "/api/trains/0001", "", http.StatusNoContent)
	for table, want := range map[string]int64{
		"timetable_trains": 0, "timetable_points": 0, "timetable_point_routes": 0, "timetable_train_links": 0,
		"timetables": 1, "timetable_operations": 1, "train_types": 2, "stations": 2, "track_circuits": 2, "routes": 2,
	} {
		var count int64
		if err := db.Table(table).Count(&count).Error; err != nil || count != want {
			t.Fatalf("%s: count=%d, want=%d, error=%v", table, count, want, err)
		}
	}
}
