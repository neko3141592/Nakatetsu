package ctc

import (
	"maps"
	"reflect"
	"testing"
)

func TestHandleCircuitEntryBlock(t *testing.T) {
	definitions := map[string]TrackCircuitDefinition{
		"A": {ControlKind: TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: TrackControlBlock, ToCircuitID: "C"},
		"C": {ControlKind: TrackControlBlock},
		"D": {ControlKind: TrackControlInterlocking},
		"E": {ControlKind: TrackControlBlock, ToCircuitID: "missing"},
		"F": {ControlKind: TrackControlUnknown},
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
			state := TrackingState{OccupiedByCircuitID: tt.previous, DynamicLinks: tt.links}
			before := maps.Clone(tt.previous)
			handleCircuitEntry(tt.circuitID, &state, definitions, nil, nil, nil)
			if state.NeedsReset != tt.wantReset {
				t.Fatalf("NeedsReset = %v, want %v", state.NeedsReset, tt.wantReset)
			}
			if !reflect.DeepEqual(state.DynamicLinks, tt.wantLinks) {
				t.Fatalf("DynamicLinks = %v, want %v", state.DynamicLinks, tt.wantLinks)
			}
			if !reflect.DeepEqual(state.OccupiedByCircuitID, before) {
				t.Fatal("previous occupancy was modified")
			}
		})
	}
}

func TestUpdateTrackingBlockEntry(t *testing.T) {
	state := TrackingState{
		OccupiedByCircuitID: map[string]bool{"A": true, "B": false, "C": true},
		DynamicLinks:        map[string]string{"A": "B"},
		TrainsByID: map[string]TrainState{
			"101F": {CircuitID: "A"},
			"103F": {CircuitID: "C"},
		},
	}
	definitions := map[string]TrackCircuitDefinition{
		"A": {ControlKind: TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: TrackControlBlock, ToCircuitID: "C"},
		"C": {ControlKind: TrackControlBlock},
	}

	updateTracking(&state, map[string]bool{"A": true, "B": true, "C": true}, definitions, nil, nil, nil)
	if state.NeedsReset || !reflect.DeepEqual(state.DynamicLinks, map[string]string{"A": "B", "B": "C"}) {
		t.Fatalf("unexpected tracking state: %+v", state)
	}
	if state.TrainsByID["101F"].CircuitID != "A" || state.TrainsByID["103F"].CircuitID != "C" {
		t.Fatal("entry moved a train before its source circuit cleared")
	}
}

func TestHandleCircuitEntryInterlocking(t *testing.T) {
	routes := []TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "end", TrackCircuitIDs: []string{"A", "B"}},
		{InterlockingStationID: "S0", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "X"}},
		{InterlockingStationID: "S1", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "C"}},
	}
	refs := buildRouteCircuitRefs(routes)
	key := RouteKey{StationID: "S1", RouteID: "R"}
	endKey := RouteKey{StationID: "S1", RouteID: "end"}
	open := InterlockingRouteState{IsAvailable: true, PathEstablished: true}
	tests := []struct {
		name        string
		fixedExit   string
		routeStates map[RouteKey]InterlockingRouteState
		links       map[string]string
		wantLinks   map[string]string
		wantReset   bool
	}{
		{
			name:      "established_route_overrides_fixed_exit_after_signal_returns_to_stop",
			fixedExit: "D", routeStates: map[RouteKey]InterlockingRouteState{key: open},
			wantLinks: map[string]string{"B": "C"},
		},
		{
			name: "unavailable_route_uses_fixed_exit", fixedExit: "D",
			routeStates: map[RouteKey]InterlockingRouteState{key: {PathEstablished: true}},
			wantLinks:   map[string]string{"B": "D"},
		},
		{
			name: "locked_but_unestablished_route_uses_fixed_exit", fixedExit: "D",
			routeStates: map[RouteKey]InterlockingRouteState{key: {IsAvailable: true, IsRouteSet: true, RouteLocked: true}},
			wantLinks:   map[string]string{"B": "D"},
		},
		{
			name: "route_end_uses_fixed_exit", fixedExit: "D",
			routeStates: map[RouteKey]InterlockingRouteState{endKey: open},
			wantLinks:   map[string]string{"B": "D"},
		},
		{
			name: "route_end_does_not_hide_outgoing_route", fixedExit: "D",
			routeStates: map[RouteKey]InterlockingRouteState{endKey: open, key: open},
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
			routeStates: map[RouteKey]InterlockingRouteState{endKey: open},
		},
		{
			name:        "existing_link_is_kept_when_route_changes",
			routeStates: map[RouteKey]InterlockingRouteState{key: open},
			links:       map[string]string{"B": "D"}, wantLinks: map[string]string{"B": "D"},
		},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			definitions := map[string]TrackCircuitDefinition{
				"B": {ControlKind: TrackControlInterlocking, ToCircuitID: tt.fixedExit},
				"C": {ControlKind: TrackControlBlock},
				"D": {ControlKind: TrackControlBlock},
				"X": {ControlKind: TrackControlBlock},
			}
			state := TrackingState{DynamicLinks: tt.links}
			handleCircuitEntry("B", &state, definitions, routes, refs, tt.routeStates)
			if state.NeedsReset != tt.wantReset || !reflect.DeepEqual(state.DynamicLinks, tt.wantLinks) {
				t.Fatalf("NeedsReset = %v, links = %v; want %v, %v", state.NeedsReset, state.DynamicLinks, tt.wantReset, tt.wantLinks)
			}
		})
	}
}

