package tests

import (
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
)

func TestShuntingWaitsFromEachPointArrival(t *testing.T) {
	_, ctcCore, departure := departureFixture(t, seconds(100), prc.HandlingStop)
	first := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 100, RouteSequenceIndex: 1}, TrainNumber: "1001", ControlStationID: "A", RouteID: "S"}
	second := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 200}, TrainNumber: "1001", ControlStationID: "A", RouteID: "S"}
	plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{
		"1001": {TrainNumber: "1001", Points: []prc.PointPlan{
			{TimetablePointID: 100, TrackCircuitID: "A", Handling: prc.HandlingStop, DepartureSeconds: seconds(100), DepartureRoutes: []prc.RouteUsage{departure}, ShuntingRoutes: []prc.RouteUsage{first}},
			{TimetablePointID: 200, TrackCircuitID: "B", Handling: prc.HandlingStop, DepartureSeconds: seconds(100), ShuntingRoutes: []prc.RouteUsage{second}},
		}},
	}}
	core := prc.New(ctcCore, plan, nil, nil)
	if core.ShuntingWaitSeconds != 30 {
		t.Fatal("default shunting wait is not 30 seconds")
	}
	if err := core.Initialize(ctcCore.tracking); err != nil {
		t.Fatal(err)
	}
	for _, step := range []struct {
		now, since float32
		circuit    string
		route      prc.RouteUsage
		wantRaised bool
	}{
		{100, 100, "A", first, false},
		{129.5, 100, "A", first, false},
		{130, 100, "A", first, true},
		{200, 200, "B", second, false},
		{229.5, 200, "B", second, false},
		{230, 200, "B", second, true},
	} {
		ctcCore.snapshot.WorldTime.WorldTimeSeconds = step.now
		ctcCore.tracking.CircuitsByID = map[string]ctc.TrackCircuitState{step.circuit: {IsOccupied: true, TrainID: "train-1"}}
		core.UpdateRouteRequests()
		state := core.GetTimetableState()
		since := state.TrainsByNumber["1001"].CurrentPointOccupiedSinceSeconds
		if since == nil || *since != float64(step.since) {
			t.Fatalf("at %v: arrival time=%v, want %v", step.now, since, step.since)
		}
		if _, exists := state.RaisedRouteUsagesByID[step.route.ID]; exists != step.wantRaised {
			t.Fatalf("at %v: raised=%v, want %v", step.now, exists, step.wantRaised)
		}
		if step.circuit == "A" && state.RaisedRouteUsagesByID[departure.ID] != departure {
			t.Fatal("normal departure was delayed by the shunting wait")
		}
	}
	// 返却された時刻の書き換えで内部状態を変更できない。
	*core.GetTimetableState().TrainsByNumber["1001"].CurrentPointOccupiedSinceSeconds = 0
	if *core.GetTimetableState().TrainsByNumber["1001"].CurrentPointOccupiedSinceSeconds != 200 {
		t.Fatal("arrival timestamp shares internal state")
	}
}
