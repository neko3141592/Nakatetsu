package tests

import (
	"context"
	"errors"
	"net"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/coder/websocket"
	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/api"
	"github.com/neko3141592/Nakatetsu/Server/internal/communication"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/test/bufconn"
	"google.golang.org/protobuf/proto"
)

func TestWorldTimeThroughGRPCAndWebSocket(t *testing.T) {
	core := ctc.New(nil, nil)
	listener := bufconn.Listen(1024 * 1024)
	grpcServer := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(grpcServer, &communication.CtcServer{CTC: core})
	go grpcServer.Serve(listener)
	t.Cleanup(func() { grpcServer.Stop(); listener.Close() })
	connection, err := grpc.NewClient("passthrough:///world-time-test",
		grpc.WithContextDialer(func(ctx context.Context, _ string) (net.Conn, error) {
			return listener.DialContext(ctx)
		}),
		grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { connection.Close() })
	client := ctcv1.NewCtcServiceClient(connection)

	handler := &api.Server{CTC: core}
	server := httptest.NewServer(http.HandlerFunc(handler.HandleWebSocket))
	t.Cleanup(server.Close)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	web, _, err := websocket.Dial(ctx, "ws"+strings.TrimPrefix(server.URL, "http"), &websocket.DialOptions{
		HTTPHeader: http.Header{"Origin": []string{"http://localhost:3000"}},
	})
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { web.CloseNow() })

	if core.GetWebSnapshot().GetWorldTime() != nil {
		t.Fatal("world time was invented before receiving a snapshot")
	}
	readWorldTimeWebSnapshot(t, ctx, web, nil)
	for _, tt := range []struct {
		name string
		time *ctcv1.WorldTimeInformation
	}{
		{"received_zero_is_present", &ctcv1.WorldTimeInformation{}},
		{"running", &ctcv1.WorldTimeInformation{
			TickCount: 1234, WorldTimeSeconds: 63725.5, TickDurationSeconds: 0.25, PlaybackSpeed: 3,
		}},
		{"paused_with_fractional_speed", &ctcv1.WorldTimeInformation{
			TickCount: 1235, WorldTimeSeconds: 63725.75, TickDurationSeconds: 0.25, IsPaused: true, PlaybackSpeed: 0.5,
		}},
		{"replacement_clears_pause_speed_and_tick_duration", &ctcv1.WorldTimeInformation{WorldTimeSeconds: 5}},
	} {
		t.Run(tt.name, func(t *testing.T) {
			message := &ctcv1.UnitySnapshotMessage{
				Header: &ctcv1.MessageHeader{MessageType: "UnitySnapshot"},
				Payload: &ctcv1.UnitySnapshot{
					WorldTime: tt.time, Interlocking: &ctcv1.InterlockingSnapshot{},
				},
			}
			if _, err := client.UpdateSnapshot(ctx, message); err != nil {
				t.Fatal(err)
			}
			if core.IsInitialized() {
				t.Fatal("world time publication unexpectedly initialized train tracking")
			}
			if got := core.GetWebSnapshot().GetWorldTime(); !proto.Equal(got, tt.time) {
				t.Fatalf("world time = %v, want %v", got, tt.time)
			}
			readWorldTimeWebSnapshot(t, ctx, web, tt.time)
		})
	}

	// gRPC requires world_time; the storage boundary must still clear missing data.
	if err := core.UpdateSnapshot(&ctcv1.UnitySnapshot{}); err != nil {
		t.Fatal(err)
	}
	if core.GetWebSnapshot().GetWorldTime() != nil {
		t.Fatal("missing world time retained a previous value")
	}
	readWorldTimeWebSnapshot(t, ctx, web, nil)
}

func TestWorldTimePublicationIndependentOfTrainInitialization(t *testing.T) {
	for _, failInitialization := range []bool{false, true} {
		name := "initialized"
		if failInitialization {
			name = "initialization_failed"
		}
		t.Run(name, func(t *testing.T) {
			core := ctc.New(map[string]ctc.TrackCircuitDefinition{
				"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
				"B": {ControlKind: ctc.TrackControlBlock},
			}, nil)
			snapshot := initializationSnapshot()
			snapshot.WorldTime = &ctcv1.WorldTimeInformation{
				TickCount: 10, WorldTimeSeconds: 36001, PlaybackSpeed: 2,
			}
			if failInitialization {
				snapshot.TrainInitialization.Trains = []*ctcv1.TrainInitializationInfo{{}}
			}
			err := core.UpdateSnapshot(snapshot)
			if failInitialization {
				if !errors.Is(err, ctc.ErrTrainInitialization) {
					t.Fatalf("error = %v, want train initialization error", err)
				}
			} else if err != nil {
				t.Fatal(err)
			}
			if core.IsInitialized() == failInitialization {
				t.Fatal("unexpected train initialization state")
			}
			if got := core.GetWebSnapshot().GetWorldTime(); !proto.Equal(got, snapshot.WorldTime) {
				t.Fatalf("world time depends on train initialization: got %v, want %v", got, snapshot.WorldTime)
			}
		})
	}
}

func TestWorldTimeCopiesIncomingAndPublishedState(t *testing.T) {
	core := ctc.New(nil, nil)
	original := &ctcv1.UnitySnapshot{WorldTime: &ctcv1.WorldTimeInformation{
		TickCount: 45, WorldTimeSeconds: 86401.5, TickDurationSeconds: 0.5, IsPaused: true, PlaybackSpeed: 3,
	}}
	want := proto.Clone(original.WorldTime).(*ctcv1.WorldTimeInformation)
	if err := core.UpdateSnapshot(original); err != nil {
		t.Fatal(err)
	}
	proto.Reset(original.WorldTime)
	if got := core.GetWebSnapshot().GetWorldTime(); !proto.Equal(got, want) {
		t.Fatalf("incoming mutation changed stored world time: %v", got)
	}
	proto.Reset(core.GetWebSnapshot().WorldTime)
	proto.Reset(core.GetUnitySnapshot().WorldTime)
	if !proto.Equal(core.GetWebSnapshot().GetWorldTime(), want) ||
		!proto.Equal(core.GetUnitySnapshot().GetWorldTime(), want) {
		t.Fatal("returned world time shares state with the retained snapshot")
	}
}

func readWorldTimeWebSnapshot(t *testing.T, ctx context.Context, web *websocket.Conn, want *ctcv1.WorldTimeInformation) {
	t.Helper()
	for {
		kind, data, err := web.Read(ctx)
		if err != nil {
			t.Fatal(err)
		}
		if kind != websocket.MessageBinary {
			t.Fatalf("message type = %v, want binary", kind)
		}
		var snapshot ctcv1.CtcSnapshot
		if err := proto.Unmarshal(data, &snapshot); err != nil {
			t.Fatal(err)
		}
		if proto.Equal(snapshot.GetWorldTime(), want) {
			return
		}
	}
}
