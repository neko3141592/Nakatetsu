package prc

import (
	"fmt"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func (p *PRC) Initialize(tracking ctc.TrackingState) error {
	if !tracking.IsInitialized {
		return fmt.Errorf("CTC tracking is not initialized")
	}

	state := TimetableState{
		TrainsByNumber:  make(map[string]TrainState, len(p.timetable.TrainsByNumber)),
		PointsByID:      make(map[int64]PointState),
		RouteUsagesByID: make(map[RouteUsageID]RouteUsageState, len(p.timetable.RouteUsagesByID)),
	}

	for number, train := range p.timetable.TrainsByNumber {
		state.TrainsByNumber[number] = TrainState{}
		for _, point := range train.Points {
			state.PointsByID[point.TimetablePointID] = PointState{}
		}
	}

	for id := range p.timetable.RouteUsagesByID {
		state.RouteUsagesByID[id] = RouteUsageState{Phase: RouteUsagePending}
	}

	for trackingID, tracked := range tracking.TrainsByID {
		number := tracked.TrainNumber
		if tracked.OperationNumber != "" {
			operation, ok := p.timetable.OperationsByNumber[tracked.OperationNumber]
			if !ok || len(operation.TrainNumbers) == 0 {
				return fmt.Errorf("train %q: unknown or empty operation %q", trackingID, tracked.OperationNumber)
			}
			number = operation.TrainNumbers[0]
		}
		if number == "" {
			continue
		}
		train, ok := p.timetable.TrainsByNumber[number]
		if !ok || len(train.Points) == 0 {
			return fmt.Errorf("train %q: unknown train number or missing points %q", trackingID, number)
		}
		assigned := state.TrainsByNumber[number]
		if assigned.TrackingTrainID != nil {
			return fmt.Errorf("train number %q is assigned to multiple trains", number)
		}

		pointID := train.Points[0].TimetablePointID
		assigned.TrackingTrainID = &trackingID
		assigned.CurrentPointID = &pointID
		if tracked.OperationNumber != "" {
			operationNumber := tracked.OperationNumber
			assigned.OperationNumber = &operationNumber
		}
		state.TrainsByNumber[number] = assigned
	}
	p.state = state
	return nil
}
