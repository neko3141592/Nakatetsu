package ctc

import "fmt"

func (c *CTC) GetStationControlMode(stationID string) (StationControlMode, error) {
	c.mu.RLock()
	defer c.mu.RUnlock()
	state, ok := c.stationStatesByID[stationID]
	if !ok {
		return StationControlManual, fmt.Errorf("unknown station: %q", stationID)
	}
	return state.ControlMode, nil
}

func (c *CTC) SetStationControlMode(stationID string, mode StationControlMode) error {
	if mode != StationControlManual && mode != StationControlAuto {
		return fmt.Errorf("invalid station control mode: %d", mode)
	}
	c.mu.Lock()
	defer c.mu.Unlock()
	state, ok := c.stationStatesByID[stationID]
	if !ok {
		return fmt.Errorf("unknown station: %q", stationID)
	}
	state.ControlMode = mode
	c.stationStatesByID[stationID] = state
	return nil
}
