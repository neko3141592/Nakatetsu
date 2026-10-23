package prc

import (
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
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
	if got := buildConflictPoints(routes); !reflect.DeepEqual(got, want) {
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
		if got := buildConflictPoints(routes); len(got) != 0 {
			t.Fatalf("conflict points = %v, want no conflict points", got)
		}
	}
}
