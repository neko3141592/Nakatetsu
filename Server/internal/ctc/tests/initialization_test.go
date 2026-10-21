package tests

import (
	"context"
	"errors"
	"reflect"
	"strings"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/communication"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/proto"
)

func TestTrainInitializationRegistersAndThenTracksWithoutTrainNumber(t *testing.T) {
	core := initializationCore()
	snapshot := initializationSnapshot()
	snapshot.TrackCircuits.OccupiedByCircuitId["A"] = true
	snapshot.TrainInitialization.Trains = []*ctcv1.TrainInitializationInfo{
		{ConsistId: "unity-1", ConsistNumber: "101F", OperationNumber: "01", ReferenceCircuitId: "A"},
	}
	if err := core.UpdateSnapshot(snapshot); err != nil {
		t.Fatal(err)
	}
	state := core.GetTrackingState()
	trainID := state.CircuitsByID["A"].TrainID
	if !state.IsInitialized || state.NeedsReset || trainID == "" || len(state.TrainsByID) != 1 {
		t.Fatalf("initial registration failed: %+v", state)
	}
	train := state.TrainsByID[trainID]
	if train.ConsistID == nil || *train.ConsistID != "unity-1" || train.ConsistNumber != "101F" || train.TrainNumber != "" || train.OperationNumber != "01" {
		t.Fatalf("wrong train metadata: %+v", train)
	}
	if !state.CircuitsByID["A"].IsOccupied || state.DynamicLinks["A"] != "B" {
		t.Fatalf("initial snapshot did not create occupancy and outgoing link: %+v", state)
	}

	// 受信元や取得結果を変えても、CTC内部の情報は変わらない。
	snapshot.TrainInitialization.Trains[0].ConsistNumber = "changed"
	*train.ConsistID = "changed"
	state.TrainsByID[trainID] = ctc.TrainState{}
	state.CircuitsByID["A"] = ctc.TrackCircuitState{}
	state.DynamicLinks["A"] = "changed"
	if got := core.GetTrackingState(); *got.TrainsByID[trainID].ConsistID != "unity-1" || got.TrainsByID[trainID].ConsistNumber != "101F" || got.CircuitsByID["A"].TrainID != trainID || got.DynamicLinks["A"] != "B" {
		t.Fatal("tracking state shares mutable storage")
	}

	// 古い初期位置や変更された列番が来ても、登録済み列車を作り直さない。
	snapshot.TrackCircuits.OccupiedByCircuitId["A"] = false
	snapshot.TrackCircuits.OccupiedByCircuitId["B"] = true
	snapshot.TrainInitialization.Trains[0].TrainNumber = "123"
	if err := core.UpdateSnapshot(snapshot); err != nil {
		t.Fatalf("normal tracking revalidated the initial circuit: %v", err)
	}
	got := core.GetTrackingState()
	if got.NeedsReset || got.CircuitsByID["A"].TrainID != "" || got.CircuitsByID["B"].TrainID != trainID || len(got.TrainsByID) != 1 || got.TrainsByID[trainID].TrainNumber != "" {
		t.Fatalf("registration was reapplied or tracking failed: %+v", got)
	}
	// 初期情報が省略された通常snapshotでも継続できる。
	snapshot.TrainInitialization = nil
	core.UpdateSnapshot(snapshot)
	if got := core.GetTrackingState(); !got.IsInitialized || got.CircuitsByID["B"].TrainID != trainID {
		t.Fatalf("normal snapshot lost registration: %+v", got)
	}
}

func TestTrainInitializationRejectsInvalidSettingsWithoutPartialRegistration(t *testing.T) {
	for _, tt := range []struct {
		name      string
		edit      func(*ctcv1.UnitySnapshot)
		wantError bool
	}{
		{"not_received", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization = nil }, false},
		{"empty_list_with_occupied_circuits", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains = nil }, true},
		{"nil_train", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains[1] = nil }, true},
		{"blank_consist_id", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains[1].ConsistId = " " }, true},
		{"duplicate_consist_id", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains[1].ConsistId = "unity-1" }, true},
		{"blank_reference_circuit", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains[1].ReferenceCircuitId = " " }, true},
		{"duplicate_reference_circuit", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains[1].ReferenceCircuitId = "A" }, true},
		{"unknown_reference_circuit", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains[1].ReferenceCircuitId = "missing" }, true},
		{"clear_reference_circuit", func(s *ctcv1.UnitySnapshot) { s.TrackCircuits.OccupiedByCircuitId["B"] = false }, true},
		{"unassigned_occupied_circuit", func(s *ctcv1.UnitySnapshot) { s.TrainInitialization.Trains = s.TrainInitialization.Trains[:1] }, true},
	} {
		t.Run(tt.name, func(t *testing.T) {
			core := initializationCore()
			snapshot := initializationSnapshot()
			snapshot.TrackCircuits.OccupiedByCircuitId = map[string]bool{"A": true, "B": true}
			snapshot.TrainInitialization.Trains = []*ctcv1.TrainInitializationInfo{
				{ConsistId: "unity-1", ReferenceCircuitId: "A"},
				{ConsistId: "unity-2", ReferenceCircuitId: "B"},
			}
			tt.edit(snapshot)
			err := core.UpdateSnapshot(snapshot)
			if tt.wantError && !errors.Is(err, ctc.ErrTrainInitialization) {
				t.Fatalf("want initialization error, got %v", err)
			}
			if !tt.wantError && err != nil {
				t.Fatalf("unreceived information should wait: %v", err)
			}
			state := core.GetTrackingState()
			if state.IsInitialized || state.NeedsReset || len(state.CircuitsByID) != 0 || len(state.TrainsByID) != 0 || len(state.DynamicLinks) != 0 {
				t.Fatalf("invalid registration changed tracking state: %+v", state)
			}
			if !proto.Equal(core.GetUnitySnapshot(), snapshot) {
				t.Fatal("received snapshot was not retained")
			}
			// 次の正常な電文で再試行でき、途中までの登録も残らない。
			core.UpdateSnapshot(initializationSnapshot())
			if !core.IsInitialized() || len(core.GetTrackingState().TrainsByID) != 0 {
				t.Fatal("valid retry failed")
			}
		})
	}
}

