package ctc

// UpdateTrackingは前回の在線と比較して、列車追跡状態を更新する。
func UpdateTracking(
	state *TrackingState,
	occupied map[string]bool,
	definitions map[string]TrackCircuitDefinition,
	routeDefinitions []TrackRouteDefinition,
	routeRefs map[string][]RouteCircuitRef,
	routeStates map[RouteKey]InterlockingRouteState,
) {
	for circuitID, isOccupied := range occupied {
		if state.NeedsReset {
			return
		}

		wasOccupied := state.CircuitsByID[circuitID].IsOccupied

		if isOccupied {
			// 在線中はリンク未生成なら再評価する。既存リンクは変更しない。
			handleCircuitEntry(circuitID, state, definitions, routeDefinitions, routeRefs, routeStates)
		} else if wasOccupied && !isOccupied {
			// 在線が消えた場合
			handleCircuitExit(circuitID, state, occupied)
		}
	}
}

func handleCircuitEntry(
	circuitID string,
	state *TrackingState,
	definitions map[string]TrackCircuitDefinition,
	routeDefinitions []TrackRouteDefinition,
	routeRefs map[string][]RouteCircuitRef,
	routeStates map[RouteKey]InterlockingRouteState,
) {
	if state.NeedsReset {
		return
	}
	if _, exists := state.DynamicLinks[circuitID]; exists {
		return
	}

	definition, ok := definitions[circuitID]
	if !ok {
		state.NeedsReset = true
		return
	}

	var toID string
	if definition.ControlKind == TrackControlBlock {
		// 現在地から、所定方向の次回路へのリンクを作る。
		toID = definition.ToCircuitID
	} else if definition.ControlKind == TrackControlInterlocking {
		// 開通進路に次回路がなければ、固定の出口を使う。
		toID = definition.ToCircuitID

		for _, ref := range routeRefs[circuitID] {
			route := routeDefinitions[ref.RouteIndex]
			key := RouteKey{StationID: route.InterlockingStationID, RouteID: route.InterlockingRouteID}
			routeState := routeStates[key]
			if !routeState.IsAvailable || !routeState.PathEstablished {
				continue
			}
			nextIndex := ref.CircuitIndex + 1
			if nextIndex >= len(route.TrackCircuitIDs) {
				continue
			}

			// その軌道回路を保有する進路は1つしか開通していないことは保証されてる
			toID = route.TrackCircuitIDs[nextIndex]
			break
		}
	} else {
		state.NeedsReset = true
		return
	}

	if toID == "" {
		return
	}

	// リンク先の軌道回路を確認
	if _, exists := definitions[toID]; !exists {
		state.NeedsReset = true
		return
	}

	if state.DynamicLinks == nil {
		state.DynamicLinks = make(map[string]string)
	}
	state.DynamicLinks[circuitID] = toID
}

func handleCircuitExit(circuitID string, state *TrackingState, occupied map[string]bool) {
	if state.NeedsReset {
		return
	}
	toID, exists := state.DynamicLinks[circuitID]
	if !exists {
		return
	}

	from := state.CircuitsByID[circuitID]
	if from.TrainID != "" {
		if !occupied[toID] {
			state.NeedsReset = true
			return
		}
		to := state.CircuitsByID[toID]
		if to.TrainID != "" && to.TrainID != from.TrainID {
			state.NeedsReset = true
			return
		}
		to.TrainID = from.TrainID
		from.TrainID = ""
		state.CircuitsByID[toID] = to
		state.CircuitsByID[circuitID] = from
	}

	delete(state.DynamicLinks, circuitID)
}
