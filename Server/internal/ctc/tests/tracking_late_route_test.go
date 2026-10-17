package tests

import (
	"path/filepath"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func TestOccupiedBlockCreatesMissingLinkAndPreservesExistingLink(t *testing.T) {
	definitions := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock},
		"C": {ControlKind: ctc.TrackControlBlock},
	}
	for _, existingTarget := range []string{"", "C"} {
		t.Run("existing="+existingTarget, func(t *testing.T) {
			state := ctc.TrackingState{
				CircuitsByID: map[string]ctc.TrackCircuitState{"A": {IsOccupied: true, TrainID: "101F"}},
			}
			if existingTarget != "" {
				state.DynamicLinks = map[string]string{"A": existingTarget}
			}
			ctc.UpdateTracking(&state, map[string]bool{"A": true}, definitions, nil, nil, nil)
			wantTarget := existingTarget
			if wantTarget == "" {
				wantTarget = "B"
			}
			if state.NeedsReset || state.DynamicLinks["A"] != wantTarget || state.CircuitsByID["A"].TrainID != "101F" {
				t.Fatalf("unchanged occupied circuit: %+v", state)
			}
		})
	}
}

func TestNakatetsuTrainTracksAfterLateRouteOpeningAndExitsToBlock(t *testing.T) {
	networkPath := filepath.Join("..", "..", "..", "..", "SourceAssets", "Track", "Network")
	definitions, err := ctc.LoadTrackCircuitDefinitions(filepath.Join(networkPath, "NakatetsuTrackGraph", "graph.json"))
	if err != nil {
		t.Fatal(err)
	}
	routes, err := ctc.LoadTrackRouteDefinitions(filepath.Join(networkPath, "NakatetsuInterlocking", "interlocking.json"), definitions)
	if err != nil {
		t.Fatal(err)
	}
	core := ctc.New(definitions, routes)
	occupied := make(map[string]bool, len(definitions))
	for id := range definitions {
		occupied[id] = false
	}
	route := &ctcv1.InterlockingRouteStatus{IsAvailable: true}
	snapshot := &ctcv1.UnitySnapshot{
		WorldTime: &ctcv1.WorldTimeInformation{},
		Interlocking: &ctcv1.InterlockingSnapshot{StationsById: map[string]*ctcv1.InterlockingStationStatus{
			"NT01": {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"3R": route}},
		}},
		TrackCircuits: &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: occupied},
		TrainInitialization: &ctcv1.TrainInitializationSnapshot{Trains: []*ctcv1.TrainInitializationInfo{{
			ConsistId: "10001F", TrainNumber: "101", ReferenceCircuitId: "NT01_3RT",
		}}},
	}
	var trainID string
	apply := func(label, wantCircuit string, occupiedIDs ...string) {
		t.Helper()
		for id := range occupied {
			occupied[id] = false
		}
		for _, id := range occupiedIDs {
			occupied[id] = true
		}
		snapshot.WorldTime.TickCount++
		if err := core.UpdateSnapshot(snapshot); err != nil {
			t.Fatalf("%s: %v", label, err)
		}
		state := core.GetTrackingState()
		if trainID == "" {
			trainID = state.CircuitsByID[wantCircuit].TrainID
		}
		if !state.IsInitialized || state.NeedsReset || trainID == "" || state.CircuitsByID[wantCircuit].TrainID != trainID {
			t.Fatalf("%s: train did not track to %s: %+v", label, wantCircuit, state)
		}
		for id, circuit := range state.CircuitsByID {
			if id != wantCircuit && circuit.TrainID != "" {
				t.Fatalf("%s: stale TrainID remained on %s", label, id)
			}
		}
		if len(state.TrainsByID) != 1 || state.TrainsByID[trainID].TrainNumber != "101" {
			t.Fatalf("%s: train identity or number changed", label)
		}
	}

	apply("initial route closed", "NT01_3RT", "NT01_3RT")
	apply("still waiting for route", "NT01_3RT", "NT01_3RT")
	if len(core.GetTrackingState().DynamicLinks) != 0 {
		t.Fatal("created a link before the route opened")
	}
	route.PathEstablished = true
	apply("route opens without occupancy change", "NT01_3RT", "NT01_3RT")
	if core.GetTrackingState().DynamicLinks["NT01_3RT"] != "NT01_22AT" {
		t.Fatal("late route opening did not create the departure link")
	}
	apply("enter 22AT", "NT01_3RT", "NT01_3RT", "NT01_22AT")
	apply("clear 3RT", "NT01_22AT", "NT01_22AT")
	// 進路状態が落ちても既存リンクを保持し、進路終端からは固定出口を使う。
	route.PathEstablished = false
	apply("enter 22BT", "NT01_22AT", "NT01_22AT", "NT01_22BT")
	apply("clear 22AT", "NT01_22BT", "NT01_22BT")
	apply("enter D1T", "NT01_22BT", "NT01_22BT", "NT01_NT02_D1T")
	apply("clear route exit", "NT01_NT02_D1T", "NT01_NT02_D1T")
	apply("enter D2T", "NT01_NT02_D1T", "NT01_NT02_D1T", "NT01_NT02_D2T")
	apply("clear D1T", "NT01_NT02_D2T", "NT01_NT02_D2T")
}
