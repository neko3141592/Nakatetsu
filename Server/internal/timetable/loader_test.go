package timetable_test

import (
	"context"
	"errors"
	"path/filepath"
	"reflect"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
	"github.com/neko3141592/Nakatetsu/Server/internal/timetable"
	"gorm.io/gorm"
)

func TestLoadTimetable(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	saved := seed(t, db)[0]
	got, err := timetable.LoadTimetable(context.Background(), db, saved.ID)
	if err != nil {
		t.Fatal(err)
	}
	if got.ID != saved.ID || got.DisplayName != saved.DisplayName || got.Revision != saved.Revision || len(got.Trains) != 2 || len(got.Operations) != 1 {
		t.Fatalf("unexpected timetable: %+v", got)
	}
	var train timetable.TimetableTrain
	for _, candidate := range got.Trains {
		if candidate.TrainNumber == "0001" {
			train = candidate
		}
	}
	if len(train.Points) != 3 {
		t.Fatalf("unexpected points: %+v", train.Points)
	}
	for i, point := range train.Points {
		if point.SequenceIndex != i {
			t.Fatalf("unexpected point order: %+v", train.Points)
		}
	}
	point := train.Points[0]
	if point.ArrivalSeconds != nil || point.DepartureSeconds == nil || *point.DepartureSeconds != 0 || train.Points[2].ArrivalSeconds == nil || *train.Points[2].ArrivalSeconds != 90600 {
		t.Fatalf("unexpected times: %+v", train.Points)
	}
	if len(point.Routes) != 2 || point.Routes[0].SequenceIndex != 0 || point.Routes[0].ControlStationID != "NT01" || point.Routes[1].SequenceIndex != 1 || point.Routes[1].ControlStationID != "NT05" {
		t.Fatalf("unexpected route order: %+v", point.Routes)
	}
	operation := got.Operations[0]
	if operation.OperationNumber != "A" || len(operation.Trains) != 2 || operation.Trains[0].TrainNumber != "0001" || operation.Trains[1].TrainNumber != "0002" {
		t.Fatalf("unexpected operation: %+v", operation)
	}

	unassigned := timetable.Timetable{
		ID:          "unassigned",
		DisplayName: "運用未所属",
		Trains:      []timetable.TimetableTrain{{TrainNumber: "0001"}},
	}
	create(t, db.Omit("Trains.*"), &unassigned)
	got, err = timetable.LoadTimetable(context.Background(), db, unassigned.ID)
	if err != nil {
		t.Fatal(err)
	}
	if len(got.Operations) != 0 || len(got.Trains) != 1 || len(got.Trains[0].Points) != 3 || len(got.Trains[0].Points[0].Routes) != 2 {
		t.Fatalf("unassigned train was not loaded: %+v", got)
	}
}

func TestLoadTimetableCombinesOperationAndTemporaryTrains(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	seed(t, db)
	create(t, db, &timetable.TimetableTrain{
		TrainNumber: "9001", TrainTypeID: "local",
		Points: []timetable.TimetablePoint{{TrackCircuitID: "origin", DepartureSeconds: intPointer(0)}},
	})
	for _, tc := range []struct {
		id         string
		temporary  []string
		operations []string
		want       []string
	}{
		{"operations-only", []string{}, []string{"A", "B"}, []string{"0001", "0002"}},
		{"temporary-only", []string{"9001"}, []string{}, []string{"9001"}},
		{"mixed", []string{"0001", "9001"}, []string{"A", "B"}, []string{"0001", "0002", "9001"}},
		{"empty", []string{}, []string{}, []string{}},
	} {
		t.Run(tc.id, func(t *testing.T) {
			saved := timetable.Timetable{ID: tc.id}
			for _, number := range tc.temporary {
				saved.Trains = append(saved.Trains, timetable.TimetableTrain{TrainNumber: number})
			}
			for _, number := range tc.operations {
				saved.Operations = append(saved.Operations, timetable.TimetableOperation{OperationNumber: number})
			}
			create(t, db.Omit("Trains.*", "Operations.*"), &saved)
			got, err := timetable.LoadTimetable(context.Background(), db, saved.ID)
			if err != nil {
				t.Fatal(err)
			}
			numbers := []string{}
			for _, train := range got.Trains {
				numbers = append(numbers, train.TrainNumber)
				if train.TrainNumber == "0001" && (len(train.Points) != 3 || len(train.Points[0].Routes) != 2 || train.Points[0].Routes[0].SequenceIndex != 0) {
					t.Fatalf("operation train details were not loaded in order: %+v", train)
				}
				if train.TrainNumber == "9001" && (len(train.Points) != 1 || train.Points[0].DepartureSeconds == nil || *train.Points[0].DepartureSeconds != 0) {
					t.Fatalf("temporary train details were not loaded: %+v", train)
				}
			}
			if !reflect.DeepEqual(numbers, tc.want) || !reflect.DeepEqual(got.TemporaryTrainNumbers, tc.temporary) {
				t.Fatalf("unexpected trains: all=%v, temporary=%v", numbers, got.TemporaryTrainNumbers)
			}
			plan, err := timetable.LoadTimetablePlan(context.Background(), db, saved.ID)
			if err != nil || len(plan.TrainsByNumber) != len(tc.want) || len(plan.OperationsByNumber) != len(tc.operations) {
				t.Fatalf("unexpected plan: %+v, error=%v", plan, err)
			}
			for _, number := range tc.want {
				if _, ok := plan.TrainsByNumber[number]; !ok {
					t.Fatalf("train %s is missing from the plan", number)
				}
			}
			if got := db.Model(&saved).Association("Trains").Count(); got != int64(len(tc.temporary)) {
				t.Fatalf("loading persisted operation trains as temporary trains: count=%d", got)
			}
		})
	}
}

