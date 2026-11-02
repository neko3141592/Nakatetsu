package tests

import (
	"context"
	"runtime"
	"testing"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
)

type routeRequestCTC struct {
	tracking ctc.TrackingState
	snapshot *ctcv1.UnitySnapshot
	mode     ctc.StationControlMode
}

func (c *routeRequestCTC) GetTrackingState() ctc.TrackingState    { return c.tracking }
func (c *routeRequestCTC) GetUnitySnapshot() *ctcv1.UnitySnapshot { return c.snapshot }
func (c *routeRequestCTC) GetStationControlMode(string) (ctc.StationControlMode, error) {
	// 挙上テストでは手動扱いにして、送信処理へ進めない。
	return c.mode, nil
}

type routeControlFunc func(context.Context, *ctcv1.InterlockingRouteRequest) (*ctcv1.InterlockingRouteRequestResult, error)

func (f routeControlFunc) SendRouteRequest(ctx context.Context, request *ctcv1.InterlockingRouteRequest) (*ctcv1.InterlockingRouteRequestResult, error) {
	return f(ctx, request)
}

func TestRejectedRouteRequestReturnsToCandidates(t *testing.T) {
	core, ctcCore, route := departureFixture(t, seconds(100), prc.HandlingStop)
	ctcCore.mode = ctc.StationControlAuto
	ctcCore.snapshot.Interlocking = &ctcv1.InterlockingSnapshot{StationsById: map[string]*ctcv1.InterlockingStationStatus{
		"A": {IsInitialized: true, HasOutput: true, RoutesById: map[string]*ctcv1.InterlockingRouteStatus{"D": {IsAvailable: true}}},
	}}
	core.RouteControl = routeControlFunc(func(_ context.Context, request *ctcv1.InterlockingRouteRequest) (*ctcv1.InterlockingRouteRequestResult, error) {
		code := ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_CIRCUIT_OCCUPIED
		return &ctcv1.InterlockingRouteRequestResult{RequestId: request.RequestId, Accepted: false, ErrorCode: &code}, nil
	})
	core.UpdateRouteRequests()
	if core.GetTimetableState().RouteUsagesByID[route.ID].Phase != prc.RouteUsageRequested {
		t.Fatal("route was not sent")
	}
	// 結果回収後に即再送しないよう、今回は時刻なしで回収だけ行う。
	ctcCore.snapshot = nil
	deadline := time.Now().Add(time.Second)
	for {
		core.UpdateRouteRequests()
		state := core.GetTimetableState()
		if state.RouteUsagesByID[route.ID].Phase == prc.RouteUsagePending {
			if state.RaisedRouteUsagesByID[route.ID] != route {
				t.Fatal("rejected route was not restored to candidates")
			}
			break
		}
		if time.Now().After(deadline) {
			t.Fatal("request result was not collected")
		}
		runtime.Gosched()
	}
}