func TestManualInitialCircuitWithoutOccupancyIsInitializationError(t *testing.T) {
	core := initializationCore()
	snapshot := initializationSnapshot()
	snapshot.TrainInitialization.Trains = []*ctcv1.TrainInitializationInfo{{ConsistId: "unity-1", ReferenceCircuitId: "A"}}
	err := core.UpdateSnapshot(snapshot)
	if !errors.Is(err, ctc.ErrTrainInitialization) || !strings.Contains(err.Error(), "not occupied") || !strings.Contains(err.Error(), "unity-1") {
		t.Fatalf("want identifiable initialization error, got %v", err)
	}
	if core.IsInitialized() || len(core.GetTrackingState().TrainsByID) != 0 {
		t.Fatal("train was registered on a clear circuit")
	}

	server := communication.CommunicationServer{CTC: core}
	message := &ctcv1.UnitySnapshotMessage{Header: &ctcv1.MessageHeader{}, Payload: snapshot}
	response, err := server.UpdateSnapshot(context.Background(), message)
	if response != nil || status.Code(err) != codes.FailedPrecondition || !strings.Contains(status.Convert(err).Message(), "not occupied") {
		t.Fatalf("initialization failure was not returned to Unity: response=%v, err=%v", response, err)
	}

	// 設定・配置を直した後は、明示的に再試行して登録できる。
	snapshot.TrackCircuits.OccupiedByCircuitId["A"] = true
	if _, err := server.UpdateSnapshot(context.Background(), message); err != nil {
		t.Fatal(err)
	}
	if !core.IsInitialized() {
		t.Fatal("valid retry did not initialize")
	}
}

func TestTrainInitializationWirePresenceAndEmptyWorld(t *testing.T) {
	for _, received := range []bool{false, true} {
		snapshot := initializationSnapshot()
		if !received {
			snapshot.TrainInitialization = nil
		}
		data, err := proto.Marshal(snapshot)
		if err != nil {
			t.Fatal(err)
		}
		decoded := new(ctcv1.UnitySnapshot)
		if err := proto.Unmarshal(data, decoded); err != nil {
			t.Fatal(err)
		}
		core := initializationCore()
		core.UpdateSnapshot(decoded)
		if core.IsInitialized() != received {
			t.Fatalf("received=%v, initialized=%v", received, core.IsInitialized())
		}
	}
}

func TestTrainInitializationAssignsDistinctInternalIDs(t *testing.T) {
	core := initializationCore()
	snapshot := initializationSnapshot()
	snapshot.TrackCircuits.OccupiedByCircuitId = map[string]bool{"A": true, "B": true}
	snapshot.TrainInitialization.Trains = []*ctcv1.TrainInitializationInfo{
		{ConsistId: "unity-1", TrainNumber: "101", ReferenceCircuitId: "A"},
		{ConsistId: "unity-2", TrainNumber: "103", ReferenceCircuitId: "B"},
	}
	core.UpdateSnapshot(snapshot)
	state := core.GetTrackingState()
	a, b := state.CircuitsByID["A"].TrainID, state.CircuitsByID["B"].TrainID
	if !state.IsInitialized || a == "" || b == "" || a == b || a == "unity-1" || b == "unity-2" {
		t.Fatalf("invalid internal IDs: %+v", state)
	}
	if !reflect.DeepEqual([]string{*state.TrainsByID[a].ConsistID, *state.TrainsByID[b].ConsistID}, []string{"unity-1", "unity-2"}) {
		t.Fatal("consist IDs were confused between trains")
	}
}

func initializationCore() *ctc.CTC {
	return ctc.New(map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock},
	}, nil)
}
