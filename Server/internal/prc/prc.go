package prc

import "github.com/neko3141592/Nakatetsu/Server/internal/ctc"

// CTCはPRC側が利用するCTCのインターフェース。
type CTC interface {
	GetStationControlMode(stationID string) (ctc.StationControlMode, error)
}

type PRC struct {
	ctc                      CTC
	timetable                TimetablePlan
	state                    TimetableState
	requestTriggersByStation map[string][]string
	conflictPoints           map[string]map[ctc.RouteKey]struct{}
}

func New(core CTC, timetable TimetablePlan, requestTriggersByStation map[string][]string, routes []ctc.TrackRouteDefinition) *PRC {
	return &PRC{
		ctc:                      core,
		timetable:                timetable,
		requestTriggersByStation: requestTriggersByStation,
		conflictPoints:           buildConflictPoints(routes),
	}
}

func (p *PRC) UpdateRouteRequests() {
	// 接近点に到達した列車を確認し、進路要求を挙上

}
