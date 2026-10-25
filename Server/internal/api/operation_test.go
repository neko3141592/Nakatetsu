package api

import (
	"encoding/json"
	"net/http"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
)

func getOperation(t *testing.T, handler http.Handler, number string) operationDetail {
	t.Helper()
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/operations/"+number, "", http.StatusOK)
	var result operationDetail
	if err := json.Unmarshal(w.Body.Bytes(), &result); err != nil {
		t.Fatal(err)
	}
	return result
}

func TestOperationCRUD(t *testing.T) {
	db, handler := trainTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"trainNumber":"0002","trainTypeId":"local","originStationId":"A","destinationStationId":"B"}`, http.StatusCreated)
	if w := trainTypeRequest(t, handler, http.MethodGet, "/api/operations", "", http.StatusOK); w.Body.String() != "[]" {
		t.Fatalf("unexpected empty list: %s", w.Body.String())
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"02","trains":[{"trainNumber":"0001","sequenceIndex":0}]}`, http.StatusCreated)
	w := trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"01","trains":[{"trainNumber":"0002","sequenceIndex":1},{"trainNumber":"0001","sequenceIndex":0}]}`, http.StatusCreated)
	var created operationDetail
	if err := json.Unmarshal(w.Body.Bytes(), &created); err != nil || created.OperationNumber != "01" || len(created.Trains) != 2 {
		t.Fatalf("unexpected create response: %s, error=%v", w.Body.String(), err)
	}
	before := getOperation(t, handler, "01")
	if !reflect.DeepEqual(before.Trains, []operationTrain{{"0001", 0}, {"0002", 1}}) {
		t.Fatalf("unexpected order: %+v", before)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"01","trains":[]}`, http.StatusConflict)
	if got := getOperation(t, handler, "01"); !reflect.DeepEqual(got, before) {
		t.Fatalf("duplicate changed the operation: %+v", got)
	}
	w = trainTypeRequest(t, handler, http.MethodGet, "/api/operations", "", http.StatusOK)
	var list []struct{ OperationNumber string }
	if err := json.Unmarshal(w.Body.Bytes(), &list); err != nil || len(list) != 2 || list[0].OperationNumber != "01" || list[1].OperationNumber != "02" {
		t.Fatalf("unexpected list: %s, error=%v", w.Body.String(), err)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/operations/01", `{"trains":[{"trainNumber":"0001","sequenceIndex":1},{"trainNumber":"0002","sequenceIndex":0}]}`, http.StatusNoContent)
	if got := getOperation(t, handler, "01"); !reflect.DeepEqual(got.Trains, []operationTrain{{"0002", 0}, {"0001", 1}}) {
		t.Fatalf("swap failed: %+v", got)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/operations/01", `{"trains":[{"trainNumber":"0002","sequenceIndex":0}]}`, http.StatusNoContent)
	if got := getOperation(t, handler, "01"); !reflect.DeepEqual(got.Trains, []operationTrain{{"0002", 0}}) {
		t.Fatalf("replacement failed: %+v", got)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/operations/01", `{"trains":[]}`, http.StatusNoContent)
	if got := getOperation(t, handler, "01"); got.Trains == nil || len(got.Trains) != 0 {
		t.Fatalf("clear failed: %+v", got)
	}
	if other := getOperation(t, handler, "02"); !reflect.DeepEqual(other.Trains, []operationTrain{{"0001", 0}}) {
		t.Fatalf("another operation changed: %+v", other)
	}
	for _, method := range []string{http.MethodGet, http.MethodPatch, http.MethodDelete} {
		trainTypeRequest(t, handler, method, "/api/operations/missing", `{"trains":[]}`, http.StatusNotFound)
	}
	trainTypeRequest(t, handler, http.MethodDelete, "/api/operations/01", "", http.StatusNoContent)
	trainTypeRequest(t, handler, http.MethodDelete, "/api/operations/01", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"empty"}`, http.StatusCreated)
	if got := getOperation(t, handler, "empty"); got.Trains == nil || len(got.Trains) != 0 {
		t.Fatalf("unexpected empty operation: %+v", got)
	}
	var count int64
	if err := db.Model(&timetable.TimetableTrain{}).Count(&count).Error; err != nil || count != 2 {
		t.Fatalf("train definitions changed: count=%d, error=%v", count, err)
	}
}

func TestOperationValidationAndRollback(t *testing.T) {
	_, handler := trainTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"01","trains":[{"trainNumber":"0001","sequenceIndex":0}]}`, http.StatusCreated)
	before := getOperation(t, handler, "01")
	trainBefore := getTrain(t, handler, "0001")
	for _, body := range []string{
		`{`, `{}`, `null`, `{"trains":null}`, `{"trains":{}}`, `{"trains":[]}{}`,
		`{"operationNumber":"02","trains":[]}`, `{"trains":[{"trainNumber":" "}]}`,
		`{"trains":[{"trainNumber":"0001","sequenceIndex":-1}]}`,
		`{"trains":[{"trainNumber":"0001","sequenceIndex":0},{"trainNumber":"0001","sequenceIndex":1}]}`,
		`{"trains":[{"trainNumber":"0001","sequenceIndex":0},{"trainNumber":"0002","sequenceIndex":0}]}`,
		`{"trains":[{"trainNumber":"0001","sequenceIndex":0},{"trainNumber":"missing","sequenceIndex":1}]}`,
		`{"trains":[{"trainNumber":"0001","operationNumber":"02"}]}`,
		`{"trains":[{"trainNumber":"0001","train":{"trainNumber":"0001","trainTypeId":"changed","originStationId":"A","destinationStationId":"B"}}]}`,
	} {
		trainTypeRequest(t, handler, http.MethodPatch, "/api/operations/01", body, http.StatusBadRequest)
		if after := getOperation(t, handler, "01"); !reflect.DeepEqual(after, before) {
			t.Fatalf("failed patch changed data, body=%s: %+v", body, after)
		}
	}
	for _, body := range []string{
		`{}`, `null`, `{"operationNumber":" "}`, `{"operationNumber":"bad","unknown":true}`,
		`{"operationNumber":"bad","trains":[{"trainNumber":"missing","sequenceIndex":0}]}`,
		`{"operationNumber":"bad","trains":[{"trainNumber":"0001","sequenceIndex":0},{"trainNumber":"missing","sequenceIndex":1}]}`,
		`{"operationNumber":"bad","trains":[{"trainNumber":"0001","sequenceIndex":0},{"trainNumber":"0001","sequenceIndex":1}]}`,
	} {
		trainTypeRequest(t, handler, http.MethodPost, "/api/operations", body, http.StatusBadRequest)
	}
	trainTypeRequest(t, handler, http.MethodGet, "/api/operations/bad", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodGet, "/api/trains/missing", "", http.StatusNotFound)
	if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, trainBefore) {
		t.Fatalf("operation edits changed train definition: %+v", after)
	}
}

