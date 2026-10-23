package tests

import (
	"context"
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

func TestTurnoutSnapshotThroughGRPCAndWebSocket(t *testing.T) {
	core := ctc.New(nil, nil)
	listener := bufconn.Listen(1024 * 1024)
	grpcServer := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(grpcServer, &communication.CommunicationServer{CTC: core})
	go grpcServer.Serve(listener)
	t.Cleanup(func() { grpcServer.Stop(); listener.Close() })
	connection, err := grpc.NewClient("passthrough:///turnout-test",
		grpc.WithContextDialer(func(ctx context.Context, _ string) (net.Conn, error) {
			return listener.DialContext(ctx)
		}),
		grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { connection.Close() })

	handler := &api.ApiServer{CTC: core}
	webServer := httptest.NewServer(http.HandlerFunc(handler.HandleWebSocket))
	t.Cleanup(webServer.Close)
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	web, _, err := websocket.Dial(ctx, "ws"+strings.TrimPrefix(webServer.URL, "http"), &websocket.DialOptions{
		HTTPHeader: http.Header{"Origin": []string{"http://localhost:3000"}},
	})
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { web.CloseNow() })
	client := ctcv1.NewCtcServiceClient(connection)

	for _, tt := range []struct {
		name     string
		turnouts *ctcv1.TurnoutSnapshot
	}{
		{"settled_positions", &ctcv1.TurnoutSnapshot{TurnoutsById: map[string]*ctcv1.TurnoutState{
			"NT01_21": {ActualPosition: ctcv1.TurnoutPosition_TURNOUT_POSITION_NORMAL},
			"NT01_25": {ActualPosition: ctcv1.TurnoutPosition_TURNOUT_POSITION_REVERSE},
		}}},
		{"moving_replaces_previous_snapshot", &ctcv1.TurnoutSnapshot{TurnoutsById: map[string]*ctcv1.TurnoutState{
			"NT01_21": {ActualPosition: ctcv1.TurnoutPosition_TURNOUT_POSITION_UNKNOWN, IsMoving: true},
		}}},
		{"settled_reverse", &ctcv1.TurnoutSnapshot{TurnoutsById: map[string]*ctcv1.TurnoutState{
			"NT01_21": {ActualPosition: ctcv1.TurnoutPosition_TURNOUT_POSITION_REVERSE},
		}}},
		{"unknown_position_keeps_id", &ctcv1.TurnoutSnapshot{TurnoutsById: map[string]*ctcv1.TurnoutState{
			"NT01_21": {},
		}}},
		{"future_enum_value", &ctcv1.TurnoutSnapshot{TurnoutsById: map[string]*ctcv1.TurnoutState{
			"NT01_21": {ActualPosition: ctcv1.TurnoutPosition(99)},
		}}},
		{"received_empty_clears_previous_positions", &ctcv1.TurnoutSnapshot{}},
		{"repopulate_before_missing", &ctcv1.TurnoutSnapshot{TurnoutsById: map[string]*ctcv1.TurnoutState{
			"NT01_21": {ActualPosition: ctcv1.TurnoutPosition_TURNOUT_POSITION_NORMAL},
		}}},
		{"missing_clears_previous_positions", nil},
	} {
		t.Run(tt.name, func(t *testing.T) {
			message := &ctcv1.UnitySnapshotMessage{
				Header: &ctcv1.MessageHeader{MessageType: "UnitySnapshot"},
				Payload: &ctcv1.UnitySnapshot{
					WorldTime:    &ctcv1.WorldTimeInformation{},
					Interlocking: &ctcv1.InterlockingSnapshot{},
					Turnouts:     tt.turnouts,
				},
			}
			if _, err := client.UpdateSnapshot(ctx, message); err != nil {
				t.Fatal(err)
			}
			if !proto.Equal(core.GetUnitySnapshot().GetTurnouts(), tt.turnouts) {
				t.Fatal("gRPC did not preserve the received turnout snapshot")
			}
			want := &ctcv1.CtcSnapshot{
				TurnoutsById: tt.turnouts.GetTurnoutsById(),
				Interlocking: message.Payload.Interlocking,
				WorldTime:    message.Payload.WorldTime,
			}
			if !proto.Equal(core.GetWebSnapshot(), want) {
				t.Fatalf("web state = %v, want %v", core.GetWebSnapshot(), want)
			}
			// 更新前のフレームが既に届いている場合は、今回の全体snapshotまで読む。
			for {
				kind, data, err := web.Read(ctx)
				if err != nil {
					t.Fatal(err)
				}
				if kind != websocket.MessageBinary {
					t.Fatalf("message type = %v, want binary", kind)
				}
				var got ctcv1.CtcSnapshot
				if err := proto.Unmarshal(data, &got); err != nil {
					t.Fatal(err)
				}
				if proto.Equal(&got, want) {
					break
				}
			}
		})
	}
}

func TestTurnoutSnapshotCopiesIncomingAndPublishedState(t *testing.T) {
	core := ctc.New(nil, nil)
	if len(core.GetWebSnapshot().GetTurnoutsById()) != 0 {
		t.Fatal("turnout positions were invented before receiving a snapshot")
	}
	original := &ctcv1.UnitySnapshot{Turnouts: &ctcv1.TurnoutSnapshot{
		TurnoutsById: map[string]*ctcv1.TurnoutState{
			"NT01_21": {ActualPosition: ctcv1.TurnoutPosition_TURNOUT_POSITION_NORMAL},
		},
	}}
	if err := core.UpdateSnapshot(original); err != nil {
		t.Fatal(err)
	}
	original.Turnouts.TurnoutsById["NT01_21"].ActualPosition = ctcv1.TurnoutPosition_TURNOUT_POSITION_REVERSE
	delete(original.Turnouts.TurnoutsById, "NT01_21")

	published := core.GetWebSnapshot()
	if state := published.GetTurnoutsById()["NT01_21"]; state.GetActualPosition() != ctcv1.TurnoutPosition_TURNOUT_POSITION_NORMAL {
		t.Fatalf("incoming mutation changed the stored position: %v", state)
	}
	published.TurnoutsById["NT01_21"].ActualPosition = ctcv1.TurnoutPosition_TURNOUT_POSITION_REVERSE
	published.TurnoutsById["NT01_21"].IsMoving = true
	delete(published.TurnoutsById, "NT01_21")
	returned := core.GetUnitySnapshot()
	returned.Turnouts.TurnoutsById["NT01_21"].ActualPosition = ctcv1.TurnoutPosition_TURNOUT_POSITION_REVERSE
	delete(returned.Turnouts.TurnoutsById, "NT01_21")
	want := &ctcv1.TurnoutState{ActualPosition: ctcv1.TurnoutPosition_TURNOUT_POSITION_NORMAL}
	if !proto.Equal(core.GetWebSnapshot().GetTurnoutsById()["NT01_21"], want) ||
		!proto.Equal(core.GetUnitySnapshot().GetTurnouts().GetTurnoutsById()["NT01_21"], want) {
		t.Fatal("returned turnout state shares a map or value with the retained snapshot")
	}
}
