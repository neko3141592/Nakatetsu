package tests

import (
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func TestBuildRouteCircuitRefs(t *testing.T) {
	routes := []ctc.TrackRouteDefinition{
		{TrackCircuitIDs: []string{"A", "B", "C"}},
		{TrackCircuitIDs: []string{"C", "B", "D"}},
		{},
	}
	want := map[string][]ctc.RouteCircuitRef{
		"A": {{RouteIndex: 0, CircuitIndex: 0}},
		"B": {{RouteIndex: 0, CircuitIndex: 1}, {RouteIndex: 1, CircuitIndex: 1}},
		"C": {{RouteIndex: 0, CircuitIndex: 2}, {RouteIndex: 1, CircuitIndex: 0}},
		"D": {{RouteIndex: 1, CircuitIndex: 2}},
	}
	if got := ctc.BuildRouteCircuitRefs(routes); !reflect.DeepEqual(got, want) {
		t.Fatalf("route refs = %v, want %v", got, want)
	}
}
