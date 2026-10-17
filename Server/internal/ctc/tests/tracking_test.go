package tests

import (
	"maps"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func TestHandleCircuitEntryBlock(t *testing.T) {
	definitions := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "C"},
		"C": {ControlKind: ctc.TrackControlBlock},
		"D": {ControlKind: ctc.TrackControlInterlocking},
		"E": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "missing"},
		"F": {ControlKind: ctc.TrackControlUnknown},
	}
	tests := []struct {
		name      string
		circuitID string
		previous  map[string]bool
		links     map[string]string
		wantLinks map[string]string
		wantReset bool
	}{
		{
			name: "create_link_from_current_circuit", circuitID: "A",
			wantLinks: map[string]string{"A": "B"},
		},
		{
			name: "next_circuit_occupancy_does_not_change_link", circuitID: "B",
			previous:  map[string]bool{"A": true, "C": true},
			wantLinks: map[string]string{"B": "C"},
		},
		{
			name: "terminal_does_not_create_link", circuitID: "C",
		},
		{
			name: "matching_existing_link_is_kept", circuitID: "A",
			links: map[string]string{"A": "B"}, wantLinks: map[string]string{"A": "B"},
		},
		{
			name: "conflicting_link_is_preserved", circuitID: "A",
			links: map[string]string{"A": "C"}, wantLinks: map[string]string{"A": "C"},
		},
		{
			name: "interlocking_without_route_or_exit_keeps_links", circuitID: "D",
			links: map[string]string{"A": "B"}, wantLinks: map[string]string{"A": "B"},
		},
		{
			name: "missing_destination_requires_reset", circuitID: "E", wantReset: true,
		},
		{
			name: "unknown_control_kind_requires_reset", circuitID: "F", wantReset: true,
		},
		{
			name: "missing_definition_requires_reset", circuitID: "missing", wantReset: true,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			state := ctc.TrackingState{CircuitsByID: circuitStates(tt.previous), DynamicLinks: tt.links}
			before := maps.Clone(state.CircuitsByID)
			ctc.UpdateTracking(&state, map[string]bool{tt.circuitID: true}, definitions, nil, nil, nil)
			if state.NeedsReset != tt.wantReset {
				t.Fatalf("NeedsReset = %v, want %v", state.NeedsReset, tt.wantReset)
			}
			if !reflect.DeepEqual(state.DynamicLinks, tt.wantLinks) {
				t.Fatalf("DynamicLinks = %v, want %v", state.DynamicLinks, tt.wantLinks)
			}
			if !reflect.DeepEqual(state.CircuitsByID, before) {
				t.Fatal("previous occupancy was modified")
			}
		})
	}
}

func TestUpdateTrackingBlockEntry(t *testing.T) {
	state := ctc.TrackingState{
		CircuitsByID: map[string]ctc.TrackCircuitState{
			"A": {IsOccupied: true, TrainID: "101F"},
			"B": {},
			"C": {IsOccupied: true, TrainID: "103F"},
		},
		DynamicLinks: map[string]string{"A": "B"},
	}
	definitions := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "C"},
		"C": {ControlKind: ctc.TrackControlBlock},
	}

	ctc.UpdateTracking(&state, map[string]bool{"A": true, "B": true, "C": true}, definitions, nil, nil, nil)
	if state.NeedsReset || !reflect.DeepEqual(state.DynamicLinks, map[string]string{"A": "B", "B": "C"}) {
		t.Fatalf("unexpected tracking state: %+v", state)
	}
	if state.CircuitsByID["A"].TrainID != "101F" || state.CircuitsByID["B"].TrainID != "" || state.CircuitsByID["C"].TrainID != "103F" {
		t.Fatal("entry moved a train before its source circuit cleared")
	}
}

