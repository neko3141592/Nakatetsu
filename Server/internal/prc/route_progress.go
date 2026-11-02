package prc

import (
	"slices"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

// updateRouteUsageProgressは受付結果ではなく、設備状態と対象列車の最後尾から進捗を確認する。
func (p *PRC) updateRouteUsageProgress(tracking ctc.TrackingState, snapshot *ctcv1.UnitySnapshot) {
	for id, usage := range p.routeUsagesByID {
		state := p.state.RouteUsagesByID[id]
		if state.Phase != RouteUsageRequested && state.Phase != RouteUsageReserved && state.Phase != RouteUsageInUse {
			continue
		}
		key := ctc.RouteKey{StationID: usage.ControlStationID, RouteID: usage.RouteID}
		status := routeStatus(snapshot, key)
		definition, exists := p.routeDefinitionsByKey[key]
		if status == nil || !status.IsAvailable || !exists || len(definition.TrackCircuitIDs) < 2 {
			continue
		}
		if status.CancelPending {
			// 手動取消された使用予定は、自動で設定し直さない。
			state.Phase = RouteUsageCancelled
		} else {
			if status.IsRouteSet {
				state.WasRouteSet = true
			}
			if state.Phase == RouteUsageRequested && status.IsRouteSet && status.RouteLocked && status.PathEstablished {
				state.Phase = RouteUsageReserved
			}
			for circuitID, circuit := range tracking.CircuitsByID {
				if !circuit.IsOccupied || circuit.TrainID == "" || circuit.TrainID != state.TrackingTrainID {
					continue
				}
				index := slices.Index(definition.TrackCircuitIDs, circuitID)
				if index > 0 {
					state.LastCircuitIndex = max(state.LastCircuitIndex, index)
					state.Phase = RouteUsageInUse
				}
			}
			if !status.IsRouteSet && !status.RouteLocked {
				if state.Phase == RouteUsageInUse && state.LastCircuitIndex == len(definition.TrackCircuitIDs)-1 {
					// 到達先までの最後尾通過と連動の解錠を、両方確認する。
					state.Phase = RouteUsageCompleted
				} else if state.Phase != RouteUsageInUse && state.WasRouteSet {
					state.Phase = RouteUsageCancelled
				}
			}
		}
		p.state.RouteUsagesByID[id] = state
	}
}

func routeStatus(snapshot *ctcv1.UnitySnapshot, key ctc.RouteKey) *ctcv1.InterlockingRouteStatus {
	station := snapshot.GetInterlocking().GetStationsById()[key.StationID]
	if !station.GetIsInitialized() || !station.GetHasOutput() {
		return nil
	}
	return station.GetRoutesById()[key.RouteID]
}

func trackingTrainID(tracking ctc.TrackingState, number string) string {
	var found string
	for id, train := range tracking.TrainsByID {
		if train.TrainNumber == number {
			if found != "" {
				return "" // 同じ列番が複数個体にある場合は対象を決めない。
			}
			found = id
		}
	}
	return found
}
