package tests

import (
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

type routeManifestJSON struct {
	SchemaVersion int                  `json:"schemaVersion"`
	Stations      []routeManifestEntry `json:"stations"`
}

type routeManifestEntry struct {
	StationID string `json:"stationId"`
	File      string `json:"file"`
}

type routeStationJSON struct {
	InterlockingID string      `json:"interlockingId"`
	Routes         []routeJSON `json:"routes"`
}

type routeJSON struct {
	RouteID                   string   `json:"routeId"`
	StartTrackCircuitID       string   `json:"startTrackCircuitId"`
	DestinationTrackCircuitID string   `json:"destinationTrackCircuitId"`
	TrackCircuitIDs           []string `json:"trackCircuitIds"`
	RouteClearTrackCircuitIDs []string `json:"routeClearTrackCircuitIds"`
}

func TestLoadTrackRouteDefinitions(t *testing.T) {
	dir := t.TempDir()
	station := routeStationJSON{"S1", []routeJSON{
		{"R", "A", "C", []string{"A", "B", "C"}, []string{"C", "B"}},
		{"L", "C", "A", []string{"C", "B", "A"}, []string{"B", "A"}},
	}}
	writeRouteJSON(t, filepath.Join(dir, "stations", "S1.json"), station)
	// 同じ進路IDを別駅でも使える。絶対パスも読み込める。
	station.InterlockingID = "S2"
	station.Routes = station.Routes[:1]
	station2Path := filepath.Join(dir, "S2.json")
	writeRouteJSON(t, station2Path, station)
	path := filepath.Join(dir, "interlocking.json")
	writeRouteJSON(t, path, routeManifestJSON{1, []routeManifestEntry{{"S1", "stations/S1.json"}, {"S2", station2Path}}})

	got, err := ctc.LoadTrackRouteDefinitions(path, routeTestCircuits())
	if err != nil {
		t.Fatal(err)
	}
	want := []ctc.TrackRouteDefinition{
		{InterlockingStationID: "S1", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "C"}, RouteClearTrackCircuitIDs: []string{"C", "B"}},
		{InterlockingStationID: "S1", InterlockingRouteID: "L", TrackCircuitIDs: []string{"C", "B", "A"}, RouteClearTrackCircuitIDs: []string{"B", "A"}},
		{InterlockingStationID: "S2", InterlockingRouteID: "R", TrackCircuitIDs: []string{"A", "B", "C"}, RouteClearTrackCircuitIDs: []string{"C", "B"}},
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("routes = %+v, want %+v", got, want)
	}
}

func TestLoadTrackRouteDefinitionsRejectsInvalidDefinitions(t *testing.T) {
	tests := []struct {
		name string
		edit func(*routeManifestJSON, *routeStationJSON)
		want string
	}{
		{"schema", func(m *routeManifestJSON, _ *routeStationJSON) { m.SchemaVersion = 2 }, "invalid interlocking manifest"},
		{"no_stations", func(m *routeManifestJSON, _ *routeStationJSON) { m.Stations = nil }, "invalid interlocking manifest"},
		{"empty_station", func(m *routeManifestJSON, _ *routeStationJSON) { m.Stations[0].StationID = " " }, "station entry"},
		{"duplicate_station", func(m *routeManifestJSON, _ *routeStationJSON) { m.Stations = append(m.Stations, m.Stations[0]) }, "station entry"},
		{"empty_file", func(m *routeManifestJSON, _ *routeStationJSON) { m.Stations[0].File = "" }, "station entry"},
		{"duplicate_file", func(m *routeManifestJSON, _ *routeStationJSON) {
			m.Stations = append(m.Stations, routeManifestEntry{"S2", "stations/../stations/S1.json"})
		}, "duplicate station file"},
		{"station_mismatch", func(_ *routeManifestJSON, s *routeStationJSON) { s.InterlockingID = "S2" }, "interlockingId"},
		{"no_routes", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes = nil }, "missing routes"},
		{"empty_route", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes[0].RouteID = " " }, "route ID"},
		{"duplicate_route", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes = append(s.Routes, s.Routes[0]) }, "route ID"},
		{"missing_ordered_list", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes[0].TrackCircuitIDs = nil }, "start to destination"},
		{"start_missing", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes[0].TrackCircuitIDs = []string{"B", "C"} }, "start to destination"},
		{"wrong_destination", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes[0].DestinationTrackCircuitID = "A" }, "start to destination"},
		{"unknown_circuit", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes[0].TrackCircuitIDs[1] = "missing" }, "unknown or duplicate circuit"},
		{"empty_circuit", func(_ *routeManifestJSON, s *routeStationJSON) { s.Routes[0].TrackCircuitIDs[1] = "" }, "unknown or duplicate circuit"},
		{"duplicate_circuit", func(_ *routeManifestJSON, s *routeStationJSON) {
			s.Routes[0].TrackCircuitIDs = []string{"A", "B", "A", "C"}
		}, "unknown or duplicate circuit"},
		{"unknown_clear_circuit", func(_ *routeManifestJSON, s *routeStationJSON) {
			s.Routes[0].RouteClearTrackCircuitIDs = []string{"missing"}
		}, "unknown route-clear circuit"},
		{"empty_clear_circuit_id", func(_ *routeManifestJSON, s *routeStationJSON) {
			s.Routes[0].RouteClearTrackCircuitIDs = []string{""}
		}, "unknown route-clear circuit"},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			dir := t.TempDir()
			manifest := routeManifestJSON{1, []routeManifestEntry{{"S1", "stations/S1.json"}}}
			station := routeStationJSON{"S1", []routeJSON{{"R", "A", "C", []string{"A", "B", "C"}, []string{"B", "C"}}}}
			tt.edit(&manifest, &station)
			path := filepath.Join(dir, "interlocking.json")
			writeRouteJSON(t, path, manifest)
			writeRouteJSON(t, filepath.Join(dir, "stations", "S1.json"), station)
			got, err := ctc.LoadTrackRouteDefinitions(path, routeTestCircuits())
			if err == nil || !strings.Contains(err.Error(), tt.want) || got != nil {
				t.Fatalf("routes = %+v, err = %v; want nil routes and error containing %q", got, err, tt.want)
			}
		})
	}
}