func TestHandleCircuitEntryInterlocking(t *testing.T) {
	routes := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "end", TrackCircuitIDs: []string{"A", "B"}},
		{InterlockingStationID: "S0", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "X"}},
		{InterlockingStationID: "S1", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "C"}},
	}
	refs := ctc.BuildRouteCircuitRefs(routes)
	key := ctc.RouteKey{StationID: "S1", RouteID: "R"}
	endKey := ctc.RouteKey{StationID: "S1", RouteID: "end"}
	open := ctc.InterlockingRouteState{IsAvailable: true, PathEstablished: true}
	tests := []struct {
		name        string
		fixedExit   string
		routeStates map[ctc.RouteKey]ctc.InterlockingRouteState
		links       map[string]string
		wantLinks   map[string]string
		wantReset   bool
	}{
		{
			name:      "established_route_overrides_fixed_exit_after_signal_returns_to_stop",
			fixedExit: "D", routeStates: map[ctc.RouteKey]ctc.InterlockingRouteState{key: open},
			wantLinks: map[string]string{"B": "C"},
		},
		{
			name: "unavailable_route_uses_fixed_exit", fixedExit: "D",
			routeStates: map[ctc.RouteKey]ctc.InterlockingRouteState{key: {PathEstablished: true}},
			wantLinks:   map[string]string{"B": "D"},
		},
		{
			name: "locked_but_unestablished_route_uses_fixed_exit", fixedExit: "D",
			routeStates: map[ctc.RouteKey]ctc.InterlockingRouteState{key: {IsAvailable: true, IsRouteSet: true, RouteLocked: true}},
			wantLinks:   map[string]string{"B": "D"},
		},
		{
			name: "route_end_uses_fixed_exit", fixedExit: "D",
			routeStates: map[ctc.RouteKey]ctc.InterlockingRouteState{endKey: open},
			wantLinks:   map[string]string{"B": "D"},
		},
		{
			name: "route_end_does_not_hide_outgoing_route", fixedExit: "D",
			routeStates: map[ctc.RouteKey]ctc.InterlockingRouteState{endKey: open, key: open},
			wantLinks:   map[string]string{"B": "C"},
		},
		{
			name: "missing_route_state_uses_fixed_exit", fixedExit: "D",
			wantLinks: map[string]string{"B": "D"},
		},
		{
			name:  "no_route_or_exit_keeps_existing_links",
			links: map[string]string{"A": "B"}, wantLinks: map[string]string{"A": "B"},
		},
		{
			name:        "route_end_without_fixed_exit_creates_no_link",
			routeStates: map[ctc.RouteKey]ctc.InterlockingRouteState{endKey: open},
		},
		{
			name:        "existing_link_is_kept_when_route_changes",
			routeStates: map[ctc.RouteKey]ctc.InterlockingRouteState{key: open},
			links:       map[string]string{"B": "D"}, wantLinks: map[string]string{"B": "D"},
		},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			definitions := map[string]ctc.TrackCircuitDefinition{
				"B": {ControlKind: ctc.TrackControlInterlocking, ToCircuitID: tt.fixedExit},
				"C": {ControlKind: ctc.TrackControlBlock},
				"D": {ControlKind: ctc.TrackControlBlock},
				"X": {ControlKind: ctc.TrackControlBlock},
			}
			state := ctc.TrackingState{DynamicLinks: tt.links}
			ctc.UpdateTracking(&state, map[string]bool{"B": true}, definitions, routes, refs, tt.routeStates)
			if state.NeedsReset != tt.wantReset || !reflect.DeepEqual(state.DynamicLinks, tt.wantLinks) {
				t.Fatalf("NeedsReset = %v, links = %v; want %v, %v", state.NeedsReset, state.DynamicLinks, tt.wantReset, tt.wantLinks)
			}
		})
	}
}

func TestUpdateTrackingInterlockingEntry(t *testing.T) {
	routes := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "C"}},
	}
	definitions := map[string]ctc.TrackCircuitDefinition{
		"B": {ControlKind: ctc.TrackControlInterlocking},
		"C": {ControlKind: ctc.TrackControlBlock},
	}
	state := ctc.TrackingState{}
	routeStates := map[ctc.RouteKey]ctc.InterlockingRouteState{
		{StationID: "S1", RouteID: "R"}: {IsAvailable: true, PathEstablished: true},
	}
	ctc.UpdateTracking(&state, map[string]bool{"B": true}, definitions, routes, ctc.BuildRouteCircuitRefs(routes), routeStates)
	if state.NeedsReset || state.DynamicLinks["B"] != "C" {
		t.Fatalf("unexpected tracking state: %+v", state)
	}
}

