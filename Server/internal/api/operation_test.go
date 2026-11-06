package api

import (
	"encoding/json"
	"fmt"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"testing"
)

func TestOperationPlanScopeAndDeletion(t *testing.T) {
	_, h := trainTestServer(t)
	w := trainTypeRequest(t, h, "POST", "/api/trains", trainBody, 201)
	var train timetable.TimetableTrain
	json.Unmarshal(w.Body.Bytes(), &train)
	w = trainTypeRequest(t, h, "POST", "/api/operation-plans", `{"timetableId":"test","displayName":"運用A"}`, 201)
	var plan timetable.TimetableOperationPlan
	json.Unmarshal(w.Body.Bytes(), &plan)
	w = trainTypeRequest(t, h, "POST", "/api/operations", fmt.Sprintf(`{"operationPlanId":%q,"operationNumber":"01","trains":[{"trainId":%q,"sequenceIndex":0}]}`, plan.ID, train.ID), 201)
	var op timetable.TimetableOperation
	json.Unmarshal(w.Body.Bytes(), &op)
	trainTypeRequest(t, h, "POST", "/api/operations", fmt.Sprintf(`{"operationPlanId":%q,"operationNumber":"02","trains":[{"trainId":%q,"sequenceIndex":0}]}`, plan.ID, train.ID), 409)
	trainTypeRequest(t, h, "POST", "/api/timetable/", `{"id":"other","displayName":"別ダイヤ"}`, 201)
	w = trainTypeRequest(t, h, "POST", "/api/operation-plans", `{"timetableId":"other","displayName":"運用B"}`, 201)
	var other timetable.TimetableOperationPlan
	json.Unmarshal(w.Body.Bytes(), &other)
	trainTypeRequest(t, h, "POST", "/api/operations", fmt.Sprintf(`{"operationPlanId":%q,"operationNumber":"01","trains":[{"trainId":%q,"sequenceIndex":0}]}`, other.ID, train.ID), 400)
	trainTypeRequest(t, h, "DELETE", "/api/trains/"+train.ID, "", 409)
	before := getTrainByID(t, h, train.ID)
	trainTypeRequest(t, h, "PATCH", "/api/operations/"+op.ID, `{"trains":[{"trainId":"missing","sequenceIndex":0}]}`, 400)
	detail := trainTypeRequest(t, h, "GET", "/api/operations/"+op.ID, "", 200)
	var unchanged timetable.TimetableOperation
	json.Unmarshal(detail.Body.Bytes(), &unchanged)
	if len(unchanged.Trains) != 1 || unchanged.Trains[0].TrainID != train.ID {
		t.Fatal("failed patch was not rolled back")
	}
	trainTypeRequest(t, h, "DELETE", "/api/operations/"+op.ID, "", 204)
	if len(getTrainByID(t, h, train.ID).Points) != len(before.Points) {
		t.Fatal("deleted train points")
	}
	trainTypeRequest(t, h, "DELETE", "/api/operation-plans/"+plan.ID, "", 204)
	trainTypeRequest(t, h, "GET", "/api/trains/"+train.ID, "", 200)
}
