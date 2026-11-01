package prc

import (
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func (p *PRC) GetApproachTrain(state ctc.TrackingState) []ApproachTrain{

	var approachTrains []ApproachTrain

	if !state.IsInitialized {
		return nil
	}

	for circuitID, approachPoint := range p.approachPoints {
		circuitState := state.CircuitsByID[circuitID]

		if circuitState.IsOccupied && circuitState.TrainID != "" {
			approachTrains = append(approachTrains, ApproachTrain{
				StationID: approachPoint.StationID,
				TrackCircuitID: circuitID,
				TrackingTrainID: circuitState.TrainID,
			})
		}
	}

	return approachTrains
}