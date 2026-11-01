package prc

import "maps"

// GetTimetableStateは実行状態のコピーを返す。
// PRCの更新と同時には呼び出さないこと。
func (p *PRC) GetTimetableState() TimetableState {
	state := p.state
	state.TrainsByNumber = maps.Clone(p.state.TrainsByNumber)
	state.PointsByID = maps.Clone(p.state.PointsByID)
	state.RouteUsagesByID = maps.Clone(p.state.RouteUsagesByID)
	for number, train := range state.TrainsByNumber {
		train.CurrentPointID = copyStateValue(train.CurrentPointID)
		state.TrainsByNumber[number] = train
	}
	for id, point := range state.PointsByID {
		point.ActualArrivalSeconds = copyStateValue(point.ActualArrivalSeconds)
		point.ActualDepartureSeconds = copyStateValue(point.ActualDepartureSeconds)
		state.PointsByID[id] = point
	}
	return state
}

func copyStateValue[T any](value *T) *T {
	if value == nil {
		return nil
	}
	copied := *value
	return &copied
}
