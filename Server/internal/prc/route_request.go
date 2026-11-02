package prc

import (
	"context"
	"time"

	"github.com/google/uuid"
	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func (p *PRC) raiseRouteRequests(trackingState ctc.TrackingState, worldTime *ctcv1.WorldTimeInformation) {
	approachRoutes := p.raiseApproachRouteRequests(trackingState)
	departureRoutes := p.raiseDepartureRouteRequests(trackingState, worldTime)

	if p.state.RaisedRouteUsagesByID == nil {
		p.state.RaisedRouteUsagesByID = make(map[RouteUsageID]RouteUsage)
	}
	for id := range p.state.RaisedRouteUsagesByID {
		usage, exists := p.state.RouteUsagesByID[id]
		if !exists || usage.Phase != RouteUsagePending {
			delete(p.state.RaisedRouteUsagesByID, id)
		}
	}

	routes := append(approachRoutes, departureRoutes...)
	for _, route := range routes {
		usage, exists := p.state.RouteUsagesByID[route.ID]
		if exists && usage.Phase == RouteUsagePending {
			p.state.RaisedRouteUsagesByID[route.ID] = route
		}
	}
}

func (p *PRC) raiseApproachRouteRequests(trackingState ctc.TrackingState) []RouteUsage {
	if !trackingState.IsInitialized || trackingState.NeedsReset {
		return nil
	}

	var routeUsages []RouteUsage

	// 接近中の追跡IDを取得する
	for _, approach := range p.GetApproachTrain(trackingState) {
		train, exists := trackingState.TrainsByID[approach.TrackingTrainID]
		if !exists || train.TrainNumber == "" {
			continue
		}

		// 列番からダイヤを取得
		plan, exists := p.timetable.TrainsByNumber[train.TrainNumber]

		if !exists {
			continue
		}

		// 列番から、現在の列車位置を取得
		trainState, exists := p.state.TrainsByNumber[train.TrainNumber]
		if !exists {
			continue
		}

		start := 0
		if trainState.CurrentPointID != nil {
			start = -1
			for i, point := range plan.Points {
				if point.TimetablePointID == *trainState.CurrentPointID {
					start = i + 1
					break
				}
			}
		}

		if start < 0 {
			continue
		}
		// 接近先の駅地点から場内を取得し、通過の場合だけ出発も挙上する。
		for _, point := range plan.Points[start:] {
			if point.StationID == nil || *point.StationID != approach.StationID {
				continue
			}
			routeUsages = append(routeUsages, point.ArrivalRoutes...)
			if point.Handling == HandlingPass {
				routeUsages = append(routeUsages, point.DepartureRoutes...)
			}
			break
		}
	}

	return routeUsages
}

func (p *PRC) raiseDepartureRouteRequests(tracking ctc.TrackingState, worldTime *ctcv1.WorldTimeInformation) []RouteUsage {
	if !tracking.IsInitialized || tracking.NeedsReset || worldTime == nil {
		return nil
	}

	var routes []RouteUsage
	for trainID, train := range tracking.TrainsByID {
		if train.TrainNumber == "" {
			continue
		}

		// ダイヤ上の進捗状況を確認する
		progress, exists := p.state.TrainsByNumber[train.TrainNumber]
		if !exists || progress.CurrentPointID == nil {
			continue
		}

		// 列車のダイヤを確認する
		plan, exists := p.timetable.TrainsByNumber[train.TrainNumber]
		if !exists {
			continue
		}

		for _, point := range plan.Points {
			if point.TimetablePointID != *progress.CurrentPointID {
				continue
			}

			// 通過地点と、すでに列車が離れた停車地点は対象外にする
			circuit, exists := tracking.CircuitsByID[point.TrackCircuitID]
			if point.Handling != HandlingStop || !exists || !circuit.IsOccupied || circuit.TrainID != trainID {
				break
			}

			// 出発・入換とも開始予定時刻を必須にする。
			if point.DepartureSeconds == nil {
				break
			}
			now := float64(worldTime.WorldTimeSeconds)
			threshold := float64(*point.DepartureSeconds) - p.DepartureRequestLeadSeconds
			if !(now >= threshold) {
				break
			}
			routes = append(routes, point.DepartureRoutes...)
			// 入換だけ、地点到達からの待機時間も確認する。
			if since := progress.CurrentPointOccupiedSinceSeconds; since != nil && now-*since >= p.ShuntingWaitSeconds {
				routes = append(routes, point.ShuntingRoutes...)
			}
			break
		}
	}
	return routes
}

func (p *PRC) selectRouteRequests() []RouteUsage {
	tracking := p.ctc.GetTrackingState()
	var selected []RouteUsage
	for _, route := range p.state.RaisedRouteUsagesByID {
		if p.checkRouteRequestState(route, tracking) && p.checkRouteRequestOrder(route) {
			selected = append(selected, route)
		}
	}
	return selected
}

// 挙上候補から基本条件を確認
func (p *PRC) checkRouteRequestState(route RouteUsage, tracking ctc.TrackingState) bool {
	if !tracking.IsInitialized || tracking.NeedsReset {
		return false
	}

	// 抑止・キャンセル時は進路要求をしない
	train, exists := p.state.TrainsByNumber[route.TrainNumber]
	if !exists || train.IsHeld || train.IsCancelled {
		return false
	}

	usage, exists := p.state.RouteUsagesByID[route.ID]
	if !exists || trackingTrainID(tracking, route.TrainNumber) == "" {
		return false
	}
	if float64(p.ctc.GetUnitySnapshot().GetWorldTime().GetWorldTimeSeconds()) < usage.RetryAfterSeconds {
		return false
	}

	mode, err := p.ctc.GetStationControlMode(route.ControlStationID)
	return err == nil && mode == ctc.StationControlAuto
}

func (p *PRC) checkRouteRequestOrder(route RouteUsage) bool {
	key := ctc.RouteKey{
		StationID: route.ControlStationID,
		RouteID:   route.RouteID,
	}
	conflictPoints := p.conflictPointsByRoute[key]

	for _, conflictPoint := range conflictPoints {
		usages := p.timetable.ConflictUsagesByCircuitID[conflictPoint]
		found := false
		for _, usage := range usages {
			if usage.Usage.ID == route.ID {
				found = true
				break
			}

			// 自列車の進路は待たなくていい
			if usage.Usage.TrainNumber == route.TrainNumber {
				continue
			}

			state, exists := p.state.RouteUsagesByID[usage.Usage.ID]
			if !exists || (state.Phase != RouteUsageCompleted && state.Phase != RouteUsageCancelled) {
				return false
			}
		}

		if !found {
			return false
		}

	}

	return true
}

func (p *PRC) sendRouteRequests(routes []RouteUsage) {
	if p.RouteControl == nil {
		return
	}
	tracking := p.ctc.GetTrackingState()
	// 進路を要求中に変更し、候補から削除する
	for _, route := range routes {
		state, exists := p.state.RouteUsagesByID[route.ID]
		trainID := trackingTrainID(tracking, route.TrainNumber)
		if !exists || state.Phase != RouteUsagePending || trainID == "" {
			continue
		}
		state.Phase = RouteUsageRequested
		state.TrackingTrainID = trainID
		state.LastCircuitIndex = 0
		state.WasRouteSet = false
		p.state.RouteUsagesByID[route.ID] = state

		delete(p.state.RaisedRouteUsagesByID, route.ID)

		request := &ctcv1.InterlockingRouteRequest{
			RequestId: uuid.NewString(),
			StationId: route.ControlStationID,
			RouteId:   route.RouteID,
			Operation: ctcv1.InterlockingRouteOperation_INTERLOCKING_ROUTE_OPERATION_SET,
		}

		reply := make(chan RouteRequestResult, 1)
		p.pendingRequests[route.ID] = reply

		go func() {
			ctx, cancel := context.WithTimeout(context.Background(), 5*time.Second)
			defer cancel()

			result, err := p.RouteControl.SendRouteRequest(ctx, request)
			reply <- RouteRequestResult{
				Route:  route,
				Result: result,
				Err:    err,
			}
		}()
	}

}

func (p *PRC) receiveRouteRequestResults() {
	for id, reply := range p.pendingRequests {
		select {
		case result := <-reply:
			delete(p.pendingRequests, id)

			if result.Err != nil || result.Result == nil {
				// TODO: 結果不明時の処理は後で実装
				continue
			}
			if result.Result.Accepted {
				continue
			}

			state, exists := p.state.RouteUsagesByID[id]
			if !exists || state.Phase != RouteUsageRequested || state.WasRouteSet {
				continue
			}
			// 設備不備は自動再要求を止め、一時的な拒否だけを再試行する。
			switch result.Result.GetErrorCode() {
			case ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_NOT_INITIALIZED,
				ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_INPUT_UNAVAILABLE,
				ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_CIRCUIT_OCCUPIED,
				ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_CIRCUIT_RESERVED,
				ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_ROUTE_CONFLICT,
				ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_TURNOUT_POSITION_CONFLICT:
			case ctcv1.InterlockingRouteErrorCode_INTERLOCKING_ROUTE_ERROR_CODE_ALREADY_SET:
				continue // 成否を決めつけず、スナップショットによる確認を待つ。
			default:
				state.Phase = RouteUsageFailed
				p.state.RouteUsagesByID[id] = state
				continue
			}
			state.Phase = RouteUsagePending
			now := p.ctc.GetUnitySnapshot().GetWorldTime().GetWorldTimeSeconds()
			state.RetryAfterSeconds = float64(now) + p.RouteRequestRetrySeconds
			p.state.RouteUsagesByID[id] = state
			p.state.RaisedRouteUsagesByID[id] = result.Route
		default:
			// 応答待ち
		}
	}
}
