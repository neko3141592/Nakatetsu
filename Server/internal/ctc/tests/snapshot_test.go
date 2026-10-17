package tests

import (
	"context"
	"net"
	"os"
	"reflect"
	"testing"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/communication"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/test/bufconn"
	"google.golang.org/protobuf/proto"
)

func TestBuildTrackingInputPreservesOccupancyPresenceOnWire(t *testing.T) {
	for _, tt := range []struct {
		name     string
		circuits *ctcv1.TrackCircuitSnapshot
		want     map[string]bool
	}{
		{"missing", nil, nil},
		{"present_empty", &ctcv1.TrackCircuitSnapshot{}, map[string]bool{}},
		{"all_clear", &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: map[string]bool{"A": false, "B": false}}, map[string]bool{"A": false, "B": false}},
		{"occupied_and_clear", &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: map[string]bool{"A": true, "B": false}}, map[string]bool{"A": true, "B": false}},
	} {
		t.Run(tt.name, func(t *testing.T) {
			data, err := proto.Marshal(&ctcv1.UnitySnapshot{TrackCircuits: tt.circuits})
			if err != nil {
				t.Fatal(err)
			}
			snapshot := new(ctcv1.UnitySnapshot)
			if err := proto.Unmarshal(data, snapshot); err != nil {
				t.Fatal(err)
			}
			occupied, _ := ctc.BuildTrackingInput(snapshot)
			if !reflect.DeepEqual(occupied, tt.want) {
				t.Fatalf("occupied = %#v, want %#v", occupied, tt.want)
			}
			if occupied != nil {
				occupied["new"] = true
				if _, exists := snapshot.GetTrackCircuits().GetOccupiedByCircuitId()["new"]; exists {
					t.Fatal("input conversion mutated the received snapshot")
				}
			}
		})
	}
	if occupied, _ := ctc.BuildTrackingInput(nil); occupied != nil {
		t.Fatal("nil snapshot produced occupancy")
	}
}

func TestCTCWaitsForFirstValidSnapshot(t *testing.T) {
	for _, tt := range []struct {
		name string
		edit func(*ctcv1.UnitySnapshot)
	}{
		{"occupancy_not_received", func(s *ctcv1.UnitySnapshot) { s.TrackCircuits = nil }},
		{"empty_occupancy", func(s *ctcv1.UnitySnapshot) { s.TrackCircuits.OccupiedByCircuitId = nil }},
		{"missing_circuit", func(s *ctcv1.UnitySnapshot) { delete(s.TrackCircuits.OccupiedByCircuitId, "B") }},
		{"wrong_circuit_with_same_count", func(s *ctcv1.UnitySnapshot) {
			delete(s.TrackCircuits.OccupiedByCircuitId, "B")
			s.TrackCircuits.OccupiedByCircuitId["unknown"] = false
		}},
		{"extra_circuit", func(s *ctcv1.UnitySnapshot) { s.TrackCircuits.OccupiedByCircuitId["unknown"] = true }},
		{"missing_world_time", func(s *ctcv1.UnitySnapshot) { s.WorldTime = nil }},
		{"missing_interlocking", func(s *ctcv1.UnitySnapshot) { s.Interlocking = nil }},
	} {
		t.Run(tt.name, func(t *testing.T) {
			core := ctc.New(map[string]ctc.TrackCircuitDefinition{
				"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
				"B": {ControlKind: ctc.TrackControlBlock},
			}, nil)
			if core.IsInitialized() {
				t.Fatal("initialized before receiving any snapshot")
			}
			snapshot := initializationSnapshot()
			tt.edit(snapshot)
			core.UpdateSnapshot(snapshot)
			if core.IsInitialized() {
				t.Fatal("incomplete snapshot initialized tracking")
			}
			if !proto.Equal(core.GetUnitySnapshot(), snapshot) {
				t.Fatal("raw snapshot was not retained")
			}

			// 全回路falseも、有効な最初のsnapshotになる。
			core.UpdateSnapshot(initializationSnapshot())
			if !core.IsInitialized() {
				t.Fatal("complete all-clear snapshot did not initialize tracking")
			}
			// 以後に情報が欠けても、初回受信済みのフラグは戻さない。
			core.UpdateSnapshot(snapshot)
			if !core.IsInitialized() {
				t.Fatal("initialization flag was cleared by a later invalid snapshot")
			}
		})
	}
}

func TestCTCAcceptsFirstSnapshotWithOccupiedCircuit(t *testing.T) {
	core := ctc.New(map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock},
	}, nil)
	snapshot := initializationSnapshot()
	snapshot.TrackCircuits.OccupiedByCircuitId["A"] = true
	snapshot.TrainInitialization.Trains = []*ctcv1.TrainInitializationInfo{{ConsistId: "unity-1", ReferenceCircuitId: "A"}}
	core.UpdateSnapshot(snapshot)
	if !core.IsInitialized() {
		t.Fatal("complete snapshot with an occupied circuit did not initialize tracking")
	}
}

func initializationSnapshot() *ctcv1.UnitySnapshot {
	return &ctcv1.UnitySnapshot{
		WorldTime:           &ctcv1.WorldTimeInformation{TickCount: 1},
		Interlocking:        &ctcv1.InterlockingSnapshot{},
		TrackCircuits:       &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: map[string]bool{"A": false, "B": false}},
		TrainInitialization: &ctcv1.TrainInitializationSnapshot{},
	}
}

