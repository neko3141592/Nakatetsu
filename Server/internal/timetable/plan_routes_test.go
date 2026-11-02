package timetable_test

import (
	"context"
	"fmt"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
)

func TestLoadPlanGroupsRoutesByStationVisit(t *testing.T) {
	db, definitions := routeGroupingFixture(t)
	plan, err := timetable.LoadTimetablePlan(context.Background(), db, "grouping", definitions)
	if err != nil {
		t.Fatal(err)
	}
	points := plan.TrainsByNumber["1001"].Points
	usage := func(source, sequence int, stationID, routeID string) prc.RouteUsage {
		return prc.RouteUsage{
			ID:          prc.RouteUsageID{TimetablePointID: points[source].TimetablePointID, RouteSequenceIndex: sequence},
			TrainNumber: "1001", ControlStationID: stationID, RouteID: routeID,
		}
	}
	want := []struct {
		arrivals, departures, shunting []prc.RouteUsage
	}{
		{departures: []prc.RouteUsage{usage(0, 0, "A", "D")}},
		{},
		{
			arrivals:   []prc.RouteUsage{usage(0, 1, "B", "H1"), usage(0, 2, "B", "H2")},
			departures: []prc.RouteUsage{usage(3, 0, "B", "D")},
			shunting:   []prc.RouteUsage{usage(2, 0, "B", "S")},
		},
		{},
		{
			arrivals: []prc.RouteUsage{usage(3, 1, "A", "H1")},
			shunting: []prc.RouteUsage{usage(4, 0, "A", "S")},
		},
		{},
	}
	for i, point := range points {
		if !reflect.DeepEqual(point.ArrivalRoutes, want[i].arrivals) ||
			!reflect.DeepEqual(point.DepartureRoutes, want[i].departures) ||
			!reflect.DeepEqual(point.ShuntingRoutes, want[i].shunting) {
			t.Fatalf("point %d: unexpected route groups: %+v", i, point)
		}
	}
	// 変換によってDB上の区間への所属を変更しない。
	saved, err := timetable.LoadTimetable(context.Background(), db, "grouping")
	if err != nil {
		t.Fatal(err)
	}
	if len(saved.Trains[0].Points[0].Routes) != 3 || len(saved.Trains[0].Points[3].Routes) != 2 {
		t.Fatal("route grouping changed the persisted sections")
	}
	core := prc.New(nil, plan, nil, nil)
	if err := core.Initialize(ctc.TrackingState{IsInitialized: true}); err != nil {
		t.Fatal(err)
	}
	state := core.GetTimetableState()
	if len(state.RouteUsagesByID) != 7 {
		t.Fatalf("route usage states lost during grouping: %+v", state.RouteUsagesByID)
	}
	for id, usage := range state.RouteUsagesByID {
		if usage.Phase != prc.RouteUsagePending {
			t.Fatalf("usage %v is not pending", id)
		}
	}
}

func TestLoadPlanBuildsConflictUsages(t *testing.T) {
	db, definitions := routeGroupingFixture(t)
	// テスト用の全進路で同じ競合点を共有させ、各地点に計画時刻を設定する。
	for i := range definitions {
		definitions[i].RouteClearTrackCircuitIDs = []string{"X"}
	}
	if err := db.Model(&timetable.TimetablePoint{}).Where("train_number = ?", "1001").Updates(map[string]any{
		"arrival_seconds":   gorm.Expr("sequence_index * 100"),
		"departure_seconds": gorm.Expr("sequence_index * 100 + 50"),
	}).Error; err != nil {
		t.Fatal(err)
	}
	plan, err := timetable.LoadTimetablePlan(context.Background(), db, "grouping", definitions)
	if err != nil {
		t.Fatal(err)
	}
	usages := plan.ConflictUsagesByCircuitID["X"]
	if len(usages) != 7 {
		t.Fatalf("unexpected usage count: %v", usages)
	}
	wantTimes := []int{50, 200, 200, 250, 250, 400, 450}
	for i, usage := range usages {
		if usage.ScheduledSeconds != wantTimes[i] {
			t.Fatalf("unexpected order: %v", usages)
		}
	}
}

