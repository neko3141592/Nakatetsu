package api

import (
	"encoding/json"
	"net/http"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
)

func timetableLinksTestServer(t *testing.T) (*gorm.DB, http.Handler) {
	t.Helper()
	db, handler := trainTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", trainBody, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"trainNumber":"0002","trainTypeId":"local","originStationId":"A","destinationStationId":"B"}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/trains", `{"trainNumber":"0003","trainTypeId":"local","originStationId":"A","destinationStationId":"B"}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"01","trains":[{"trainNumber":"0001","sequenceIndex":0}]}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPost, "/api/operations", `{"operationNumber":"02","trains":[{"trainNumber":"0002","sequenceIndex":0}]}`, http.StatusCreated)
	return db, handler
}

func getTimetable(t *testing.T, handler http.Handler, id string) timetable.Timetable {
	t.Helper()
	w := trainTypeRequest(t, handler, http.MethodGet, "/api/timetable/"+id, "", http.StatusOK)
	var result timetable.Timetable
	if err := json.Unmarshal(w.Body.Bytes(), &result); err != nil {
		t.Fatal(err)
	}
	return result
}

func assertTimetableLinks(t *testing.T, plan timetable.Timetable, trains, operations []string) {
	t.Helper()
	gotTrains, gotOperations := []string{}, []string{}
	for _, train := range plan.Trains {
		gotTrains = append(gotTrains, train.TrainNumber)
	}
	for _, operation := range plan.Operations {
		gotOperations = append(gotOperations, operation.OperationNumber)
	}
	if !reflect.DeepEqual(gotTrains, trains) || !reflect.DeepEqual(gotOperations, operations) {
		t.Fatalf("unexpected links: trains=%v, operations=%v", gotTrains, gotOperations)
	}
}

func TestTimetableLinks(t *testing.T) {
	db, handler := timetableLinksTestServer(t)
	trainBefore := getTrain(t, handler, "0001")
	operationBefore := getOperation(t, handler, "01")
	w := trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", `{"id":"weekday","displayName":"平日","revision":1,"temporaryTrainNumbers":["0003"],"operationNumbers":["01"]}`, http.StatusCreated)
	var created timetable.Timetable
	if err := json.Unmarshal(w.Body.Bytes(), &created); err != nil {
		t.Fatal(err)
	}
	assertTimetableLinks(t, created, []string{"0001", "0003"}, []string{"01"})
	if !reflect.DeepEqual(created.TemporaryTrainNumbers, []string{"0003"}) {
		t.Fatalf("unexpected temporary trains: %v", created.TemporaryTrainNumbers)
	}
	if len(created.Trains[0].Points) != 2 || len(created.Trains[0].Points[0].Routes) != 2 || len(created.Operations[0].Trains) != 1 {
		t.Fatalf("missing linked definitions in response: %+v", created)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", `{"id":"holiday","operationNumbers":["01"]}`, http.StatusCreated)
	holiday := getTimetable(t, handler, "holiday")
	var directLinks int64
	if err := db.Table("timetable_train_links").Where("timetable_id = ?", "holiday").Count(&directLinks).Error; err != nil || directLinks != 0 {
		t.Fatalf("operation trains were stored as temporary trains: count=%d, error=%v", directLinks, err)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"displayName":"改正","revision":0}`, http.StatusNoContent)
	plan := getTimetable(t, handler, "weekday")
	assertTimetableLinks(t, plan, []string{"0001", "0003"}, []string{"01"})
	if plan.DisplayName != "改正" || plan.Revision != 0 {
		t.Fatalf("metadata update failed: %+v", plan)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"temporaryTrainNumbers":["0002"]}`, http.StatusNoContent)
	assertTimetableLinks(t, getTimetable(t, handler, "weekday"), []string{"0001", "0002"}, []string{"01"})
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"operationNumbers":["02"]}`, http.StatusNoContent)
	assertTimetableLinks(t, getTimetable(t, handler, "weekday"), []string{"0002"}, []string{"02"})
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"temporaryTrainNumbers":[]}`, http.StatusNoContent)
	plan = getTimetable(t, handler, "weekday")
	assertTimetableLinks(t, plan, []string{"0002"}, []string{"02"})
	if plan.TemporaryTrainNumbers == nil || len(plan.TemporaryTrainNumbers) != 0 {
		t.Fatalf("temporary train clear failed: %v", plan.TemporaryTrainNumbers)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"displayName":"最終","temporaryTrainNumbers":["0003"],"operationNumbers":["01"]}`, http.StatusNoContent)
	assertTimetableLinks(t, getTimetable(t, handler, "weekday"), []string{"0001", "0003"}, []string{"01"})
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"operationNumbers":[]}`, http.StatusNoContent)
	assertTimetableLinks(t, getTimetable(t, handler, "weekday"), []string{"0003"}, []string{})
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"temporaryTrainNumbers":[]}`, http.StatusNoContent)
	assertTimetableLinks(t, getTimetable(t, handler, "weekday"), []string{}, []string{})
	if got := getTimetable(t, handler, "holiday"); !reflect.DeepEqual(got, holiday) {
		t.Fatalf("another timetable changed: %+v", got)
	}
	if got := getTrain(t, handler, "0001"); !reflect.DeepEqual(got, trainBefore) {
		t.Fatalf("train definition changed: %+v", got)
	}
	if got := getOperation(t, handler, "01"); !reflect.DeepEqual(got, operationBefore) {
		t.Fatalf("operation definition changed: %+v", got)
	}
	for table, want := range map[string]int64{"timetable_trains": 3, "timetable_operations": 2, "timetable_operation_trains": 2} {
		var count int64
		if err := db.Table(table).Count(&count).Error; err != nil || count != want {
			t.Fatalf("%s: count=%d, want=%d, error=%v", table, count, want, err)
		}
	}
}

