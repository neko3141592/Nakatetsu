package timetable

import (
	"context"

	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
	"gorm.io/gorm"
)

func LoadTimetable(ctx context.Context, db *gorm.DB, id string) (Timetable, error) {
	var result Timetable
	ordered := func(db *gorm.DB) *gorm.DB {
		return db.Order("sequence_index ASC")
	}
	err := db.WithContext(ctx).
		Preload("Trains").
		Preload("Trains.Points", ordered).
		Preload("Trains.Points.Routes", ordered).
		Preload("Operations").
		Preload("Operations.Trains", ordered).
		First(&result, "id = ?", id).Error
	if err != nil {
		return Timetable{}, err
	}
	return result, nil
}

func LoadTimetablePlan(ctx context.Context, db *gorm.DB, id string) (prc.TimetablePlan, error) {
	source, err := LoadTimetable(ctx, db, id)
	if err != nil {
		return prc.TimetablePlan{}, err
	}
	plan := prc.TimetablePlan{
		TimetableID:        source.ID,
		DisplayName:        source.DisplayName,
		Revision:           source.Revision,
		TrainsByNumber:     make(map[string]prc.TrainPlan, len(source.Trains)),
		OperationsByNumber: make(map[string]prc.OperationPlan, len(source.Operations)),
		RouteUsagesByID:    make(map[prc.RouteUsageID]prc.RouteUsage),
	}
	for _, train := range source.Trains {
		trainPlan := prc.TrainPlan{
			TrainNumber: train.TrainNumber,
			TrainTypeID: train.TrainTypeID,
			Points:      make([]prc.PointPlan, len(train.Points)),
		}
		for i, point := range train.Points {
			pointPlan := prc.PointPlan{
				TimetablePointID: point.TimetablePointID,
				SequenceIndex:    point.SequenceIndex,
				StationID:        point.StationID,
				TrackCircuitID:   point.TrackCircuitID,
				Handling:         prc.PointHandling(point.Handling),
				ArrivalSeconds:   point.ArrivalSeconds,
				DepartureSeconds: point.DepartureSeconds,
				RouteUsageIDs:    make([]prc.RouteUsageID, len(point.Routes)),
			}
			for j, route := range point.Routes {
				usageID := prc.RouteUsageID{TimetablePointID: point.TimetablePointID, RouteSequenceIndex: route.SequenceIndex}
				pointPlan.RouteUsageIDs[j] = usageID
				plan.RouteUsagesByID[usageID] = prc.RouteUsage{
					ID:               usageID,
					TrainNumber:      train.TrainNumber,
					ControlStationID: route.ControlStationID,
					RouteID:          route.RouteID,
				}
			}
			trainPlan.Points[i] = pointPlan
		}
		plan.TrainsByNumber[train.TrainNumber] = trainPlan
	}
	for _, operation := range source.Operations {
		operationPlan := prc.OperationPlan{
			OperationNumber: operation.OperationNumber,
			TrainNumbers:    make([]string, len(operation.Trains)),
		}
		for i, train := range operation.Trains {
			operationPlan.TrainNumbers[i] = train.TrainNumber
		}
		plan.OperationsByNumber[operation.OperationNumber] = operationPlan
	}
	return plan, nil
}