func TestLoadPlanRejectsUnassignableRoutes(t *testing.T) {
	for _, tc := range []struct {
		name, message string
		modify        func(*gorm.DB, []ctc.TrackRouteDefinition) []ctc.TrackRouteDefinition
	}{
		{"unknown_route", "unknown route", func(_ *gorm.DB, routes []ctc.TrackRouteDefinition) []ctc.TrackRouteDefinition {
			return routes[1:]
		}},
		{"unknown_kind", "unknown kind", func(_ *gorm.DB, routes []ctc.TrackRouteDefinition) []ctc.TrackRouteDefinition {
			routes[0].Kind = "invalid"
			return routes
		}},
		{"no_departure_point", "no preceding station point", func(db *gorm.DB, routes []ctc.TrackRouteDefinition) []ctc.TrackRouteDefinition {
			if err := db.Model(&timetable.TimetablePoint{}).Where("train_number = ? AND sequence_index = ?", "1001", 0).Update("station_id", nil).Error; err != nil {
				t.Fatal(err)
			}
			return routes
		}},
		{"no_arrival_point", "no following station point", func(db *gorm.DB, routes []ctc.TrackRouteDefinition) []ctc.TrackRouteDefinition {
			if err := db.Model(&timetable.TimetablePoint{}).Where("train_number = ? AND sequence_index = ?", "1001", 2).Update("station_id", nil).Error; err != nil {
				t.Fatal(err)
			}
			return routes
		}},
	} {
		t.Run(tc.name, func(t *testing.T) {
			db, definitions := routeGroupingFixture(t)
			definitions = tc.modify(db, definitions)
			plan, err := timetable.LoadTimetablePlan(context.Background(), db, "grouping", definitions)
			if err == nil || !strings.Contains(err.Error(), tc.message) || plan.TrainsByNumber != nil {
				t.Fatalf("plan=%+v, error=%v, want %q", plan, err, tc.message)
			}
		})
	}
}

func routeGroupingFixture(t *testing.T) (*gorm.DB, []ctc.TrackRouteDefinition) {
	t.Helper()
	db := openDatabase(t, filepath.Join(t.TempDir(), "grouping.sqlite"))
	create(t, db, &[]timetable.Station{{StationID: "A"}, {StationID: "B"}})
	definitions := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "A", InterlockingRouteID: "D", Kind: ctc.RouteKindDeparture},
		{InterlockingStationID: "B", InterlockingRouteID: "H1", Kind: ctc.RouteKindHome},
		{InterlockingStationID: "B", InterlockingRouteID: "H2", Kind: ctc.RouteKindHome},
		{InterlockingStationID: "B", InterlockingRouteID: "D", Kind: ctc.RouteKindDeparture},
		{InterlockingStationID: "A", InterlockingRouteID: "H1", Kind: ctc.RouteKindHome},
		{InterlockingStationID: "B", InterlockingRouteID: "S", Kind: ctc.RouteKindShunting},
		{InterlockingStationID: "A", InterlockingRouteID: "S", Kind: ctc.RouteKindShunting},
	}
	for _, route := range definitions {
		create(t, db, &timetable.Route{ControlStationID: route.InterlockingStationID, RouteID: route.InterlockingRouteID})
	}
	route := func(stationID, routeID string, sequence int) timetable.TimetablePointRoute {
		return timetable.TimetablePointRoute{ControlStationID: stationID, RouteID: routeID, SequenceIndex: sequence}
	}
	points := []timetable.TimetablePoint{
		{StationID: stringPointer("A"), Routes: []timetable.TimetablePointRoute{route("A", "D", 0), route("B", "H1", 1), route("B", "H2", 2)}},
		{},
		{StationID: stringPointer("B"), Routes: []timetable.TimetablePointRoute{route("B", "S", 0)}},
		{Routes: []timetable.TimetablePointRoute{route("B", "D", 0), route("A", "H1", 1)}},
		{StationID: stringPointer("A"), Routes: []timetable.TimetablePointRoute{route("A", "S", 0)}},
		{},
	}
	for i := range points {
		points[i].SequenceIndex = i
		points[i].Handling = timetable.HandlingStop
		points[i].ArrivalSeconds = intPointer(i * 100)
		points[i].DepartureSeconds = intPointer(i*100 + 50)
		points[i].TrackCircuitID = fmt.Sprintf("P%d", i)
		create(t, db, &timetable.TrackCircuit{TrackCircuitID: points[i].TrackCircuitID})
	}
	create(t, db, &timetable.Timetable{
		ID:     "grouping",
		Trains: []timetable.TimetableTrain{{TrainNumber: "1001", Points: points}},
	})
	return db, definitions
}