func TestLoadTimetableErrors(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	id := seed(t, db)[0].ID
	for _, missingID := range []string{"", "missing", "weekday' OR 1=1 --"} {
		got, err := timetable.LoadTimetable(context.Background(), db, missingID)
		if !errors.Is(err, gorm.ErrRecordNotFound) || got.ID != "" {
			t.Fatalf("id=%q: timetable=%+v, err=%v", missingID, got, err)
		}
		plan, err := timetable.LoadTimetablePlan(context.Background(), db, missingID)
		if !errors.Is(err, gorm.ErrRecordNotFound) || plan.TimetableID != "" {
			t.Fatalf("id=%q: plan=%+v, err=%v", missingID, plan, err)
		}
	}
	ctx, cancel := context.WithCancel(context.Background())
	cancel()
	if _, err := timetable.LoadTimetable(ctx, db, id); !errors.Is(err, context.Canceled) {
		t.Fatalf("err=%v, want context.Canceled", err)
	}
	if _, err := timetable.LoadTimetablePlan(ctx, db, id); !errors.Is(err, context.Canceled) {
		t.Fatalf("err=%v, want context.Canceled", err)
	}
}

func TestLoadTimetablePlan(t *testing.T) {
	db := openDatabase(t, filepath.Join(t.TempDir(), "timetable.sqlite"))
	saved := seed(t, db)[0]
	if err := db.Model(&timetable.Timetable{}).Where("id = ?", saved.ID).Update("revision", 3).Error; err != nil {
		t.Fatal(err)
	}
	var firstPoint timetable.TimetablePoint
	if err := db.First(&firstPoint, "train_number = ? AND sequence_index = ?", "0001", 0).Error; err != nil {
		t.Fatal(err)
	}
	create(t, db, &timetable.TimetablePointRoute{
		TimetablePointID: firstPoint.TimetablePointID,
		SequenceIndex:    5,
		ControlStationID: "NT01",
		RouteID:          "1R",
	})
	plan, err := timetable.LoadTimetablePlan(context.Background(), db, saved.ID)
	if err != nil {
		t.Fatal(err)
	}
	if plan.TimetableID != saved.ID || plan.DisplayName != saved.DisplayName || plan.Revision != 3 || len(plan.TrainsByNumber) != 2 || len(plan.OperationsByNumber) != 1 {
		t.Fatalf("unexpected plan: %+v", plan)
	}
	train := plan.TrainsByNumber["0001"]
	if train.TrainNumber != "0001" || train.TrainTypeID != "local" || len(train.Points) != 3 {
		t.Fatalf("unexpected train: %+v", train)
	}
	for i, point := range train.Points {
		if point.SequenceIndex != i {
			t.Fatalf("unexpected point order: %+v", train.Points)
		}
	}
	point := train.Points[0]
	if point.TimetablePointID != firstPoint.TimetablePointID || point.StationID == nil || *point.StationID != "NT01" || point.TrackCircuitID != "origin" || point.Handling != prc.HandlingStop {
		t.Fatalf("unexpected first point: %+v", point)
	}
	if point.ArrivalSeconds != nil || point.DepartureSeconds == nil || *point.DepartureSeconds != 0 || train.Points[1].StationID != nil || train.Points[1].Handling != prc.HandlingPass || train.Points[2].ArrivalSeconds == nil || *train.Points[2].ArrivalSeconds != 90600 {
		t.Fatalf("unexpected point handling or times: %+v", train.Points)
	}
	wantUsages := make(map[prc.RouteUsageID]prc.RouteUsage)
	var wantIDs []prc.RouteUsageID
	for i, sequence := range []int{0, 1, 5} {
		id := prc.RouteUsageID{TimetablePointID: firstPoint.TimetablePointID, RouteSequenceIndex: sequence}
		wantIDs = append(wantIDs, id)
		wantUsages[id] = prc.RouteUsage{
			ID: id, TrainNumber: "0001", ControlStationID: []string{"NT01", "NT05", "NT01"}[i], RouteID: "1R",
		}
	}
	if !reflect.DeepEqual(point.RouteUsageIDs, wantIDs) || !reflect.DeepEqual(plan.RouteUsagesByID, wantUsages) {
		t.Fatalf("unexpected route usages: ids=%v, usages=%v", point.RouteUsageIDs, plan.RouteUsagesByID)
	}
	operation := plan.OperationsByNumber["A"]
	if operation.OperationNumber != "A" || !reflect.DeepEqual(operation.TrainNumbers, []string{"0001", "0002"}) {
		t.Fatalf("unexpected operation: %+v", operation)
	}
	unassigned := timetable.Timetable{
		ID:          "unassigned",
		DisplayName: "運用未所属",
		Trains:      []timetable.TimetableTrain{{TrainNumber: "0001"}},
	}
	create(t, db.Omit("Trains.*"), &unassigned)
	plan, err = timetable.LoadTimetablePlan(context.Background(), db, unassigned.ID)
	if err != nil {
		t.Fatal(err)
	}
	if len(plan.OperationsByNumber) != 0 || len(plan.TrainsByNumber) != 1 || len(plan.RouteUsagesByID) != 3 {
		t.Fatalf("unassigned train was not converted: %+v", plan)
	}
}
