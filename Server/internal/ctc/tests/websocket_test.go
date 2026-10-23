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
		"A": {DisplayName: "下り本線", ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
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
	handler := &api.ApiServer{CTC: core}

	for _, tc := range []struct {
		host   string
		origin string
	}{
		{"127.0.0.1:8080", "http://localhost:3000"},
		{"127.0.0.1:8080", "http://127.0.0.1:3000"},
		{"192.168.1.10:8080", "http://192.168.1.10:3000"},
		{"nakatetsu.local:8080", "http://nakatetsu.local:3000"},
	} {
		t.Run(tc.origin, func(t *testing.T) {
			server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
				r.Host = tc.host
				handler.HandleWebSocket(w, r)
			}))
			defer server.Close()
			ctx, cancel := context.WithTimeout(context.Background(), 3*time.Second)
			defer cancel()
			conn, _, err := websocket.Dial(ctx, "ws"+strings.TrimPrefix(server.URL, "http"), &websocket.DialOptions{
				HTTPHeader: http.Header{"Origin": []string{tc.origin}},
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
				if circuit := snapshot.CircuitsById["A"]; circuit.GetDisplayName() != "下り本線" || !circuit.GetIsOccupied() || circuit.GetTrainId() == "" {
					t.Fatalf("occupied circuit lost its name or tracking state: %v", circuit)
				}
				if circuit := snapshot.CircuitsById["B"]; circuit.GetDisplayName() != "B" || circuit.GetIsOccupied() {
					t.Fatalf("unnamed circuit should fall back to its ID: %v", circuit)
				}
				if !proto.Equal(&snapshot, core.GetWebSnapshot()) {
					t.Fatalf("received snapshot differs from CTC: %v", &snapshot)
				}
			}
		})
	}
}

func TestWebSocketRejectsUnapprovedOrigin(t *testing.T) {
	handler := &api.ApiServer{CTC: ctc.New(nil, nil)}
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