func TestTimetableOperationTrainChanges(t *testing.T) {
	_, handler := timetableLinksTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", `{"id":"weekday","operationNumbers":["01"]}`, http.StatusCreated)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/operations/01", `{"trains":[{"trainNumber":"0002","sequenceIndex":0}]}`, http.StatusNoContent)
	assertTimetableLinks(t, getTimetable(t, handler, "weekday"), []string{"0002"}, []string{"01"})
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"temporaryTrainNumbers":["0002"]}`, http.StatusNoContent)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"operationNumbers":[]}`, http.StatusNoContent)
	plan := getTimetable(t, handler, "weekday")
	assertTimetableLinks(t, plan, []string{"0002"}, []string{})
	if !reflect.DeepEqual(plan.TemporaryTrainNumbers, []string{"0002"}) {
		t.Fatalf("explicit temporary train was removed: %+v", plan)
	}
}

func TestTimetableLinksValidation(t *testing.T) {
	_, handler := timetableLinksTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", `{"id":"weekday","displayName":"平日","revision":1,"temporaryTrainNumbers":["0001"],"operationNumbers":["01"]}`, http.StatusCreated)
	before := getTimetable(t, handler, "weekday")
	for _, body := range []string{
		`{"displayName":"失敗","temporaryTrainNumbers":["missing"]}`,
		`{"revision":2,"operationNumbers":["missing"]}`,
		`{"temporaryTrainNumbers":["0001","0001"]}`, `{"operationNumbers":["01","01"]}`,
		`{"temporaryTrainNumbers":[" "]}`, `{"operationNumbers":["　"]}`,
		`{"trainNumbers":[]}`,
		`{"temporaryTrainNumbers":[1]}`, `{"temporaryTrainNumbers":{}}`, `{"temporaryTrainNumbers":null}`,
		`{"temporaryTrainNumbers":["0001"]}{}`, `{"temporaryTrainNumbers":[],"unexpected":true}`,
	} {
		trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", body, http.StatusBadRequest)
		if after := getTimetable(t, handler, "weekday"); !reflect.DeepEqual(after, before) {
			t.Fatalf("invalid patch changed data, body=%s: %+v", body, after)
		}
	}
	for _, body := range []string{
		`{"id":"bad","temporaryTrainNumbers":["missing"]}`,
		`{"id":"bad","operationNumbers":["missing"]}`,
		`{"id":"bad","trainNumbers":[]}`,
		`{"id":"bad","temporaryTrainNumbers":["0001","0001"]}`,
		`{"id":"bad","operationNumbers":["01","01"]}`,
		`{"id":"bad","revision":-1}`, `{"id":" "}`, `null`,
		`{"id":"bad","trains":[{"trainNumber":"new","trainTypeId":"local","originStationId":"A","destinationStationId":"B"}]}`,
		`{"id":"bad","operations":[{"operationNumber":"new"}]}`,
	} {
		trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", body, http.StatusBadRequest)
	}
	trainTypeRequest(t, handler, http.MethodGet, "/api/timetable/bad", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodGet, "/api/trains/missing", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodGet, "/api/operations/missing", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", `{"id":"weekday","temporaryTrainNumbers":[],"operationNumbers":[]}`, http.StatusConflict)
	if after := getTimetable(t, handler, "weekday"); !reflect.DeepEqual(after, before) {
		t.Fatalf("duplicate create changed the timetable: %+v", after)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/missing", `{"temporaryTrainNumbers":[],"operationNumbers":[]}`, http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodGet, "/api/timetable/missing", "", http.StatusNotFound)
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"temporaryTrainNumbers":[],"operationNumbers":[]}`, http.StatusNoContent)
	assertTimetableLinks(t, getTimetable(t, handler, "weekday"), []string{}, []string{})
}

func TestTimetableLinksRollbackOnWriteFailure(t *testing.T) {
	db, handler := timetableLinksTestServer(t)
	trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", `{"id":"weekday","displayName":"平日","revision":1,"temporaryTrainNumbers":["0001"],"operationNumbers":["01"]}`, http.StatusCreated)
	before := getTimetable(t, handler, "weekday")
	err := db.Exec(`CREATE TRIGGER reject_operation_link BEFORE INSERT ON timetable_operation_links
		WHEN NEW.operation_number = '02' BEGIN SELECT RAISE(ABORT, 'test failure'); END`).Error
	if err != nil {
		t.Fatal(err)
	}
	trainTypeRequest(t, handler, http.MethodPatch, "/api/timetable/weekday", `{"displayName":"失敗","revision":2,"temporaryTrainNumbers":["0002"],"operationNumbers":["02"]}`, http.StatusInternalServerError)
	if after := getTimetable(t, handler, "weekday"); !reflect.DeepEqual(after, before) {
		t.Fatalf("write failure changed the timetable: %+v", after)
	}
	trainTypeRequest(t, handler, http.MethodPost, "/api/timetable/", `{"id":"failed","temporaryTrainNumbers":["0002"],"operationNumbers":["02"]}`, http.StatusInternalServerError)
	trainTypeRequest(t, handler, http.MethodGet, "/api/timetable/failed", "", http.StatusNotFound)
}
