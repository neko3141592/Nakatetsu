package tests

import (
	"context"
	"runtime"
	"sync/atomic"
	"testing"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
)

func TestAutomaticApproachStopAndPass(t *testing.T) {
	for _, handling := range []prc.PointHandling{prc.HandlingStop, prc.HandlingPass} {
		t.Run(string(handling), func(t *testing.T) {
			home := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 1}, TrainNumber: "1001", ControlStationID: "S", RouteID: "H"}
			departure := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 1, RouteSequenceIndex: 1}, TrainNumber: "1001", ControlStationID: "S", RouteID: "D"}
			plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{
				"1001": {TrainNumber: "1001", Points: []prc.PointPlan{{TimetablePointID: 1, StationID: station("S"), TrackCircuitID: "berth", Handling: handling, ArrivalSeconds: seconds(100), DepartureSeconds: seconds(100), ArrivalRoutes: []prc.RouteUsage{home}, DepartureRoutes: []prc.RouteUsage{departure}}}},
			}}
			source := &routeRequestCTC{mode: ctc.StationControlAuto,
				tracking: ctc.TrackingState{IsInitialized: true, TrainsByID: map[string]ctc.TrainState{"train-1": {TrainNumber: "1001"}}, CircuitsByID: map[string]ctc.TrackCircuitState{"approach": {IsOccupied: true, TrainID: "train-1"}}},
				snapshot: &ctcv1.UnitySnapshot{WorldTime: &ctcv1.WorldTimeInformation{WorldTimeSeconds: 100}, Interlocking: &ctcv1.InterlockingSnapshot{StationsById: map[string]*ctcv1.InterlockingStationStatus{
					"S": {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"H": {IsAvailable: true}, "D": {IsAvailable: true}}},
				}}},
			}
			core := prc.New(source, plan, map[string][]string{"S": {"approach"}}, []ctc.TrackRouteDefinition{
				{InterlockingStationID: "S", InterlockingRouteID: "H", TrackCircuitIDs: []string{"approach", "berth"}},
				{InterlockingStationID: "S", InterlockingRouteID: "D", TrackCircuitIDs: []string{"berth", "exit"}},
			})
			requests := make(chan string, 2)
			core.RouteControl = routeControlFunc(func(_ context.Context, request *ctcv1.InterlockingRouteRequest) (*ctcv1.InterlockingRouteRequestResult, error) {
				requests <- request.RouteId
				return &ctcv1.InterlockingRouteRequestResult{RequestId: request.RequestId, Accepted: true}, nil
			})
			if err := core.Initialize(source.tracking); err != nil {
				t.Fatal(err)
			}
			core.UpdateRouteRequests()
			assertPhase(t, core, home.ID, prc.RouteUsageRequested)
			wantCount := 1
			if handling == prc.HandlingPass {
				wantCount = 2
				assertPhase(t, core, departure.ID, prc.RouteUsageRequested)
			} else {
				assertPhase(t, core, departure.ID, prc.RouteUsagePending)
			}
			seen := make(map[string]bool)
			for i := 0; i < wantCount; i++ {
				select {
				case id := <-requests:
					seen[id] = true
				case <-time.After(time.Second):
					t.Fatal("automatic request did not arrive")
				}
			}
			if !seen["H"] || seen["D"] != (handling == prc.HandlingPass) {
				t.Fatalf("unexpected requests: %v", seen)
			}
		})
	}
}

func TestTemporaryRejectionWaitsBeforeRetry(t *testing.T) {
	core, source, usage, _ := progressFixture(t)
	var calls atomic.Int32
	requests := make(chan string, 2)
	core.RouteControl = routeControlFunc(func(_ context.Context, request *ctcv1.InterlockingRouteRequest) (*ctcv1.InterlockingRouteRequestResult, error) {
		first := calls.Add(1) == 1
		requests <- request.RequestId
		code := ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_CIRCUIT_OCCUPIED
		return &ctcv1.InterlockingRouteRequestResult{RequestId: request.RequestId, Accepted: !first, ErrorCode: &code}, nil
	})
	core.UpdateRouteRequests()
	firstID := <-requests
	deadline := time.Now().Add(time.Second)
	for core.GetTimetableState().RouteUsagesByID[usage.ID].Phase != prc.RouteUsagePending {
		core.UpdateRouteRequests()
		if time.Now().After(deadline) {
			t.Fatal("temporary rejection was not collected")
		}
		runtime.Gosched()
	}
	source.snapshot.WorldTime.WorldTimeSeconds = 100.5
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsagePending)
	source.snapshot.WorldTime.WorldTimeSeconds = 101
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageRequested)
	select {
	case secondID := <-requests:
		if secondID == firstID {
			t.Fatal("retry reused the previous request ID")
		}
	case <-time.After(time.Second):
		t.Fatal("retry did not arrive")
	}
}