func TestStoppedDepartureRouteTiming(t *testing.T) {
	for _, tc := range []struct {
		name       string
		now        float32
		departure  *int
		lead       float64
		handling   prc.PointHandling
		noTime     bool
		wantRaised bool
	}{
		{"before_threshold", 69.5, seconds(100), 30, prc.HandlingStop, false, false},
		{"at_threshold", 70, seconds(100), 30, prc.HandlingStop, false, true},
		{"after_threshold", 70.5, seconds(100), 30, prc.HandlingStop, false, true},
		{"late_arrival", 120, seconds(100), 30, prc.HandlingStop, false, true},
		{"untimed", 0, nil, 30, prc.HandlingStop, false, false},
		{"untimed_without_clock", 0, nil, 30, prc.HandlingStop, true, false},
		{"missing_clock", 100, seconds(100), 30, prc.HandlingStop, true, false},
		{"midnight_is_timed", 0, seconds(0), 30, prc.HandlingStop, false, true},
		{"after_24_hours", 90070, seconds(90100), 30, prc.HandlingStop, false, true},
		{"custom_lead", 40, seconds(100), 60, prc.HandlingStop, false, true},
		{"zero_lead_before", 99, seconds(100), 0, prc.HandlingStop, false, false},
		{"zero_lead_at_departure", 100, seconds(100), 0, prc.HandlingStop, false, true},
		{"passing_train", 120, seconds(100), 30, prc.HandlingPass, false, false},
		{"unspecified_handling", 120, nil, 30, prc.HandlingUnspecified, false, false},
	} {
		t.Run(tc.name, func(t *testing.T) {
			core, ctcCore, route := departureFixture(t, tc.departure, tc.handling)
			core.DepartureRequestLeadSeconds = tc.lead
			ctcCore.snapshot = &ctcv1.UnitySnapshot{WorldTime: &ctcv1.WorldTimeInformation{WorldTimeSeconds: tc.now}}
			if tc.noTime {
				ctcCore.snapshot = nil
			}
			core.UpdateRouteRequests()
			raised := core.GetTimetableState().RaisedRouteUsagesByID
			got, exists := raised[route.ID]
			if exists != tc.wantRaised || (exists && got != route) {
				t.Fatalf("raised=%v, wantRaised=%v", raised, tc.wantRaised)
			}
		})
	}
}

func TestDepartureRequiresCurrentTrainAtPoint(t *testing.T) {
	for _, tc := range []struct {
		name   string
		modify func(*ctc.TrackingState)
	}{
		{"uninitialized", func(s *ctc.TrackingState) { s.IsInitialized = false }},
		{"needs_reset", func(s *ctc.TrackingState) { s.NeedsReset = true }},
		{"missing_circuit", func(s *ctc.TrackingState) { delete(s.CircuitsByID, "A") }},
		{"vacant_circuit", func(s *ctc.TrackingState) { s.CircuitsByID["A"] = ctc.TrackCircuitState{TrainID: "train-1"} }},
		{"other_train", func(s *ctc.TrackingState) {
			s.CircuitsByID["A"] = ctc.TrackCircuitState{IsOccupied: true, TrainID: "other"}
		}},
		{"no_train_id", func(s *ctc.TrackingState) { s.CircuitsByID["A"] = ctc.TrackCircuitState{IsOccupied: true} }},
		{"no_train_number", func(s *ctc.TrackingState) { s.TrainsByID["train-1"] = ctc.TrainState{} }},
		{"unknown_train_number", func(s *ctc.TrackingState) { s.TrainsByID["train-1"] = ctc.TrainState{TrainNumber: "unknown"} }},
	} {
		t.Run(tc.name, func(t *testing.T) {
			core, ctcCore, _ := departureFixture(t, seconds(100), prc.HandlingStop)
			// 一度到達を記録し、CurrentPointIDが残っていても位置を確認する。
			core.UpdatePointProgress(ctcCore.tracking, 100)
			tc.modify(&ctcCore.tracking)
			core.UpdateRouteRequests()
			if got := core.GetTimetableState().RaisedRouteUsagesByID; len(got) != 0 {
				t.Fatalf("unexpected departure candidates: %v", got)
			}
		})
	}
}