func TestDeleteOperationPreservesTrains(t *testing.T) {
	db, handler := trainTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"01","trains":[{"trainNumber":"0001","sequenceIndex":0}]}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"02","trains":[{"trainNumber":"0001","sequenceIndex":0}]}`, http.StatusCreated)
	for _, id := range []string{"weekday", "holiday"} {
		if err := db.Create(&timetable.Timetable{
			ID: id, DisplayName: id, Operations: []timetable.TimetableOperation{{OperationNumber: "01"}, {OperationNumber: "02"}},
			Trains: []timetable.TimetableTrain{{TrainNumber: "0001", TrainTypeID: "local"}},
		}).Error; err != nil {
			t.Fatal(err)
		}
	}
	before := getTrain(t, handler, "0001")
	trainTypeRequest(t, handler, http.MethodDelete, "/api/operations/01", "", http.StatusNoContent)
	if after := getTrain(t, handler, "0001"); !reflect.DeepEqual(after, before) {
		t.Fatalf("deletion changed the train: %+v", after)
	}
	if other := getOperation(t, handler, "02"); !reflect.DeepEqual(other.Trains, []operationTrain{{"0001", 0}}) {
		t.Fatalf("another operation changed: %+v", other)
	}
	for table, want := range map[string]int64{
		"timetable_operations": 1, "timetable_operation_trains": 1, "timetable_operation_links": 2,
		"timetable_trains": 1, "timetable_points": 2, "timetable_point_routes": 2, "timetables": 2, "timetable_train_links": 2,
	} {
		var count int64
		if err := db.Table(table).Count(&count).Error; err != nil || count != want {
			t.Fatalf("%s: count=%d, want=%d, error=%v", table, count, want, err)
		}
	}
}
