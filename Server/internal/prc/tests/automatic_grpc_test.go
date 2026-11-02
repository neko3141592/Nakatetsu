package tests

import (
	"context"
	"net"
	"testing"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/communication"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/test/bufconn"
)

func TestAutomaticRoutesThroughGRPC(t *testing.T) {
	definitions := map[string]ctc.TrackCircuitDefinition{
		"A": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"E": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "B"},
		"B": {ControlKind: ctc.TrackControlInterlocking},
		"C": {ControlKind: ctc.TrackControlBlock, ToCircuitID: "D"},
		"D": {ControlKind: ctc.TrackControlBlock},
		"F": {ControlKind: ctc.TrackControlBlock},
	}
	routes := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S", InterlockingRouteID: "R1", TrackCircuitIDs: []string{"A", "B", "C"}, RouteClearTrackCircuitIDs: []string{"B", "C"}},
		{InterlockingStationID: "S", InterlockingRouteID: "R2", TrackCircuitIDs: []string{"E", "B", "F"}, RouteClearTrackCircuitIDs: []string{"B", "F"}},
	}
	first := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 101}, TrainNumber: "1001", ControlStationID: "S", RouteID: "R1"}
	second := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 201}, TrainNumber: "1002", ControlStationID: "S", RouteID: "R2"}
	plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{
		"1001": {TrainNumber: "1001", Points: []prc.PointPlan{
			{TimetablePointID: 101, TrackCircuitID: "A", Handling: prc.HandlingStop, DepartureSeconds: seconds(100), DepartureRoutes: []prc.RouteUsage{first}},
			{TimetablePointID: 102, TrackCircuitID: "D", Handling: prc.HandlingPass},
		}},
		"1002": {TrainNumber: "1002", Points: []prc.PointPlan{{TimetablePointID: 201, TrackCircuitID: "E", Handling: prc.HandlingStop, DepartureSeconds: seconds(110), DepartureRoutes: []prc.RouteUsage{second}}}},
	}}
	var err error
	plan.ConflictUsagesByCircuitID, err = prc.BuildConflictUsages(plan, prc.BuildConflictPoints(routes))
	if err != nil {
		t.Fatal(err)
	}
	ctcCore := ctc.New(definitions, routes)
	transport := &communication.CommunicationServer{CTC: ctcCore}
	core := prc.New(ctcCore, plan, nil, routes)
	core.RouteControl = transport
	ctcCore.SetPRC(core)
	if err := ctcCore.SetStationControlMode("S", ctc.StationControlAuto); err != nil {
		t.Fatal(err)
	}
	listener := bufconn.Listen(1024 * 1024)
	server := grpc.NewServer()
	ctcv1.RegisterCtcServiceServer(server, transport)
	go server.Serve(listener)
	t.Cleanup(func() { server.Stop(); listener.Close() })
	connection, err := grpc.NewClient("passthrough:///prc-test", grpc.WithTransportCredentials(insecure.NewCredentials()),
		grpc.WithContextDialer(func(ctx context.Context, _ string) (net.Conn, error) { return listener.DialContext(ctx) }))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { connection.Close() })
	ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
	defer cancel()
	client := ctcv1.NewCtcServiceClient(connection)
	stream, err := client.ConnectRouteControl(ctx)
	if err != nil {
		t.Fatal(err)
	}
	requests := make(chan *ctcv1.InterlockingRouteRequest, 8)
	go func() {
		for {
			request, err := stream.Recv()
			if err != nil {
				return
			}
			if request.RouteId != "probe" {
				requests <- request
			}
			if err := stream.Send(&ctcv1.InterlockingRouteRequestResult{RequestId: request.RequestId, StationId: request.StationId, RouteId: request.RouteId, Accepted: true}); err != nil {
				return
			}
		}
	}()
	// 双方向ストリームが登録されてから、最初の自動要求を発生させる。
	for {
		_, err := transport.SendRouteRequest(ctx, &ctcv1.InterlockingRouteRequest{RequestId: "probe", RouteId: "probe"})
		if err == nil {
			break
		}
		select {
		case <-ctx.Done():
			t.Fatal("route stream did not become ready")
		case <-time.After(time.Millisecond):
		}
	}
	statuses := map[string]*ctcv1.InterlockingRouteStatus{"R1": {IsAvailable: true}, "R2": {IsAvailable: true}}
	snapshot := &ctcv1.UnitySnapshot{
		WorldTime: &ctcv1.WorldTimeInformation{WorldTimeSeconds: 100},
		Interlocking: &ctcv1.InterlockingSnapshot{StationsById: map[string]*ctcv1.InterlockingStationStatus{
			"S": {IsInitialized: true, HasOutput: true, RoutesById: statuses},
		}},
		TrainInitialization: &ctcv1.TrainInitializationSnapshot{Trains: []*ctcv1.TrainInitializationInfo{
			{ConsistId: "unit-1", TrainNumber: "1001", ReferenceCircuitId: "A"},
			{ConsistId: "unit-2", TrainNumber: "1002", ReferenceCircuitId: "E"},
		}},
	}
	update := func(position string) {
		t.Helper()
		occupied := make(map[string]bool, len(definitions))
		for id := range definitions {
			occupied[id] = id == position || id == "E"
		}
		snapshot.TrackCircuits = &ctcv1.TrackCircuitSnapshot{OccupiedByCircuitId: occupied}
		snapshot.WorldTime.TickCount++
		_, err := client.UpdateSnapshot(ctx, &ctcv1.UnitySnapshotMessage{Header: &ctcv1.MessageHeader{}, Payload: snapshot})
		if err != nil {
			t.Fatal(err)
		}
	}
	readRequest := func(want string) {
		t.Helper()
		select {
		case request := <-requests:
			if request.RouteId != want || request.RequestId == "" || request.Operation != ctcv1.InterlockingRouteOperation_INTERLOCKING_ROUTE_OPERATION_SET {
				t.Fatalf("unexpected request: %v", request)
			}
		case <-ctx.Done():
			t.Fatalf("request %s did not arrive", want)
		}
	}
	update("A")
	readRequest("R1")
	assertPhase(t, core, second.ID, prc.RouteUsagePending)
	statuses["R1"].IsRouteSet, statuses["R1"].RouteLocked = true, true
	update("A")
	assertPhase(t, core, first.ID, prc.RouteUsageRequested)
	statuses["R1"].PathEstablished = true
	update("A")
	assertPhase(t, core, first.ID, prc.RouteUsageReserved)
	update("B")
	assertPhase(t, core, first.ID, prc.RouteUsageInUse)
	update("C")
	assertPhase(t, core, first.ID, prc.RouteUsageInUse)
	*statuses["R1"] = ctcv1.InterlockingRouteStatus{IsAvailable: true}
	update("D")
	assertPhase(t, core, first.ID, prc.RouteUsageCompleted)
	readRequest("R2")
	assertPhase(t, core, second.ID, prc.RouteUsageRequested)
	if ctcCore.GetTrackingState().NeedsReset {
		t.Fatal("automatic control broke CTC tracking")
	}
}