func TestUpdateTrackingInterlockingEntry(t *testing.T) {
	routes := []TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "C"}},
	}
	definitions := map[string]TrackCircuitDefinition{
		"B": {ControlKind: TrackControlInterlocking},
		"C": {ControlKind: TrackControlBlock},
	}
	state := TrackingState{}
	routeStates := map[RouteKey]InterlockingRouteState{
		{StationID: "S1", RouteID: "R"}: {IsAvailable: true, PathEstablished: true},
	}
	updateTracking(&state, map[string]bool{"B": true}, definitions, routes, buildRouteCircuitRefs(routes), routeStates)
	if state.NeedsReset || state.DynamicLinks["B"] != "C" {
		t.Fatalf("unexpected tracking state: %+v", state)
	}
}

func TestHandleCircuitExit(t *testing.T) {
	tests := []struct {
		name          string
		sourceID      string
		hasLink       bool
		occupied      map[string]bool
		wantCircuitID string
		wantLink      bool
		wantReset     bool
	}{
		{
			name: "move_train_and_consume_link", sourceID: "A", hasLink: true,
			occupied: map[string]bool{"B": true}, wantCircuitID: "B",
		},
		{
			name: "clear_destination_requires_reset", sourceID: "A", hasLink: true,
			occupied: map[string]bool{"B": false}, wantCircuitID: "A",
			wantLink: true, wantReset: true,
		},
		{
			name: "missing_destination_occupancy_requires_reset", sourceID: "A", hasLink: true,
			wantCircuitID: "A", wantLink: true, wantReset: true,
		},
		{
			name: "no_train_at_source_only_removes_link", sourceID: "D", hasLink: true,
			wantCircuitID: "D",
		},
		{
			name: "no_link_keeps_train_position", sourceID: "A",
			occupied: map[string]bool{"B": true}, wantCircuitID: "A",
		},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			train := TrainState{CircuitID: tt.sourceID, ConsistNumber: "101F", TrainNumber: "101", OperationNumber: "01"}
			otherTrain := TrainState{CircuitID: "C", ConsistNumber: "103F"}
			state := TrackingState{
				OccupiedByCircuitID: map[string]bool{"A": true},
				TrainsByID:          map[string]TrainState{"train1": train, "train2": otherTrain},
				DynamicLinks:        map[string]string{"C": "D"},
			}
			if tt.hasLink {
				state.DynamicLinks["A"] = "B"
			}
			handleCircuitExit("A", &state, tt.occupied)
			train.CircuitID = tt.wantCircuitID
			if state.TrainsByID["train1"] != train || state.TrainsByID["train2"] != otherTrain {
				t.Fatalf("unexpected trains: %+v", state.TrainsByID)
			}
			wantLinks := map[string]string{"C": "D"}
			if tt.wantLink {
				wantLinks["A"] = "B"
			}
			if state.NeedsReset != tt.wantReset || !reflect.DeepEqual(state.DynamicLinks, wantLinks) {
				t.Fatalf("NeedsReset = %v, links = %v; want %v, %v", state.NeedsReset, state.DynamicLinks, tt.wantReset, wantLinks)
			}
			if !reflect.DeepEqual(state.OccupiedByCircuitID, map[string]bool{"A": true}) {
				t.Fatal("previous occupancy was modified")
			}
		})
	}
}

func TestUpdateTrackingCircuitExitUsesCurrentOccupancy(t *testing.T) {
	state := TrackingState{
		OccupiedByCircuitID: map[string]bool{"A": true, "B": false, "C": true},
		DynamicLinks:        map[string]string{"A": "B"},
		TrainsByID: map[string]TrainState{
			"101F": {CircuitID: "A"},
			"103F": {CircuitID: "C"},
		},
	}
	definitions := map[string]TrackCircuitDefinition{
		"A": {ControlKind: TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: TrackControlBlock, ToCircuitID: "C"},
		"C": {ControlKind: TrackControlBlock},
	}
	updateTracking(&state, map[string]bool{"A": false, "B": true, "C": true}, definitions, nil, nil, nil)
	if state.NeedsReset || state.TrainsByID["101F"].CircuitID != "B" || state.TrainsByID["103F"].CircuitID != "C" {
		t.Fatalf("unexpected tracking state: %+v", state)
	}
	if !reflect.DeepEqual(state.DynamicLinks, map[string]string{"B": "C"}) {
		t.Fatalf("unexpected links: %v", state.DynamicLinks)
	}
}