func TestLoadTrackRouteDefinitionsFileErrors(t *testing.T) {
	for _, target := range []string{"manifest", "station"} {
		for _, content := range []string{"missing", "{", "null\n"} {
			t.Run(target+"/"+content, func(t *testing.T) {
				dir := t.TempDir()
				path := filepath.Join(dir, "interlocking.json")
				badPath := path
				if target == "station" {
					writeRouteJSON(t, path, routeManifestJSON{1, []routeManifestEntry{{"S1", "S1.json"}}})
					badPath = filepath.Join(dir, "S1.json")
				}
				if content != "missing" {
					if err := os.WriteFile(badPath, []byte(content), 0600); err != nil {
						t.Fatal(err)
					}
				}
				got, err := ctc.LoadTrackRouteDefinitions(path, routeTestCircuits())
				if err == nil || got != nil || !strings.Contains(err.Error(), badPath) {
					t.Fatalf("routes = %+v, err = %v; want file error with %s", got, err, badPath)
				}
				if content == "missing" && !errors.Is(err, os.ErrNotExist) {
					t.Fatalf("lost file error: %v", err)
				}
				var syntaxErr *json.SyntaxError
				if content == "{" && !errors.As(err, &syntaxErr) {
					t.Fatalf("lost syntax error: %v", err)
				}
			})
		}
	}
}

func TestLoadNakatetsuTrackRouteDefinitions(t *testing.T) {
	root := filepath.Join("..", "..", "..", "..", "SourceAssets", "Track", "Network")
	circuits, err := ctc.LoadTrackCircuitDefinitions(filepath.Join(root, "NakatetsuTrackGraph", "graph.json"))
	if err != nil {
		t.Fatal(err)
	}
	routes, err := ctc.LoadTrackRouteDefinitions(filepath.Join(root, "NakatetsuInterlocking", "interlocking.json"), circuits)
	if err != nil {
		t.Fatal(err)
	}
	if len(routes) != 58 {
		t.Fatalf("loaded %d routes, want 58", len(routes))
	}
	counts := make(map[string]int)
	for _, route := range routes {
		counts[route.InterlockingStationID]++
	}
	if want := (map[string]int{"NT01": 6, "NT05": 8, "NT07": 24, "NT11": 20}); !reflect.DeepEqual(counts, want) {
		t.Fatalf("route counts = %v, want %v", counts, want)
	}
	refs := ctc.BuildRouteCircuitRefs(routes)
	for _, tt := range []struct{ station, route, from, to string }{
		{"NT01", "1LA", "NT01_21BT", "NT01_21AT"},
		{"NT01", "1R", "NT01_1RT", "NT01_23T"},
		// ATCの回路列には含まれていなかった進入元も、追跡用JSONには含める。
		{"NT07", "2RA", "NT07_22BT", "NT07_23T"},
		{"NT11", "11LB", "NT11_DEPOT_T", "NT11_26T"},
	} {
		t.Run(tt.station+"/"+tt.route, func(t *testing.T) {
			state := ctc.TrackingState{}
			states := map[ctc.RouteKey]ctc.InterlockingRouteState{
				{StationID: tt.station, RouteID: tt.route}: {IsAvailable: true, PathEstablished: true},
			}
			ctc.UpdateTracking(&state, map[string]bool{tt.from: true}, circuits, routes, refs, states)
			if state.NeedsReset || state.DynamicLinks[tt.from] != tt.to {
				t.Fatalf("dynamic links = %v, NeedsReset = %v; want %s -> %s", state.DynamicLinks, state.NeedsReset, tt.from, tt.to)
			}
		})
	}
}

func routeTestCircuits() map[string]ctc.TrackCircuitDefinition {
	return map[string]ctc.TrackCircuitDefinition{"A": {}, "B": {}, "C": {}}
}

func writeRouteJSON(t *testing.T, path string, value any) {
	t.Helper()
	data, err := json.Marshal(value)
	if err != nil {
		t.Fatal(err)
	}
	if err := os.MkdirAll(filepath.Dir(path), 0700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(path, data, 0600); err != nil {
		t.Fatal(err)
	}
}
