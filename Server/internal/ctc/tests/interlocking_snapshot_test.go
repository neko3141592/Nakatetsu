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

func TestInterlockingSnapshotThroughGRPCAndWebSocket(t *testing.T) {
	core := ctc.New(nil, nil)
	listener := bufconn.Listen(1024 * 1024)
	grpcServer := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(grpcServer, &communication.CtcServer{CTC: core})
	go grpcServer.Serve(listener)
	t.Cleanup(func() { grpcServer.Stop(); listener.Close() })
	connection, err := grpc.NewClient("passthrough:///interlocking-test",
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

	// 追跡初期化前でも公開し、未受信と受信済みの空メッセージは区別する。
	if core.GetWebSnapshot().GetInterlocking() != nil {
		t.Fatal("interlocking state was invented before receiving a snapshot")
	}
	readInterlockingWebSnapshot(t, ctx, web, nil)
	for _, tt := range []struct {
		name         string
		interlocking *ctcv1.InterlockingSnapshot
	}{
		{"all_route_fields_and_station_flags", interlockingPublicationFixture()},
		{"replacement_removes_old_stations_and_routes", &ctcv1.InterlockingSnapshot{
			StationsById: map[string]*ctcv1.InterlockingStationStatus{
				"NT01": {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"1R": {}}},
			},
		}},
		{"station_without_routes", &ctcv1.InterlockingSnapshot{
			StationsById: map[string]*ctcv1.InterlockingStationStatus{
				"NT01": {IsInitialized: true, HasOutput: true},
			},
		}},
		{"explicit_empty_clears_previous_stations", &ctcv1.InterlockingSnapshot{}},
		{"repopulate_before_missing", interlockingPublicationFixture()},
	} {
		t.Run(tt.name, func(t *testing.T) {
			message := &ctcv1.UnitySnapshotMessage{
				Header: &ctcv1.MessageHeader{MessageType: "UnitySnapshot"},
				Payload: &ctcv1.UnitySnapshot{
					WorldTime:    &ctcv1.WorldTimeInformation{},
					Interlocking: tt.interlocking,
				},
			}
			if _, err := client.UpdateSnapshot(ctx, message); err != nil {
				t.Fatal(err)
			}
			if !proto.Equal(core.GetUnitySnapshot().GetInterlocking(), tt.interlocking) {
				t.Fatal("gRPC did not preserve the received interlocking snapshot")
			}
			if core.IsInitialized() {
				t.Fatal("fixture unexpectedly initialized train tracking")
			}
			want := &ctcv1.CtcSnapshot{Interlocking: tt.interlocking, WorldTime: message.Payload.WorldTime}
			if !proto.Equal(core.GetWebSnapshot(), want) {
				t.Fatalf("web state = %v, want %v", core.GetWebSnapshot(), want)
			}
			readInterlockingWebSnapshot(t, ctx, web, tt.interlocking)
		})
	}

	// gRPCでは必須項目なので、欠落の扱いはCTCの保持・公開境界で確認する。
	if err := core.UpdateSnapshot(&ctcv1.UnitySnapshot{}); err != nil {
		t.Fatal(err)
	}
	if core.GetWebSnapshot().GetInterlocking() != nil {
		t.Fatal("missing interlocking retained a previous snapshot")
	}
	readInterlockingWebSnapshot(t, ctx, web, nil)
}

func TestInterlockingSnapshotCopiesIncomingAndPublishedState(t *testing.T) {
	core := ctc.New(nil, nil)
	original := &ctcv1.UnitySnapshot{Interlocking: interlockingPublicationFixture()}
	want := proto.Clone(original.Interlocking).(*ctcv1.InterlockingSnapshot)
	if err := core.UpdateSnapshot(original); err != nil {
		t.Fatal(err)
	}
	mutateInterlockingPublication(original.Interlocking)
	if !proto.Equal(core.GetWebSnapshot().GetInterlocking(), want) {
		t.Fatal("incoming mutation changed the retained interlocking snapshot")
	}

	published := core.GetWebSnapshot()
	mutateInterlockingPublication(published.Interlocking)
	returned := core.GetUnitySnapshot()
	mutateInterlockingPublication(returned.Interlocking)
	if !proto.Equal(core.GetWebSnapshot().GetInterlocking(), want) ||
		!proto.Equal(core.GetUnitySnapshot().GetInterlocking(), want) {
		t.Fatal("returned interlocking state shares a nested map or value with the retained snapshot")
	}
}

func readInterlockingWebSnapshot(t *testing.T, ctx context.Context, web *websocket.Conn, want *ctcv1.InterlockingSnapshot) {
	t.Helper()
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
		if proto.Equal(got.GetInterlocking(), want) {
			return
		}
	}
}

func interlockingPublicationFixture() *ctcv1.InterlockingSnapshot {
	allFields := &ctcv1.InterlockingRouteStatus{
		IsAvailable:                     true,
		IsRouteSet:                      true,
		PathEstablished:                 true,
		ProceedAllowed:                  true,
		RouteLocked:                     true,
		CancelPending:                   true,
		ApproachLocked:                  true,
		ApproachReleaseRemainingSeconds: 31.5,
		OverrunMode:                     ctcv1.OverrunProtectionMode_OVERRUN_PROTECTION_MODE_RESTRICTED,
		OverrunPhase:                    ctcv1.OverrunProtectionPhase_OVERRUN_PROTECTION_PHASE_RELEASE_TIMING,
		OverrunReleaseRemainingSeconds:  12.75,
	}
	return &ctcv1.InterlockingSnapshot{StationsById: map[string]*ctcv1.InterlockingStationStatus{
		"NT01": {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{
			"1R": allFields,
			"2R": {},
		}},
		"NT02": {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{
			"1R": {IsAvailable: true, IsRouteSet: true, RouteLocked: true},
		}},
		"uninitialized": {HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{
			"1R": proto.Clone(allFields).(*ctcv1.InterlockingRouteStatus),
		}},
		"no_output": {IsInitialized: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{
			"1R": proto.Clone(allFields).(*ctcv1.InterlockingRouteStatus),
		}},
		"no_flags": {RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"1R": {}}},
	}}
}

func mutateInterlockingPublication(snapshot *ctcv1.InterlockingSnapshot) {
	station := snapshot.StationsById["NT01"]
	station.IsInitialized = false
	station.HasOutput = false
	route := station.RoutesById["1R"]
	route.IsRouteSet = false
	route.ApproachReleaseRemainingSeconds = 0
	route.OverrunPhase = ctcv1.OverrunProtectionPhase_OVERRUN_PROTECTION_PHASE_NONE
	route.OverrunReleaseRemainingSeconds = 0
	delete(station.RoutesById, "2R")
	station.RoutesById["new"] = &ctcv1.InterlockingRouteStatus{IsAvailable: true}
	delete(snapshot.StationsById, "NT02")
	snapshot.StationsById["new"] = &ctcv1.InterlockingStationStatus{IsInitialized: true}
}
