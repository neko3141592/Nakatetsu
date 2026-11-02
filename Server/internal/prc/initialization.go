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
		TrainsByNumber:        make(map[string]TrainState, len(p.timetable.TrainsByNumber)),
		PointsByID:            make(map[int64]PointState),
		RouteUsagesByID:       make(map[RouteUsageID]RouteUsageState),
		RaisedRouteUsagesByID: make(map[RouteUsageID]RouteUsage),
	}

	for number, train := range p.timetable.TrainsByNumber {
		state.TrainsByNumber[number] = TrainState{}
		for _, point := range train.Points {
			state.PointsByID[point.TimetablePointID] = PointState{}
			for _, routes := range [][]RouteUsage{point.ArrivalRoutes, point.DepartureRoutes, point.ShuntingRoutes} {
				for _, route := range routes {
					state.RouteUsagesByID[route.ID] = RouteUsageState{Phase: RouteUsagePending}
				}
			}
		}
	}

	p.state = state
	return nil
}
