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

	p.state = state
	return nil
}
