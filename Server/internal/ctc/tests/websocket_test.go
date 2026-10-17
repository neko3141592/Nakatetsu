package tests

import (
	"context"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
	"time"

	"github.com/coder/websocket"
	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/api"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"google.golang.org/protobuf/proto"
)

func TestWebSocketDebugClientReceivesSnapshot(t *testing.T) {
	core := ctc.New(map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlBlock},
	}, nil)
	err := core.UpdateSnapshot(&ctcv1.UnitySnapshot{
		WorldTime:     &ctcv1.WorldTimeInformation{TickCount: 1},
		Interlocking:  &ctcv1.InterlockingSnapshot{},
		TrackCircuits: &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: map[string]bool{"A": true, "B": false}},
		TrainInitialization: &ctcv1.TrainInitializationSnapshot{Trains: []*ctcv1.TrainInitializationInfo{{
			ConsistId: "unity-101", ConsistNumber: "101F", TrainNumber: "101", OperationNumber: "01", ReferenceCircuitId: "A",
		}}},
	})
	if err != nil {
		t.Fatal(err)
	}
	handler := &api.Server{CTC: core}
	server := httptest.NewServer(http.HandlerFunc(handler.HandleWebSocket))
	defer server.Close()

	for _, origin := range []string{"http://localhost:3000", "http://127.0.0.1:3000"} {
		t.Run(origin, func(t *testing.T) {
			ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
			defer cancel()
			conn, _, err := websocket.Dial(ctx, "ws"+strings.TrimPrefix(server.URL, "http"), &websocket.DialOptions{
				HTTPHeader: http.Header{"Origin": []string{origin}},
			})
			if err != nil {
				t.Fatal(err)
			}
			defer conn.CloseNow()
			for i := 0; i < 2; i++ {
				kind, data, err := conn.Read(ctx)
				if err != nil {
					t.Fatal(err)
				}
				var snapshot ctcv1.CtcSnapshot
				if kind != websocket.MessageBinary {
					t.Fatalf("message type = %v, want binary", kind)
				}
				if err := proto.Unmarshal(data, &snapshot); err != nil {
					t.Fatal(err)
				}
				if !proto.Equal(&snapshot, core.GetWebSnapshot()) {
					t.Fatalf("received snapshot differs from CTC: %v", &snapshot)
				}
			}
		})
	}
}

func TestWebSocketRejectsUnapprovedOrigin(t *testing.T) {
	handler := &api.Server{CTC: ctc.New(nil, nil)}
	server := httptest.NewServer(http.HandlerFunc(handler.HandleWebSocket))
	defer server.Close()
	ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
	defer cancel()
	conn, response, err := websocket.Dial(ctx, "ws"+strings.TrimPrefix(server.URL, "http"), &websocket.DialOptions{
		HTTPHeader: http.Header{"Origin": []string{"http://example.com:3000"}},
	})
	if conn != nil {
		defer conn.CloseNow()
	}
	if err == nil || response == nil || response.StatusCode != http.StatusForbidden {
		t.Fatalf("unapproved origin: err=%v, response=%v", err, response)
	}
}
