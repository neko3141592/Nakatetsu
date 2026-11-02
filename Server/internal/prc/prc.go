package prc

import (
	"context"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

// CTCはPRC側が利用するCTCのインターフェース。
type CTC interface {
	GetStationControlMode(stationID string) (ctc.StationControlMode, error)
	GetTrackingState() ctc.TrackingState
	GetUnitySnapshot() *ctcv1.UnitySnapshot
}

// RouteControlはWeb APIと共通の進路要求送信窓口。
type RouteControl interface {
	SendRouteRequest(context.Context, *ctcv1.InterlockingRouteRequest) (*ctcv1.InterlockingRouteRequestResult, error)
}

type RouteRequestResult struct {
	Route  RouteUsage
	Result *ctcv1.InterlockingRouteRequestResult
	Err    error
}

type PRC struct {
	RouteControl RouteControl

	// 停車列車の出発進路を、ダイヤ上の出発時刻の何秒前に挙上するか。
	DepartureRequestLeadSeconds float64
	// 入換進路を挙上するまでの、地点到達後の待機秒数。
	ShuntingWaitSeconds float64
	// 一時拒否された要求を再判定するまでのシミュレーション秒数。
	RouteRequestRetrySeconds float64

	ctc                      CTC
	timetable                TimetablePlan
	state                    TimetableState
	requestTriggersByStation map[string][]string
	conflictPoints           map[string]map[ctc.RouteKey]struct{}
	// 駅ID・進路ID -> 使用する競合点の回路ID一覧。
	conflictPointsByRoute map[ctc.RouteKey][]string
	routeDefinitionsByKey map[ctc.RouteKey]ctc.TrackRouteDefinition
	routeUsagesByID       map[RouteUsageID]RouteUsage
	// 軌道回路 -> 接近点
	approachPoints map[string]ApproachPoint

	// 進路要求を受け取るチャネル
	pendingRequests map[RouteUsageID]chan RouteRequestResult
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

	conflictPoints := BuildConflictPoints(routes)
	routeDefinitions := make(map[ctc.RouteKey]ctc.TrackRouteDefinition, len(routes))
	for _, route := range routes {
		routeDefinitions[ctc.RouteKey{StationID: route.InterlockingStationID, RouteID: route.InterlockingRouteID}] = route
	}
	// 実行状態のIDから、対応する計画と設備を参照する索引。
	usages := make(map[RouteUsageID]RouteUsage)
	for _, train := range timetable.TrainsByNumber {
		for _, point := range train.Points {
			all := append(append([]RouteUsage{}, point.ArrivalRoutes...), point.DepartureRoutes...)
			all = append(all, point.ShuntingRoutes...)
			for _, usage := range all {
				usages[usage.ID] = usage
			}
		}
	}
	return &PRC{
		DepartureRequestLeadSeconds: 30,
		ShuntingWaitSeconds:         30,
		RouteRequestRetrySeconds:    1,
		ctc:                         core,
		timetable:                   timetable,
		requestTriggersByStation:    requestTriggersByStation,
		conflictPoints:              conflictPoints,
		conflictPointsByRoute:       buildConflictPointsByRoute(conflictPoints),
		routeDefinitionsByKey:       routeDefinitions,
		routeUsagesByID:             usages,
		approachPoints:              approachPoints,
		pendingRequests:             make(map[RouteUsageID]chan RouteRequestResult),
	}
}

func (p *PRC) UpdateRouteRequests() {
	p.receiveRouteRequestResults()
	trackingState := p.ctc.GetTrackingState()
	if !trackingState.IsInitialized || trackingState.NeedsReset {
		return
	}

	// 同じ追跡状態で進捗を更新してから、2種類の挙上を行う。
	snapshot := p.ctc.GetUnitySnapshot()
	worldTime := snapshot.GetWorldTime()
	if worldTime == nil {
		return
	}

	p.UpdatePointProgress(trackingState, float64(worldTime.WorldTimeSeconds))
	p.updateRouteUsageProgress(trackingState, snapshot)
	p.raiseRouteRequests(trackingState, worldTime)

	selected := p.selectRouteRequests()
	p.sendRouteRequests(selected)

}
