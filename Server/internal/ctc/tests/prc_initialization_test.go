package tests

import (
	"errors"
	"testing"
	"time"

	"github.com/neko3141592/Nakatetsu/Server/gen/ctcv1"
	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

type initializationPRC struct {
	core       *ctc.CTC
	err        error
	initialize int
	updates    int
}

func (p *initializationPRC) Initialize(tracking ctc.TrackingState) error {
	p.initialize++
	if !tracking.IsInitialized || !p.core.GetTrackingState().IsInitialized {
		return errors.New("CTC is not initialized")
	}
	for id := range tracking.TrainsByID {
		tracking.TrainsByID[id] = ctc.TrainState{}
	}
	return p.err
}

func (p *initializationPRC) UpdateRouteRequests() {
	p.core.GetTrackingState()
	p.updates++
}

func TestPRCInitializedOnceAfterCTCInitialization(t *testing.T) {
	for _, fail := range []bool{false, true} {
		name := "success"
		if fail {
			name = "error"
		}
		t.Run(name, func(t *testing.T) {
			core := initializationCore()
			prc := &initializationPRC{core: core}
			if fail {
				prc.err = errors.New("PRC initialization failed")
			}
			core.SetPRC(prc)
			unready := initializationSnapshot()
			unready.TrainInitialization = nil
			invalid := initializationSnapshot()
			invalid.TrackCircuits.OccupiedByCircuitId["A"] = true
			valid := initializationSnapshot()
			valid.TrackCircuits.OccupiedByCircuitId["A"] = true
			valid.TrainInitialization.Trains = []*ctcv1.TrainInitializationInfo{
				{ConsistId: "unity-1", OperationNumber: "01", ReferenceCircuitId: "A"},
			}
			done := make(chan []error, 1)
			go func() {
				done <- []error{
					core.UpdateSnapshot(unready), core.UpdateSnapshot(invalid),
					core.UpdateSnapshot(valid), core.UpdateSnapshot(valid),
				}
			}()
			select {
			case results := <-done:
				if results[0] != nil || !errors.Is(results[1], ctc.ErrTrainInitialization) || !errors.Is(results[2], prc.err) || !errors.Is(results[3], prc.err) {
					t.Fatalf("unexpected snapshot errors: %v", results)
				}
			case <-time.After(3 * time.Second):
				t.Fatal("PRC callback blocked while reading CTC state")
			}
			wantUpdates := 2
			if fail {
				wantUpdates = 0
			}
			if prc.initialize != 1 || prc.updates != wantUpdates {
				t.Fatalf("initialize=%d, updates=%d", prc.initialize, prc.updates)
			}
			for _, train := range core.GetTrackingState().TrainsByID {
				if train.OperationNumber != "01" {
					t.Fatal("PRC changed CTC tracking state")
				}
			}
		})
	}
}
