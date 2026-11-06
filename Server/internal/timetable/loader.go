package timetable

import (
	"context"
	"fmt"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
	"github.com/neko3141592/Nakatetsu/Server/internal/prc"
	"gorm.io/gorm"
)

func LoadTimetable(ctx context.Context, db *gorm.DB, id string) (Timetable, error) {
	var result Timetable
	err := db.WithContext(ctx).Preload("Trains", func(db *gorm.DB) *gorm.DB { return db.Order("train_number, id") }).
		Preload("Trains.TrainType").Preload("Trains.Points", orderedRecords).Preload("Trains.Points.Routes", orderedRecords).
		First(&result, "id = ?", id).Error
	if result.Trains == nil {
		result.Trains = []TimetableTrain{}
	}
	return result, err
}
func orderedRecords(db *gorm.DB) *gorm.DB { return db.Order("sequence_index") }

func LoadOperationPlan(ctx context.Context, db *gorm.DB, id string) (TimetableOperationPlan, error) {
	var result TimetableOperationPlan
	err := db.WithContext(ctx).Preload("Operations", func(db *gorm.DB) *gorm.DB { return db.Order("operation_number, id") }).
		Preload("Operations.Trains", orderedRecords).Preload("Operations.Trains.Train.TrainType").First(&result, "id = ?", id).Error
	if result.Operations == nil {
		result.Operations = []TimetableOperation{}
	}
	for i := range result.Operations {
		if result.Operations[i].Trains == nil {
			result.Operations[i].Trains = []TimetableOperationTrain{}
		}
	}
	return result, err
}

func LoadTimetablePlan(ctx context.Context, db *gorm.DB, id string, routes []ctc.TrackRouteDefinition, operationPlanIDs ...string) (prc.TimetablePlan, error) {
	source, err := LoadTimetable(ctx, db, id)
	if err != nil {
		return prc.TimetablePlan{}, err
	}
	var operations []TimetableOperation
	if len(operationPlanIDs) > 0 && operationPlanIDs[0] != "" {
		operationPlan, err := LoadOperationPlan(ctx, db, operationPlanIDs[0])
		if err != nil {
			return prc.TimetablePlan{}, err
		}
		if operationPlan.TimetableID != id {
			return prc.TimetablePlan{}, fmt.Errorf("運用計画の対象ダイヤが一致しません")
		}
		operations = operationPlan.Operations
	}
	trainNumbers := map[string]string{}
	for _, train := range source.Trains {
		if _, exists := trainNumbers[train.ID]; exists {
			return prc.TimetablePlan{}, fmt.Errorf("duplicate train ID: %s", train.ID)
		}
		trainNumbers[train.ID] = train.TrainNumber
	}
	definitions := make(map[ctc.RouteKey]ctc.TrackRouteDefinition, len(routes))
	for _, route := range routes {
		key := ctc.RouteKey{StationID: route.InterlockingStationID, RouteID: route.InterlockingRouteID}
		definitions[key] = route
	}
	plan := prc.TimetablePlan{
		TimetableID:        source.ID,
		DisplayName:        source.DisplayName,
		Revision:           source.Revision,
		TrainsByNumber:     make(map[string]prc.TrainPlan, len(source.Trains)),
		OperationsByNumber: make(map[string]prc.OperationPlan, len(operations)),
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
			}
			trainPlan.Points[i] = pointPlan
		}
		if err := assignRouteUsages(&trainPlan, train.Points, definitions); err != nil {
			return prc.TimetablePlan{}, err
		}
		if err := validateRouteTimes(trainPlan); err != nil {
			return prc.TimetablePlan{}, err
		}
		// The live Unity/CTC protocol identifies scheduled trains by number. Never silently overwrite duplicates.
		if _, exists := plan.TrainsByNumber[train.TrainNumber]; exists {
			return prc.TimetablePlan{}, fmt.Errorf("PRCでは同じダイヤ内の列番重複は使用できません: %s（編集・保存は可能）", train.TrainNumber)
		}
		plan.TrainsByNumber[train.TrainNumber] = trainPlan
	}
	for _, operation := range operations {
		operationPlan := prc.OperationPlan{
			OperationNumber: operation.OperationNumber,
			TrainNumbers:    make([]string, len(operation.Trains)),
		}
		for i, train := range operation.Trains {
			operationPlan.TrainNumbers[i] = trainNumbers[train.TrainID]
		}
		plan.OperationsByNumber[operation.OperationNumber] = operationPlan
	}
	// 初版はダイヤ固定のため、順序判定用の一覧を読込み時に一度だけ作る。
	plan.ConflictUsagesByCircuitID, err = prc.BuildConflictUsages(plan, prc.BuildConflictPoints(routes))
	if err != nil {
		return prc.TimetablePlan{}, err
	}
	return plan, nil
}
