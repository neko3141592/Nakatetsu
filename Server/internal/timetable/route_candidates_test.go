package timetable

import (
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func TestFindRouteCandidates(t *testing.T) {
	route := func(id string, circuits ...string) ctc.TrackRouteDefinition {
		return ctc.TrackRouteDefinition{InterlockingStationID: "S", InterlockingRouteID: id, TrackCircuitIDs: circuits}
	}
	key := func(id string) ctc.RouteKey { return ctc.RouteKey{StationID: "S", RouteID: id} }
	tests := []struct {
		name   string
		routes []ctc.TrackRouteDefinition
		want   [][]ctc.RouteKey
	}{
		{
			name: "branches share intermediate circuits",
			routes: []ctc.TrackRouteDefinition{
				route("first", "A", "M", "B"),
				route("left", "B", "N", "C"),
				route("right", "B", "N", "D"),
			},
			want: [][]ctc.RouteKey{{key("first"), key("left")}, {key("first"), key("right")}},
		},
		{
			name: "reject previously visited intermediate circuit",
			routes: []ctc.TrackRouteDefinition{
				route("first", "A", "M", "B"),
				route("cycle", "B", "M", "T"),
				route("valid", "B", "N", "T"),
			},
			want: [][]ctc.RouteKey{{key("first"), key("valid")}},
		},
		{
			name: "reject previously visited destination circuit",
			routes: []ctc.TrackRouteDefinition{
				route("first", "A", "M", "B"),
				route("cycle", "B", "A"),
				route("valid", "B", "T"),
			},
			want: [][]ctc.RouteKey{{key("first"), key("valid")}},
		},
		{
			name:   "interlocking exit follows fixed connection",
			routes: []ctc.TrackRouteDefinition{route("exit", "A", "M", "X")},
			want:   [][]ctc.RouteKey{{key("exit")}},
		},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			circuits := make(map[string]ctc.TrackCircuitDefinition)
			for _, id := range []string{"A", "B", "M", "N", "T"} {
				circuits[id] = ctc.TrackCircuitDefinition{ControlKind: ctc.TrackControlInterlocking}
			}
			circuits["X"] = ctc.TrackCircuitDefinition{ControlKind: ctc.TrackControlInterlocking, ToCircuitID: "C"}
			for _, id := range []string{"C", "D"} {
				circuits[id] = ctc.TrackCircuitDefinition{ControlKind: ctc.TrackControlBlock, ToCircuitID: "T"}
			}
			got, err := FindRouteCandidates("A", "T", circuits, test.routes)
			if err != nil || !reflect.DeepEqual(got, test.want) {
				t.Fatalf("got=%v, err=%v, want=%v", got, err, test.want)
			}
		})
	}
}

func TestRouteCandidateErrorsAndCleanup(t *testing.T) {
	circuits := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlInterlocking},
		"M": {ControlKind: ctc.TrackControlInterlocking},
		"B": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "missing"},
		"T": {ControlKind: ctc.TrackControlBlock},
	}
	for _, ids := range [][]string{{"A", "missing", "T"}, {"A", "M", "B"}} {
		visited := make(map[string]bool)
		candidates := [][]ctc.RouteKey{}
		err := search("A", "T", visited, nil, &candidates, circuits, []ctc.TrackRouteDefinition{{TrackCircuitIDs: ids}}, nil)
		if err == nil || len(visited) != 0 || len(candidates) != 0 {
			t.Fatalf("err=%v, visited=%v, candidates=%v", err, visited, candidates)
		}
	}
	for _, endpoints := range [][2]string{{"missing", "T"}, {"A", "missing"}, {"A", "T"}} {
		if _, err := FindRouteCandidates(endpoints[0], endpoints[1], circuits, nil); err == nil {
			t.Fatalf("expected an error for %v", endpoints)
		}
	}
	got, err := FindRouteCandidates("T", "T", circuits, nil)
	if err != nil || len(got) != 1 || len(got[0]) != 0 {
		t.Fatalf("empty route candidate: got=%v, err=%v", got, err)
	}
}

func TestRouteKindsDoNotBypassExcludedRoutes(t *testing.T) {
	circuits := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlInterlocking, ToCircuitID: "T"},
		"B": {ControlKind: ctc.TrackControlInterlocking, ToCircuitID: "T"},
		"T": {ControlKind: ctc.TrackControlBlock},
	}
	routes := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S", InterlockingRouteID: "home", Kind: ctc.RouteKindHome, TrackCircuitIDs: []string{"A", "B"}},
		{InterlockingStationID: "S", InterlockingRouteID: "shunting", Kind: ctc.RouteKindShunting, TrackCircuitIDs: []string{"B", "T"}},
	}
	for _, kinds := range [][]ctc.RouteKind{{ctc.RouteKindHome, ctc.RouteKindDeparture}, {ctc.RouteKindShunting}} {
		if _, err := FindRouteCandidates("A", "T", circuits, routes, kinds...); err == nil {
			t.Fatalf("excluded route bypassed with allowed kinds %v", kinds)
		}
	}
}

func TestBlockCircuitHomeRoute(t *testing.T) {
	circuits := map[string]ctc.TrackCircuitDefinition{
		"approach": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "entry"},
		"entry":    {ControlKind: ctc.TrackControlInterlocking},
		"platform": {ControlKind: ctc.TrackControlInterlocking},
	}
	routes := []ctc.TrackRouteDefinition{{
		InterlockingStationID: "S", InterlockingRouteID: "1R", Kind: ctc.RouteKindHome,
		TrackCircuitIDs: []string{"approach", "entry", "platform"},
	}}
	got, err := FindRouteCandidates("approach", "platform", circuits, routes, ctc.RouteKindHome)
	want := [][]ctc.RouteKey{{{StationID: "S", RouteID: "1R"}}}
	if err != nil || !reflect.DeepEqual(got, want) {
		t.Fatalf("got=%v, err=%v, want=%v", got, err, want)
	}
	// 入換限定の探索では、場内進路や固定接続を経由して入場しない。
	if _, err := FindRouteCandidates("approach", "entry", circuits, routes, ctc.RouteKindShunting); err == nil {
		t.Fatal("excluded block-start home route was bypassed")
	}
}
