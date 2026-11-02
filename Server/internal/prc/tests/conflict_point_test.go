package tests

import (
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
)

func TestBuildConflictPoints(t *testing.T) {
	routes := []ctc.TrackRouteDefinition{
		{
			InterlockingStationID: "S1", InterlockingRouteID: "A",
			TrackCircuitIDs: []string{"origin", "T1", "T2"}, RouteClearTrackCircuitIDs: []string{"T1", "T2"},
		},
		{
			InterlockingStationID: "S1", InterlockingRouteID: "B",
			TrackCircuitIDs: []string{"origin", "T2", "T3"}, RouteClearTrackCircuitIDs: []string{"T2", "T3"},
		},
		{
			InterlockingStationID: "S1", InterlockingRouteID: "C",
			TrackCircuitIDs: []string{"origin", "T3", "T4"}, RouteClearTrackCircuitIDs: []string{"T3", "T4", "T4"},
		},
		{
			InterlockingStationID: "S2", InterlockingRouteID: "A",
			TrackCircuitIDs: []string{"origin", "T2", "T5"}, RouteClearTrackCircuitIDs: []string{"T2", "T5"},
		},
	}
	// 同じ進路を再登録しても、Setの要素数は増えない。
	routes = append(routes, routes[0])
	want := map[string]map[ctc.RouteKey]struct{}{
		"T2": {
			{StationID: "S1", RouteID: "A"}: {},
			{StationID: "S1", RouteID: "B"}: {},
			{StationID: "S2", RouteID: "A"}: {},
		},
		"T3": {
			{StationID: "S1", RouteID: "B"}: {},
			{StationID: "S1", RouteID: "C"}: {},
		},
	}
	if got := prc.BuildConflictPoints(routes); !reflect.DeepEqual(got, want) {
		t.Fatalf("conflict points = %v, want %v", got, want)
	}
}

func TestBuildConflictPointsWithoutSharedCircuits(t *testing.T) {
	for _, routes := range [][]ctc.TrackRouteDefinition{
		nil,
		{{InterlockingStationID: "S1", InterlockingRouteID: "A", RouteClearTrackCircuitIDs: []string{"T1", "T1"}}},
		{{InterlockingStationID: "S1", InterlockingRouteID: "A", TrackCircuitIDs: []string{"T1"}},
			{InterlockingStationID: "S1", InterlockingRouteID: "B", TrackCircuitIDs: []string{"T1"}}},
	} {
		if got := prc.BuildConflictPoints(routes); len(got) != 0 {
			t.Fatalf("conflict points = %v, want no conflict points", got)
		}
	}
}

func TestBuildConflictUsages(t *testing.T) {
	home := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 10}, TrainNumber: "1001", ControlStationID: "S1", RouteID: "A"}
	departure := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 20}, TrainNumber: "1001", ControlStationID: "S1", RouteID: "B"}
	passing := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 30}, TrainNumber: "1002", ControlStationID: "S2", RouteID: "A"}
	shunting := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 40}, TrainNumber: "1003", ControlStationID: "S1", RouteID: "C"}
	zero := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 50}, TrainNumber: "1004", ControlStationID: "S1", RouteID: "A"}
	plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{
		"1001": {Points: []prc.PointPlan{{Handling: prc.HandlingStop, ArrivalSeconds: seconds(100), DepartureSeconds: seconds(140), ArrivalRoutes: []prc.RouteUsage{home}, DepartureRoutes: []prc.RouteUsage{departure}}}},
		"1002": {Points: []prc.PointPlan{{Handling: prc.HandlingPass, ArrivalSeconds: seconds(999), DepartureSeconds: seconds(120), ArrivalRoutes: []prc.RouteUsage{passing}}}},
		"1003": {Points: []prc.PointPlan{{DepartureSeconds: seconds(90100), ShuntingRoutes: []prc.RouteUsage{shunting}}}},
		"1004": {Points: []prc.PointPlan{{DepartureSeconds: seconds(0), DepartureRoutes: []prc.RouteUsage{zero}}}},
	}}
	points := map[string]map[ctc.RouteKey]struct{}{
		"X":      {{StationID: "S1", RouteID: "A"}: {}, {StationID: "S1", RouteID: "B"}: {}, {StationID: "S2", RouteID: "A"}: {}},
		"Y":      {{StationID: "S1", RouteID: "B"}: {}, {StationID: "S1", RouteID: "C"}: {}},
		"unused": {{StationID: "S9", RouteID: "A"}: {}},
	}
	want := map[string][]prc.ConflictUsage{
		"X":      {{Usage: zero, ScheduledSeconds: 0}, {Usage: home, ScheduledSeconds: 100}, {Usage: passing, ScheduledSeconds: 120}, {Usage: departure, ScheduledSeconds: 140}},
		"Y":      {{Usage: departure, ScheduledSeconds: 140}, {Usage: shunting, ScheduledSeconds: 90100}},
		"unused": nil,
	}
	got, err := prc.BuildConflictUsages(plan, points)
	if err != nil || !reflect.DeepEqual(got, want) {
		t.Fatalf("conflict usages=%v, err=%v, want=%v", got, err, want)
	}
}

func TestBuildConflictUsagesRejectsMissingTime(t *testing.T) {
	route := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 10}, TrainNumber: "1001", ControlStationID: "S1", RouteID: "A"}
	for _, point := range []prc.PointPlan{
		{Handling: prc.HandlingStop, ArrivalRoutes: []prc.RouteUsage{route}},
		{Handling: prc.HandlingPass, ArrivalSeconds: seconds(100), ArrivalRoutes: []prc.RouteUsage{route}},
		{DepartureRoutes: []prc.RouteUsage{route}},
		{ShuntingRoutes: []prc.RouteUsage{route}},
	} {
		plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{"1001": {Points: []prc.PointPlan{point}}}}
		points := map[string]map[ctc.RouteKey]struct{}{"X": {{StationID: "S1", RouteID: "A"}: {}}}
		if got, err := prc.BuildConflictUsages(plan, points); err == nil || got != nil {
			t.Fatalf("missing time accepted: got=%v, err=%v", got, err)
		}
	}
}