func TestBuildTrackingInputFiltersUnavailableStations(t *testing.T) {
	flags := &ctcv1.InterlockingRouteStatus{
		IsAvailable: true, IsRouteSet: true, PathEstablished: true,
		ProceedAllowed: false, RouteLocked: true, CancelPending: true,
	}
	snapshot := &ctcv1.UnitySnapshot{Interlocking: &ctcv1.InterlockingSnapshot{
		StationsById: map[string]*ctcv1.InterlockingStationStatus{
			"S1":            {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"R": flags, "unavailable": {}, "nil": nil}},
			"S2":            {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"R": {}}},
			"uninitialized": {HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"R": flags}},
			"no_output":     {IsInitialized: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"R": flags}},
			"nil":           nil,
		},
	}}
	_, got := ctc.BuildTrackingInput(snapshot)
	want := map[ctc.RouteKey]ctc.InterlockingRouteState{
		{StationID: "S1", RouteID: "R"}:           {IsAvailable: true, IsRouteSet: true, PathEstablished: true, RouteLocked: true, CancelPending: true},
		{StationID: "S1", RouteID: "unavailable"}: {},
		{StationID: "S2", RouteID: "R"}:           {},
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("route states = %#v, want %#v", got, want)
	}
}

func TestReceivedOccupancyDrivesBlockTracking(t *testing.T) {
	definitions := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock},
	}
	state := ctc.TrackingState{CircuitsByID: map[string]ctc.TrackCircuitState{"A": {TrainID: "101F"}}}
	for _, current := range []map[string]bool{{"A": true, "B": false}, {"A": false, "B": true}} {
		occupied, routes := ctc.BuildTrackingInput(&ctcv1.UnitySnapshot{
			TrackCircuits: &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: current},
		})
		ctc.UpdateTracking(&state, occupied, definitions, nil, nil, routes)
		for id, isOccupied := range occupied {
			circuit := state.CircuitsByID[id]
			circuit.IsOccupied = isOccupied
			state.CircuitsByID[id] = circuit
		}
	}
	if state.NeedsReset || state.CircuitsByID["B"].TrainID != "101F" || state.CircuitsByID["A"].TrainID != "" {
		t.Fatalf("tracking failed: %+v", state)
	}
}

func TestSnapshotOccupancyThroughGRPC(t *testing.T) {
	message := &ctcv1.UnitySnapshotMessage{
		Header: &ctcv1.MessageHeader{MessageType: "UnitySnapshot"},
		Payload: &ctcv1.UnitySnapshot{
			WorldTime:           &ctcv1.WorldTimeInformation{TickCount: 1},
			Interlocking:        &ctcv1.InterlockingSnapshot{},
			TrackCircuits:       &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: map[string]bool{"A": true, "B": false}},
			TrainInitialization: &ctcv1.TrainInitializationSnapshot{Trains: []*ctcv1.TrainInitializationInfo{{ConsistId: "unity-1", ReferenceCircuitId: "A"}}},
		},
	}
	if path := os.Getenv("NAKATETSU_SNAPSHOT_TEST_OUTPUT"); path != "" {
		data, err := os.ReadFile(path)
		if err != nil {
			t.Fatal(err)
		}
		if err := proto.Unmarshal(data, message); err != nil {
			t.Fatal(err)
		}
		t.Log("Using the snapshot serialized by Unity")
	}
	core := ctc.New(map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock},
	}, nil)
	listener := bufconn.Listen(1024 * 1024)
	server := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(server, &communication.CtcServer{CTC: core})
	go server.Serve(listener)
	t.Cleanup(func() { server.Stop(); listener.Close() })
	client, err := grpc.NewClient("passthrough:///snapshot-test",
		grpc.WithContextDialer(func(ctx context.Context, _ string) (net.Conn, error) { return listener.DialContext(ctx) }),
		grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { client.Close() })
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	if _, err := ctcv1.NewCtcServiceClient(client).UpdateSnapshot(ctx, message); err != nil {
		t.Fatal(err)
	}
	stored := core.GetUnitySnapshot()
	state := core.GetTrackingState()
	trainID := state.CircuitsByID["A"].TrainID
	if !state.IsInitialized || trainID == "" || len(state.TrainsByID) != 1 || state.TrainsByID[trainID].ConsistID == nil {
		t.Fatalf("gRPC did not initialize the train: %+v", state)
	}
	info := message.Payload.GetTrainInitialization().GetTrains()[0]
	registered := state.TrainsByID[trainID]
	if *registered.ConsistID != info.ConsistId || registered.TrainNumber != info.TrainNumber || registered.OperationNumber != info.OperationNumber || registered.ConsistNumber != info.ConsistNumber {
		t.Fatalf("gRPC lost initial metadata: %+v", registered)
	}
	if stored.GetWorldTime().GetTickCount() != 1 {
		t.Fatalf("wrong tick: %v", stored)
	}
	occupied, _ := ctc.BuildTrackingInput(stored)
	if !reflect.DeepEqual(occupied, map[string]bool{"A": true, "B": false}) {
		t.Fatalf("gRPC lost circuit states: %#v", occupied)
	}
	stored.TrackCircuits.OccupiedByCircuitId["A"] = false
	if !core.GetUnitySnapshot().GetTrackCircuits().GetOccupiedByCircuitId()["A"] {
		t.Fatal("returned snapshot shares the CTC's stored map")
	}
	// 未取得の旧形式のsnapshotも受信できる。
	message.Payload.TrackCircuits = nil
	if _, err := ctcv1.NewCtcServiceClient(client).UpdateSnapshot(ctx, message); err != nil {
		t.Fatal(err)
	}
	if core.GetUnitySnapshot().GetTrackCircuits() != nil {
		t.Fatal("missing occupancy was changed to a present message")
	}
}
