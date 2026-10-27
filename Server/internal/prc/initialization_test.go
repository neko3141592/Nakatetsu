package prc

import (
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func TestInitializeAssignsFirstTrainAndPoint(t *testing.T) {
	plan := initializationPlan()
	core := New(nil, plan, nil, nil)
	err := core.Initialize(ctc.TrackingState{
		IsInitialized: true,
		TrainsByID: map[string]ctc.TrainState{
			"operation":  {OperationNumber: "01", TrainNumber: "0002"},
			"number":     {TrainNumber: "0003"},
			"unassigned": {},
		},
	})
	if err != nil {
		t.Fatal(err)
	}
	first := core.state.TrainsByNumber["0001"]
	if first.TrackingTrainID == nil || *first.TrackingTrainID != "operation" || first.OperationNumber == nil || *first.OperationNumber != "01" || first.CurrentPointID == nil || *first.CurrentPointID != 1 {
		t.Fatalf("wrong operation assignment: %+v", first)
	}
	standalone := core.state.TrainsByNumber["0003"]
	if standalone.TrackingTrainID == nil || *standalone.TrackingTrainID != "number" || standalone.OperationNumber != nil || standalone.CurrentPointID == nil || *standalone.CurrentPointID != 3 {
		t.Fatalf("wrong standalone assignment: %+v", standalone)
	}
	if future := core.state.TrainsByNumber["0002"]; future.TrackingTrainID != nil || future.CurrentPointID != nil {
		t.Fatalf("future train was assigned: %+v", future)
	}
	if len(core.state.TrainsByNumber) != 3 || len(core.state.PointsByID) != 3 || len(core.state.RouteUsagesByID) != 2 {
		t.Fatalf("incomplete timetable state: %+v", core.state)
	}
	for id, state := range core.state.RouteUsagesByID {
		if state.Phase != RouteUsagePending {
			t.Fatalf("route usage %v is not pending: %+v", id, state)
		}
	}
	*first.CurrentPointID = 99
	if plan.TrainsByNumber["0001"].Points[0].TimetablePointID != 1 {
		t.Fatal("runtime state shares a point ID with the plan")
	}
}

func TestInitializeRejectsInvalidAssignmentsWithoutChangingState(t *testing.T) {
	for _, name := range []string{"uninitialized", "unknown_operation", "empty_operation", "unknown_train", "missing_points", "duplicate_assignment"} {
		t.Run(name, func(t *testing.T) {
			plan := initializationPlan()
			tracking := ctc.TrackingState{
				IsInitialized: true,
				TrainsByID: map[string]ctc.TrainState{
					"valid": {TrainNumber: "0003"},
					"other": {OperationNumber: "01"},
				},
			}
			switch name {
			case "uninitialized":
				tracking.IsInitialized = false
			case "unknown_operation":
				tracking.TrainsByID["other"] = ctc.TrainState{OperationNumber: "missing"}
			case "empty_operation":
				plan.OperationsByNumber["01"] = OperationPlan{}
			case "unknown_train":
				tracking.TrainsByID["other"] = ctc.TrainState{TrainNumber: "missing"}
			case "missing_points":
				plan.TrainsByNumber["0001"] = TrainPlan{}
			case "duplicate_assignment":
				tracking.TrainsByID["other"] = ctc.TrainState{TrainNumber: "0003"}
			}
			core := New(nil, plan, nil, nil)
			before := TimetableState{TrainsByNumber: map[string]TrainState{"existing": {IsHeld: true}}}
			core.state = before
			if err := core.Initialize(tracking); err == nil {
				t.Fatal("expected initialization error")
			}
			if !reflect.DeepEqual(core.state, before) {
				t.Fatalf("failed initialization changed the state: %+v", core.state)
			}
		})
	}
}

func initializationPlan() TimetablePlan {
	return TimetablePlan{
		TrainsByNumber: map[string]TrainPlan{
			"0001": {TrainNumber: "0001", Points: []PointPlan{{TimetablePointID: 1}}},
			"0002": {TrainNumber: "0002", Points: []PointPlan{{TimetablePointID: 2}}},
			"0003": {TrainNumber: "0003", Points: []PointPlan{{TimetablePointID: 3}}},
		},
		OperationsByNumber: map[string]OperationPlan{
			"01": {OperationNumber: "01", TrainNumbers: []string{"0001", "0002"}},
		},
		RouteUsagesByID: map[RouteUsageID]RouteUsage{
			{TimetablePointID: 1}: {TrainNumber: "0001"},
			{TimetablePointID: 2}: {TrainNumber: "0002"},
		},
	}
}
