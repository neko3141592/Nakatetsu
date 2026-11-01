package prc

import "github.com/neko3141592/Nakatetsu/Server/internal/ctc"

// CTCはPRC側が利用するCTCのインターフェース。
type CTC interface {
	GetStationControlMode(stationID string) (ctc.StationControlMode, error)
	GetTrackingState() ctc.TrackingState
}

type PRC struct {
	ctc                      CTC
	timetable                TimetablePlan
	state                    TimetableState
	requestTriggersByStation map[string][]string
	conflictPoints           map[string]map[ctc.RouteKey]struct{}
	// 軌道回路 -> 接近点
	approachPoints			 map[string]ApproachPoint
}

func New(core CTC, timetable TimetablePlan, requestTriggersByStation map[string][]string, routes []ctc.TrackRouteDefinition) *PRC {
	approachPoints := make(map[string]ApproachPoint)
	for stationID, circuitIDs := range requestTriggersByStation {
		for _, circuitID := range circuitIDs {
			approachPoints[circuitID] = ApproachPoint{
				StationID:      stationID,
				TrackCircuitID: circuitID,
			}
		}
	}

	return &PRC{
		ctc:                      core,
		timetable:                timetable,
		requestTriggersByStation: requestTriggersByStation,
		conflictPoints:           BuildConflictPoints(routes),
		approachPoints:           approachPoints,
	}
}

func (p *PRC) UpdateRouteRequests() {
	state := p.ctc.GetTrackingState()

	// 接近中の追跡IDを取得する
    for _, approach := range p.GetApproachTrain(state) {
        train, exists := state.TrainsByID[approach.TrackingTrainID]
        if !exists || train.TrainNumber == "" {
            continue
        }
		
        // 列番からダイヤを取得
        plan, exists := p.timetable.TrainsByNumber[train.TrainNumber]
        if !exists {
            continue
        }

        _ = plan // 次に、この計画から要求する進路を選ぶ。
    }
}
