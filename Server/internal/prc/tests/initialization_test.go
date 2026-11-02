package tests

import (
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
)

func TestInitializeCreatesTimetableStateWithoutAssigningTrains(t *testing.T) {
	plan := initializationPlan()
	// PRCはCTCの列番・運用を割り当て直したり、初期化時に検証したりしない。
	for name, trains := range map[string]map[string]ctc.TrainState{
		"no_trains": nil,
		"operation_and_number": {
			"tracked": {OperationNumber: "01", TrainNumber: "0002"},
		},
		"unknown_operation": {
			"tracked": {OperationNumber: "missing", TrainNumber: "0003"},
		},
		"unknown_number": {
			"tracked": {TrainNumber: "missing"},
		},
		"duplicate_number": {
			"first":  {TrainNumber: "0003"},
			"second": {TrainNumber: "0003"},
		},
	} {
		t.Run(name, func(t *testing.T) {
			core := prc.New(nil, plan, nil, nil)
			if err := core.Initialize(ctc.TrackingState{
				IsInitialized: true,
				TrainsByID:    trains,
			}); err != nil {
				t.Fatal(err)
			}
			state := core.GetTimetableState()
			if len(state.TrainsByNumber) != 3 || len(state.PointsByID) != 3 || len(state.RouteUsagesByID) != 3 {
				t.Fatalf("incomplete timetable state: %+v", state)
			}
			for number := range plan.TrainsByNumber {
				train, exists := state.TrainsByNumber[number]
				if !exists || train != (prc.TrainState{}) {
					t.Fatalf("train %q should be unmatched: %+v", number, train)
				}
			}
			for id := range state.PointsByID {
				if state.PointsByID[id] != (prc.PointState{}) {
					t.Fatalf("point %d has unexpected actual times", id)
				}
			}
			for id, usage := range state.RouteUsagesByID {
				if usage.Phase != prc.RouteUsagePending {
					t.Fatalf("route usage %v is not pending: %+v", id, usage)
				}
			}
			delete(state.TrainsByNumber, "0001")
			delete(state.PointsByID, 1)
			delete(state.RouteUsagesByID, prc.RouteUsageID{TimetablePointID: 1})
			if fresh := core.GetTimetableState(); len(fresh.TrainsByNumber) != 3 || len(fresh.PointsByID) != 3 || len(fresh.RouteUsagesByID) != 3 {
				t.Fatal("returned state shares maps with PRC")
			}
		})
	}
}

func TestInitializeRejectsUninitializedTrackingWithoutChangingState(t *testing.T) {
	core := prc.New(nil, initializationPlan(), nil, nil)
	if err := core.Initialize(ctc.TrackingState{IsInitialized: true}); err != nil {
		t.Fatal(err)
	}
	before := core.GetTimetableState()
	if err := core.Initialize(ctc.TrackingState{}); err == nil {
		t.Fatal("expected initialization error")
	}
	if state := core.GetTimetableState(); !reflect.DeepEqual(state, before) {
		t.Fatalf("failed initialization changed the state: %+v", state)
	}
}

func initializationPlan() prc.TimetablePlan {
	return prc.TimetablePlan{
		TrainsByNumber: map[string]prc.TrainPlan{
			"0001": {TrainNumber: "0001", Points: []prc.PointPlan{{
				TimetablePointID: 1,
				DepartureRoutes:  []prc.RouteUsage{{ID: prc.RouteUsageID{TimetablePointID: 1}, TrainNumber: "0001"}},
			}}},
			"0002": {TrainNumber: "0002", Points: []prc.PointPlan{{
				TimetablePointID: 2,
				ArrivalRoutes:    []prc.RouteUsage{{ID: prc.RouteUsageID{TimetablePointID: 2}, TrainNumber: "0002"}},
			}}},
			"0003": {TrainNumber: "0003", Points: []prc.PointPlan{{
				TimetablePointID: 3,
				ShuntingRoutes:   []prc.RouteUsage{{ID: prc.RouteUsageID{TimetablePointID: 3}, TrainNumber: "0003"}},
			}}},
		},
		OperationsByNumber: map[string]prc.OperationPlan{
			"01": {OperationNumber: "01", TrainNumbers: []string{"0001", "0002"}},
		},
	}
}
