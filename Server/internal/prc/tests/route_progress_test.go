package tests

import (
	"context"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
)

func TestRouteProgressThroughTurningAndPassage(t *testing.T) {
	core, source, usage, status := progressFixture(t)
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageRequested)
	status.IsRouteSet, status.RouteLocked = true, true
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageRequested) // 転換中は確保済みとしない。
	status.PathEstablished = true
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageReserved)
	source.tracking.CircuitsByID["mid"] = ctc.TrackCircuitState{IsOccupied: true, TrainID: "other"}
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageReserved) // 他列車の在線を取り込まない。
	source.tracking.CircuitsByID = map[string]ctc.TrackCircuitState{"mid": {IsOccupied: true, TrainID: "train-1"}}
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageInUse)
	status.IsRouteSet, status.RouteLocked, status.PathEstablished = false, false, false
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageInUse) // 解錠だけでは通過完了としない。
	source.tracking.CircuitsByID = map[string]ctc.TrackCircuitState{"end": {IsOccupied: true, TrainID: "train-1"}}
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageCompleted)
}

func TestManualCancellationDoesNotRaiseAgain(t *testing.T) {
	core, _, usage, status := progressFixture(t)
	core.UpdateRouteRequests()
	status.IsRouteSet, status.RouteLocked, status.PathEstablished = true, true, true
	core.UpdateRouteRequests()
	status.CancelPending = true
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageCancelled)
	*status = ctcv1.InterlockingRouteStatus{IsAvailable: true}
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageCancelled)
	if len(core.GetTimetableState().RaisedRouteUsagesByID) != 0 {
		t.Fatal("manually cancelled usage was raised again")
	}
}

func TestMissingRouteOutputDoesNotCompleteUsage(t *testing.T) {
	core, source, usage, status := progressFixture(t)
	core.UpdateRouteRequests()
	status.IsRouteSet, status.RouteLocked, status.PathEstablished = true, true, true
	core.UpdateRouteRequests()
	source.tracking.CircuitsByID = map[string]ctc.TrackCircuitState{"end": {IsOccupied: true, TrainID: "train-1"}}
	core.UpdateRouteRequests()
	source.snapshot.Interlocking = nil
	core.UpdateRouteRequests()
	assertPhase(t, core, usage.ID, prc.RouteUsageInUse)
}

func progressFixture(t *testing.T) (*prc.PRC, *routeRequestCTC, prc.RouteUsage, *ctcv1.InterlockingRouteStatus) {
	t.Helper()
	_, source, usage := departureFixture(t, seconds(100), prc.HandlingStop)
	source.mode = ctc.StationControlAuto
	status := &ctcv1.InterlockingRouteStatus{IsAvailable: true}
	source.snapshot.Interlocking = &ctcv1.InterlockingSnapshot{StationsById: map[string]*ctcv1.InterlockingStationStatus{
		"A": {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"D": status}},
	}}
	plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{
		"1001": {TrainNumber: "1001", Points: []prc.PointPlan{{TimetablePointID: 100, TrackCircuitID: "A", Handling: prc.HandlingStop, DepartureSeconds: seconds(100), DepartureRoutes: []prc.RouteUsage{usage}}}},
	}}
	core := prc.New(source, plan, nil, []ctc.TrackRouteDefinition{{
		InterlockingStationID: "A", InterlockingRouteID: "D", TrackCircuitIDs: []string{"A", "mid", "end"}, RouteClearTrackCircuitIDs: []string{"mid", "end"},
	}})
	core.RouteControl = routeControlFunc(func(_ context.Context, request *ctcv1.InterlockingRouteRequest) (*ctcv1.InterlockingRouteRequestResult, error) {
		return &ctcv1.InterlockingRouteRequestResult{RequestId: request.RequestId, Accepted: true}, nil
	})
	if err := core.Initialize(source.tracking); err != nil {
		t.Fatal(err)
	}
	return core, source, usage, status
}

func assertPhase(t *testing.T, core *prc.PRC, id prc.RouteUsageID, want prc.RouteUsagePhase) {
	t.Helper()
	if got := core.GetTimetableState().RouteUsagesByID[id].Phase; got != want {
		t.Fatalf("usage %v: phase=%v, want %v", id, got, want)
	}
}
