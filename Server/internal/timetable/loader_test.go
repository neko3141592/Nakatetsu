package timetable_test

import (
	"context"
	"errors"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
	"path/filepath"
	"testing"
)

func loaderRouteDefinitions() []ctc.TrackRouteDefinition {
	return []ctc.TrackRouteDefinition{{InterlockingStationID: "NT01", InterlockingRouteID: "1R", Kind: ctc.RouteKindDeparture}, {InterlockingStationID: "NT05", InterlockingRouteID: "1R", Kind: ctc.RouteKindHome}}
}
func TestLoadTimetablePlan(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "db"))
	seed(t, db)
	got, err := timetable.LoadTimetablePlan(t.Context(), db, "weekday", loaderRouteDefinitions())
	if err != nil || len(got.TrainsByNumber) != 2 || len(got.OperationsByNumber) != 0 {
		t.Fatalf("%+v %v", got, err)
	}
	got, err = timetable.LoadTimetablePlan(t.Context(), db, "weekday", loaderRouteDefinitions(), "weekday-plan")
	if err != nil || len(got.OperationsByNumber) != 1 || len(got.OperationsByNumber["A"].TrainNumbers) != 2 {
		t.Fatalf("%+v %v", got, err)
	}
	if _, err = timetable.LoadTimetablePlan(t.Context(), db, "weekday", loaderRouteDefinitions(), "holiday-plan"); err == nil {
		t.Fatal("accepted wrong timetable plan")
	}
	create(t, db, &timetable.TimetableTrain{TimetableID: "weekday", TrainNumber: "0001"})
	if _, err = timetable.LoadTimetablePlan(t.Context(), db, "weekday", loaderRouteDefinitions()); err == nil {
		t.Fatal("silently overwrote duplicate runtime number")
	}
}
func TestLoadTimetableErrors(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "db"))
	for _, id := range []string{"", "missing", "weekday' OR 1=1 --"} {
		if _, err := timetable.LoadTimetable(t.Context(), db, id); !errors.Is(err, gorm.ErrRecordNotFound) {
			t.Fatal(err)
		}
	}
	ctx, cancel := context.WithCancel(t.Context())
	cancel()
	if _, err := timetable.LoadTimetable(ctx, db, "x"); !errors.Is(err, context.Canceled) {
		t.Fatal(err)
	}
}