func TestHandleCircuitExit(t *testing.T) {
	tests := []struct {
		name               string
		sourceID           string
		destinationTrainID string
		hasLink            bool
		occupied           map[string]bool
		wantCircuitID      string
		wantLink           bool
		wantReset          bool
	}{
		{name: "move_train_and_consume_link", sourceID: "A", hasLink: true,
			occupied: map[string]bool{"B": true}, wantCircuitID: "B"},
		{name: "clear_destination_requires_reset", sourceID: "A", hasLink: true,
			occupied: map[string]bool{"B": false}, wantCircuitID: "A", wantLink: true, wantReset: true},
		{name: "missing_destination_occupancy_requires_reset", sourceID: "A", hasLink: true,
			wantCircuitID: "A", wantLink: true, wantReset: true},
		{name: "no_train_at_source_only_removes_link", sourceID: "D", hasLink: true,
			wantCircuitID: "D"},
		{name: "no_link_keeps_train_position", sourceID: "A",
			occupied: map[string]bool{"B": true}, wantCircuitID: "A"},
		{name: "different_train_at_destination_is_not_overwritten", sourceID: "A", hasLink: true,
			destinationTrainID: "target", occupied: map[string]bool{"B": true},
			wantCircuitID: "A", wantLink: true, wantReset: true},
		{name: "same_train_at_destination_is_consolidated", sourceID: "A", hasLink: true,
			destinationTrainID: "train1", occupied: map[string]bool{"B": true}, wantCircuitID: "B"},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			state := ctc.TrackingState{
				CircuitsByID: map[string]ctc.TrackCircuitState{
					"A": {IsOccupied: true},
					"B": {IsOccupied: true, TrainID: tt.destinationTrainID},
					"C": {IsOccupied: true, TrainID: "train2"},
					"D": {IsOccupied: true},
				},
				TrainsByID: map[string]ctc.TrainState{
					"train1": {ConsistNumber: "101F", TrainNumber: "101", OperationNumber: "01"},
					"train2": {ConsistNumber: "103F"},
					"target": {ConsistNumber: "105F"},
				},
				DynamicLinks: map[string]string{"C": "D"},
			}
			from := state.CircuitsByID[tt.sourceID]
			from.TrainID = "train1"
			state.CircuitsByID[tt.sourceID] = from
			beforeTrains := maps.Clone(state.TrainsByID)
			if tt.hasLink {
				state.DynamicLinks["A"] = "B"
			}
			occupied := maps.Clone(tt.occupied)
			if occupied == nil {
				occupied = make(map[string]bool)
			}
			occupied["A"] = false
			ctc.UpdateTracking(&state, occupied, nil, nil, nil, nil)
			if state.CircuitsByID[tt.wantCircuitID].TrainID != "train1" {
				t.Fatalf("train1 did not remain at %s: %+v", tt.wantCircuitID, state.CircuitsByID)
			}
			if tt.wantCircuitID == "B" && state.CircuitsByID["A"].TrainID != "" {
				t.Fatal("source kept the transferred TrainID")
			}
			if tt.destinationTrainID == "target" && state.CircuitsByID["B"].TrainID != "target" {
				t.Fatal("another train at destination was overwritten")
			}
			if state.CircuitsByID["C"].TrainID != "train2" || !reflect.DeepEqual(state.TrainsByID, beforeTrains) {
				t.Fatal("unrelated position or train metadata was changed")
			}
			wantLinks := map[string]string{"C": "D"}
			if tt.wantLink {
				wantLinks["A"] = "B"
			}
			if state.NeedsReset != tt.wantReset || !reflect.DeepEqual(state.DynamicLinks, wantLinks) {
				t.Fatalf("NeedsReset = %v, links = %v; want %v, %v", state.NeedsReset, state.DynamicLinks, tt.wantReset, wantLinks)
			}
			for id, circuit := range state.CircuitsByID {
				if !circuit.IsOccupied {
					t.Fatalf("previous occupancy was modified: %s", id)
				}
			}
		})
	}
}

func TestUpdateTrackingCircuitExitUsesCurrentOccupancy(t *testing.T) {
	state := ctc.TrackingState{
		CircuitsByID: map[string]ctc.TrackCircuitState{
			"A": {IsOccupied: true, TrainID: "101F"},
			"B": {},
			"C": {IsOccupied: true, TrainID: "103F"},
		},
		DynamicLinks: map[string]string{"A": "B"},
	}
	definitions := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "C"},
		"C": {ControlKind: ctc.TrackControlBlock},
	}
	ctc.UpdateTracking(&state, map[string]bool{"A": false, "B": true, "C": true}, definitions, nil, nil, nil)
	if state.NeedsReset || state.CircuitsByID["A"].TrainID != "" || state.CircuitsByID["B"].TrainID != "101F" || state.CircuitsByID["C"].TrainID != "103F" {
		t.Fatalf("unexpected tracking state: %+v", state)
	}
	if !reflect.DeepEqual(state.DynamicLinks, map[string]string{"B": "C"}) {
		t.Fatalf("unexpected links: %v", state.DynamicLinks)
	}
}

func circuitStates(occupied map[string]bool) map[string]ctc.TrackCircuitState {
	if occupied == nil {
		return nil
	}
	circuits := make(map[string]ctc.TrackCircuitState, len(occupied))
	for id, value := range occupied {
		circuits[id] = ctc.TrackCircuitState{IsOccupied: value}
	}
	return circuits
}