func TestUpdateRaisesApproachAndDepartureRoutes(t *testing.T) {
	for _, handling := range []prc.PointHandling{prc.HandlingPass, prc.HandlingStop} {
		t.Run(string(handling), func(t *testing.T) {
			core, ctcCore, departure := departureFixture(t, seconds(100), prc.HandlingStop)
			// もう1列車はB駅へ接近中。停車中の列車とは別に場内を挙上する。
			home := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 200}, TrainNumber: "1002", ControlStationID: "B", RouteID: "H"}
			through := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 200, RouteSequenceIndex: 1}, TrainNumber: "1002", ControlStationID: "B", RouteID: "D"}
			plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{
				"1001": {TrainNumber: "1001", Points: []prc.PointPlan{{TimetablePointID: 100, TrackCircuitID: "A", Handling: prc.HandlingStop, DepartureSeconds: seconds(100), DepartureRoutes: []prc.RouteUsage{departure}}}},
				"1002": {TrainNumber: "1002", Points: []prc.PointPlan{{TimetablePointID: 200, StationID: station("B"), TrackCircuitID: "B", Handling: handling, ArrivalRoutes: []prc.RouteUsage{home}, DepartureRoutes: []prc.RouteUsage{through}}}},
			}}
			core = prc.New(ctcCore, plan, map[string][]string{"B": {"approach-B"}}, nil)
			ctcCore.tracking.TrainsByID["train-2"] = ctc.TrainState{TrainNumber: "1002"}
			ctcCore.tracking.CircuitsByID["approach-B"] = ctc.TrackCircuitState{IsOccupied: true, TrainID: "train-2"}
			if err := core.Initialize(ctcCore.tracking); err != nil {
				t.Fatal(err)
			}
			core.UpdateRouteRequests()
			core.UpdateRouteRequests()
			raised := core.GetTimetableState().RaisedRouteUsagesByID
			wantCount := 2
			if handling == prc.HandlingPass {
				wantCount = 3
			}
			if len(raised) != wantCount || raised[departure.ID] != departure || raised[home.ID] != home {
				t.Fatalf("unexpected combined candidates: %v", raised)
			}
			if _, exists := raised[through.ID]; exists != (handling == prc.HandlingPass) {
				t.Fatal("approaching stop train raised its departure")
			}
			// 候補取得側がmapを変更しても、内部の挙上状態は維持する。
			delete(raised, departure.ID)
			if core.GetTimetableState().RaisedRouteUsagesByID[departure.ID] != departure {
				t.Fatal("returned candidates share the internal map")
			}
			// 候補が残っても、送信済みの扱いにはしない。
			if core.GetTimetableState().RouteUsagesByID[departure.ID].Phase != prc.RouteUsagePending {
				t.Fatal("raising changed the send phase")
			}
			delete(ctcCore.tracking.CircuitsByID, "approach-B")
			core.UpdateRouteRequests()
			if core.GetTimetableState().RaisedRouteUsagesByID[home.ID] != home {
				t.Fatal("waiting approach candidate was lost")
			}
		})
	}
}

func departureFixture(t *testing.T, departure *int, handling prc.PointHandling) (*prc.PRC, *routeRequestCTC, prc.RouteUsage) {
	t.Helper()
	route := prc.RouteUsage{ID: prc.RouteUsageID{TimetablePointID: 100}, TrainNumber: "1001", ControlStationID: "A", RouteID: "D"}
	ctcCore := &routeRequestCTC{snapshot: &ctcv1.UnitySnapshot{WorldTime: &ctcv1.WorldTimeInformation{WorldTimeSeconds: 100}}, tracking: ctc.TrackingState{
		IsInitialized: true,
		TrainsByID:    map[string]ctc.TrainState{"train-1": {TrainNumber: "1001"}},
		CircuitsByID:  map[string]ctc.TrackCircuitState{"A": {IsOccupied: true, TrainID: "train-1"}},
	}}
	plan := prc.TimetablePlan{TrainsByNumber: map[string]prc.TrainPlan{
		"1001": {TrainNumber: "1001", Points: []prc.PointPlan{{
			TimetablePointID: 100, StationID: station("A"), TrackCircuitID: "A",
			Handling: handling, DepartureSeconds: departure, DepartureRoutes: []prc.RouteUsage{route},
		}}},
	}}
	core := prc.New(ctcCore, plan, nil, []ctc.TrackRouteDefinition{{
		InterlockingStationID: "A", InterlockingRouteID: "D", TrackCircuitIDs: []string{"A", "exit"},
	}})
	if core.DepartureRequestLeadSeconds != 30 {
		t.Fatal("default departure lead is not 30 seconds")
	}
	if err := core.Initialize(ctcCore.tracking); err != nil {
		t.Fatal(err)
	}
	return core, ctcCore, route
}

func seconds(value int) *int       { return &value }
func station(value string) *string { return &value }
