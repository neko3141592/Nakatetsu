package prc

import "github.com/neko3141592/Nakatetsu/Server/internal/ctc"

// UpdatePointProgressは最後尾の回路から、最後に到達したダイヤ地点を更新する。
// 最初の地点から順に照合する。停止完了の判定は行わない。
func (p *PRC) UpdatePointProgress(tracking ctc.TrackingState, now float64) {
	if !tracking.IsInitialized || tracking.NeedsReset {
		return
	}

	for circuitID, circuit := range tracking.CircuitsByID {
		if !circuit.IsOccupied || circuit.TrainID == "" {
			continue
		}
		train, exists := tracking.TrainsByID[circuit.TrainID]
		if !exists || train.TrainNumber == "" {
			continue
		}
		plan, exists := p.timetable.TrainsByNumber[train.TrainNumber]
		if !exists {
			continue
		}
		progress, exists := p.state.TrainsByNumber[train.TrainNumber]
		if !exists {
			continue
		}

		// 未照合なら最初の地点、それ以降は次の地点だけを照合する。
		nextIndex := 0
		if progress.CurrentPointID != nil {
			nextIndex = -1
			for i, point := range plan.Points {
				if point.TimetablePointID == *progress.CurrentPointID {
					nextIndex = i + 1
					break
				}
			}
		}
		if nextIndex < 0 || nextIndex >= len(plan.Points) {
			continue
		}
		point := plan.Points[nextIndex]
		if point.TrackCircuitID != circuitID {
			continue
		}

		pointID := point.TimetablePointID
		progress.CurrentPointID = &pointID
		// 地点が進んだ時点の到達時刻で上書きする。
		progress.CurrentPointOccupiedSinceSeconds = &now
		p.state.TrainsByNumber[train.TrainNumber] = progress
	}
}
