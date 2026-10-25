package tests

import (
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func TestStationControlMode(t *testing.T) {
	core := ctc.New(nil, []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "R1"},
		{InterlockingStationID: "S1", InterlockingRouteID: "R2"},
		{InterlockingStationID: "S2", InterlockingRouteID: "R1"},
	})
	for _, id := range []string{"S1", "S2"} {
		if mode, err := core.GetStationControlMode(id); err != nil || mode != ctc.StationControlManual {
			t.Fatalf("station=%s: mode=%v, err=%v", id, mode, err)
		}
	}
	for _, wanted := range []ctc.StationControlMode{ctc.StationControlAuto, ctc.StationControlManual} {
		if err := core.SetStationControlMode("S1", wanted); err != nil {
			t.Fatal(err)
		}
		if mode, err := core.GetStationControlMode("S1"); err != nil || mode != wanted {
			t.Fatalf("mode=%v, err=%v, want %v", mode, err, wanted)
		}
		if mode, err := core.GetStationControlMode("S2"); err != nil || mode != ctc.StationControlManual {
			t.Fatalf("changing S1 changed S2: mode=%v, err=%v", mode, err)
		}
	}
}

func TestStationControlModeRejectsInvalidUpdates(t *testing.T) {
	core := ctc.New(nil, []ctc.TrackRouteDefinition{{InterlockingStationID: "S1", InterlockingRouteID: "R"}})
	if err := core.SetStationControlMode("S1", ctc.StationControlAuto); err != nil {
		t.Fatal(err)
	}
	for _, id := range []string{"", "missing"} {
		if err := core.SetStationControlMode(id, ctc.StationControlAuto); err == nil {
			t.Fatalf("accepted unknown station %q", id)
		}
		if _, err := core.GetStationControlMode(id); err == nil {
			t.Fatalf("created unknown station %q", id)
		}
	}
	for _, mode := range []ctc.StationControlMode{-1, 2} {
		if err := core.SetStationControlMode("S1", mode); err == nil {
			t.Fatalf("accepted invalid mode %v", mode)
		}
	}
	if mode, err := core.GetStationControlMode("S1"); err != nil || mode != ctc.StationControlAuto {
		t.Fatalf("invalid updates changed mode: mode=%v, err=%v", mode, err)
	}
}
