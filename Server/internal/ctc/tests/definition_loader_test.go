package tests

import (
	"encoding/json"
	"errors"
	"os"
	"path/filepath"
	"testing"

	"github.com/neko3141592/Nakatetsu/Server/internal/ctc"
)

func TestLoadTrackCircuitDefinitions(t *testing.T) {
	tests := []struct {
		name string
		edit func(*trackGraphJSON)
		want map[string]string
	}{
		{"forward_on_same_edge", nil, map[string]string{"A": "B", "B": "C", "C": ""}},
		{"reverse_travel_direction", func(g *trackGraphJSON) {
			g.Edges[0].TravelDirection = 2
		}, map[string]string{"A": "", "B": "A", "C": "B"}},
		{"reverse_geometry_axis", func(g *trackGraphJSON) {
			g.Edges[0].Start, g.Edges[0].End = 30, 0
		}, map[string]string{"A": "", "B": "A", "C": "B"}},
		{"explicit_block_link_is_preserved", func(g *trackGraphJSON) {
			g.Circuits[0].ToCircuitID = "C"
		}, map[string]string{"A": "C", "B": "C", "C": ""}},
		{"interlocking_is_not_completed", func(g *trackGraphJSON) {
			g.Circuits[1].ControlKind = "Interlocking"
		}, map[string]string{"A": "B", "B": "", "C": ""}},
		{"explicit_interlocking_link_is_preserved", func(g *trackGraphJSON) {
			g.Circuits[1].ControlKind = "Interlocking"
			g.Circuits[1].ToCircuitID = "A"
		}, map[string]string{"A": "B", "B": "A", "C": ""}},
		{"missing_direction_is_not_guessed", func(g *trackGraphJSON) {
			g.Edges[0].TravelDirection = 0
			g.Circuits[0].ToCircuitID = "C"
		}, map[string]string{"A": "C", "B": "", "C": ""}},
		{"multiple_candidates_are_not_guessed", func(g *trackGraphJSON) {
			g.Circuits[2].Sections[0].Start = 10
		}, map[string]string{"A": "", "B": "C", "C": ""}},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			graph := straightCircuitGraph()
			if tt.edit != nil {
				tt.edit(&graph)
			}
			definitions, err := ctc.LoadTrackCircuitDefinitions(writeCircuitGraph(t, graph))
			if err != nil {
				t.Fatal(err)
			}
			for id, want := range tt.want {
				if got := definitions[id].ToCircuitID; got != want {
					t.Errorf("%s.ToCircuitID = %q, want %q", id, got, want)
				}
			}
		})
	}
}

func TestLoadTrackCircuitDefinitionsAcrossFixedConnection(t *testing.T) {
	graph := straightCircuitGraph()
	graph.Nodes = []graphNodeJSON{{"N0", []string{"E"}}, {"N1", []string{"E", "F"}}, {"N2", []string{"F"}}}
	graph.Edges = []graphEdgeJSON{{"E", "N0", "N1", 1, 0, 10}, {"F", "N2", "N1", 2, 100, 90}}
	graph.Connections = []graphConnectionJSON{{"fixed", "N1", []graphPairJSON{{"pair", "E", "F", 1}}}}
	graph.Circuits = []graphCircuitJSON{
		{"A", "Block", "", []graphSectionJSON{{"E", 0, 10}}},
		{"B", "Interlocking", "", []graphSectionJSON{{"F", 90, 100}}},
	}
	definitions, err := ctc.LoadTrackCircuitDefinitions(writeCircuitGraph(t, graph))
	if err != nil {
		t.Fatal(err)
	}
	if definitions["A"].ToCircuitID != "B" || definitions["B"].ToCircuitID != "" {
		t.Fatalf("unexpected definitions: %+v", definitions)
	}
}

func TestLoadTrackCircuitDefinitionsFollowsSameCircuitAcrossEdges(t *testing.T) {
	graph := straightCircuitGraph()
	graph.Nodes = []graphNodeJSON{{"N0", []string{"E"}}, {"N1", []string{"E", "F"}}, {"N2", []string{"F"}}}
	graph.Edges = []graphEdgeJSON{{"E", "N0", "N1", 1, 0, 10}, {"F", "N1", "N2", 1, 0, 10}}
	graph.Connections = []graphConnectionJSON{{"fixed", "N1", []graphPairJSON{{"pair", "E", "F", 1}}}}
	graph.Circuits = []graphCircuitJSON{
		{"A", "Block", "", []graphSectionJSON{{"E", 0, 10}, {"F", 0, 5}}},
		{"B", "Block", "", []graphSectionJSON{{"F", 5, 10}}},
	}
	definitions, err := ctc.LoadTrackCircuitDefinitions(writeCircuitGraph(t, graph))
	if err != nil {
		t.Fatal(err)
	}
	if definitions["A"].ToCircuitID != "B" {
		t.Fatalf("A.ToCircuitID = %q", definitions["A"].ToCircuitID)
	}
}

func TestLoadTrackCircuitDefinitionsDoesNotGuessTurnout(t *testing.T) {
	graph := straightCircuitGraph()
	graph.Nodes = []graphNodeJSON{{"N0", []string{"E"}}, {"N1", []string{"E", "F", "G"}}, {"N2", []string{"F"}}, {"N3", []string{"G"}}}
	graph.Edges = []graphEdgeJSON{{"E", "N0", "N1", 1, 0, 10}, {"F", "N1", "N2", 1, 0, 10}, {"G", "N1", "N3", 1, 0, 10}}
	graph.Connections = []graphConnectionJSON{{"turnout", "N1", []graphPairJSON{{"normal", "E", "F", 2}, {"reverse", "E", "G", 3}}}}
	graph.Circuits = []graphCircuitJSON{
		{"A", "Block", "", []graphSectionJSON{{"E", 0, 10}}},
		{"B", "Interlocking", "", []graphSectionJSON{{"F", 0, 10}}},
		{"C", "Interlocking", "", []graphSectionJSON{{"G", 0, 10}}},
	}
	path := writeCircuitGraph(t, graph)
	definitions, err := ctc.LoadTrackCircuitDefinitions(path)
	if err != nil {
		t.Fatal(err)
	}
	if definitions["A"].ToCircuitID != "" {
		t.Fatalf("inferred a turnout branch: %+v", definitions["A"])
	}
	graph.Circuits[0].ToCircuitID = "C"
	definitions, err = ctc.LoadTrackCircuitDefinitions(writeCircuitGraph(t, graph))
	if err != nil || definitions["A"].ToCircuitID != "C" {
		t.Fatalf("explicit branch was not preserved: %+v, %v", definitions, err)
	}
}

func TestLoadTrackCircuitDefinitionsStopsAtCycle(t *testing.T) {
	graph := straightCircuitGraph()
	graph.Nodes = []graphNodeJSON{{"N0", []string{"E", "F"}}, {"N1", []string{"E", "F"}}}
	graph.Edges = []graphEdgeJSON{{"E", "N0", "N1", 1, 0, 10}, {"F", "N1", "N0", 1, 0, 10}}
	graph.Connections = []graphConnectionJSON{
		{"start", "N0", []graphPairJSON{{"start-pair", "E", "F", 1}}},
		{"end", "N1", []graphPairJSON{{"end-pair", "E", "F", 1}}},
	}
	graph.Circuits = []graphCircuitJSON{{"A", "Block", "", []graphSectionJSON{{"E", 0, 10}}}}
	definitions, err := ctc.LoadTrackCircuitDefinitions(writeCircuitGraph(t, graph))
	if err != nil || definitions["A"].ToCircuitID != "" {
		t.Fatalf("unexpected cycle result: %+v, %v", definitions, err)
	}
}

func TestLoadTrackCircuitDefinitionsRejectsInvalidGraph(t *testing.T) {
	tests := []struct {
		name string
		edit func(*trackGraphJSON)
	}{
		{"duplicate_circuit", func(g *trackGraphJSON) { g.Circuits = append(g.Circuits, g.Circuits[0]) }},
		{"unknown_control_kind", func(g *trackGraphJSON) { g.Circuits[0].ControlKind = "invalid" }},
		{"unknown_explicit_target", func(g *trackGraphJSON) { g.Circuits[0].ToCircuitID = "missing" }},
		{"missing_edge", func(g *trackGraphJSON) { g.Circuits[0].Sections[0].EdgeID = "missing" }},
		{"section_outside_edge", func(g *trackGraphJSON) { g.Circuits[0].Sections[0].End = 100 }},
		{"missing_node", func(g *trackGraphJSON) { g.Edges[0].NodeAID = "missing" }},
		{"no_circuits", func(g *trackGraphJSON) { g.Circuits = nil }},
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			graph := straightCircuitGraph()
			tt.edit(&graph)
			definitions, err := ctc.LoadTrackCircuitDefinitions(writeCircuitGraph(t, graph))
			if err == nil || definitions != nil {
				t.Fatalf("invalid graph accepted: %+v, %v", definitions, err)
			}
		})
	}
}

func TestLoadTrackCircuitDefinitionsReportsFileErrors(t *testing.T) {
	path := writeCircuitGraph(t, straightCircuitGraph())
	fragment := filepath.Join(filepath.Dir(path), "circuits.json")
	if err := os.Remove(fragment); err != nil {
		t.Fatal(err)
	}
	if _, err := ctc.LoadTrackCircuitDefinitions(path); !errors.Is(err, os.ErrNotExist) {
		t.Fatalf("expected missing file error, got %v", err)
	}
	if err := os.WriteFile(fragment, []byte("{"), 0600); err != nil {
		t.Fatal(err)
	}
	var syntaxError *json.SyntaxError
	if _, err := ctc.LoadTrackCircuitDefinitions(path); !errors.As(err, &syntaxError) {
		t.Fatalf("expected JSON syntax error, got %v", err)
	}
	if err := os.WriteFile(fragment, []byte(" null\n"), 0600); err != nil {
		t.Fatal(err)
	}
	if _, err := ctc.LoadTrackCircuitDefinitions(path); err == nil {
		t.Fatal("null fragment was accepted")
	}
}

func TestLoadNakatetsuTrackCircuitDefinitions(t *testing.T) {
	path := filepath.Join("..", "..", "..", "..", "SourceAssets", "Track", "Network", "NakatetsuTrackGraph", "graph.json")
	definitions, err := ctc.LoadTrackCircuitDefinitions(path)
	if err != nil {
		t.Fatal(err)
	}
	if len(definitions) != 154 {
		t.Fatalf("loaded %d circuits, want 154", len(definitions))
	}
	for circuitID, want := range map[string]string{
		"NT01_22BT":      "下り本線",
		"NT05_2RT":       "2番線",
		"NT07_26BT":      "下り急行線",
		"NT07_DEPOT_1T":  "電留1線",
		"NT11_1_STUB_T":  "1引線",
		"NT01_NT02_D1T":  "中央下1",
		"NT05_NT06_U2T":  "東長上2",
		"NT07_NT08_DE1T": "小泉下急1",
		"NT07_NT08_DL1T": "小泉下緩1",
	} {
		if got := definitions[circuitID].DisplayName; got != want {
			t.Errorf("%s.DisplayName = %q, want %q", circuitID, got, want)
		}
	}
	interlockingExits := map[string]string{
		"NT01_22BT": "NT01_NT02_D1T",
		"NT05_22T":  "NT05_NT06_D1T",
		"NT05_23T":  "NT04_NT05_U1T",
		"NT07_22AT": "NT06_NT07_U3T",
		"NT07_25BT": "NT07_NT08_DL1T",
		"NT07_26BT": "NT07_NT08_DE1T",
	}
	for circuitID, target := range interlockingExits {
		if definitions[circuitID].ToCircuitID != target {
			t.Errorf("%s.ToCircuitID = %q, want %q", circuitID, definitions[circuitID].ToCircuitID, target)
		}
	}
	for circuitID, target := range map[string]string{
		"NT01_NT02_D1T": "NT01_NT02_D2T",
		"NT01_NT02_D3T": "NT02_DT",
		"NT01_NT02_U2T": "NT01_NT02_U1T",
	} {
		if definitions[circuitID].ToCircuitID != target {
			t.Errorf("%s.ToCircuitID = %q, want %q", circuitID, definitions[circuitID].ToCircuitID, target)
		}
	}
	blockCount, completedCount := 0, 0
	for circuitID, definition := range definitions {
		if definition.DisplayName == "" {
			t.Errorf("circuit has no display name: %s", circuitID)
		}
		if definition.ControlKind == ctc.TrackControlInterlocking && definition.ToCircuitID != interlockingExits[circuitID] {
			t.Errorf("completed an Interlocking circuit: %s", circuitID)
		}
		if definition.ControlKind == ctc.TrackControlBlock {
			blockCount++
			if definition.ToCircuitID != "" {
				completedCount++
			}
		}
	}
	t.Logf("loaded %d circuits; completed %d/%d Block successors", len(definitions), completedCount, blockCount)
}

func straightCircuitGraph() trackGraphJSON {
	return trackGraphJSON{
		Nodes: []graphNodeJSON{{"N0", []string{"E"}}, {"N1", []string{"E"}}},
		Edges: []graphEdgeJSON{{"E", "N0", "N1", 1, 0, 30}},
		Circuits: []graphCircuitJSON{
			{"A", "Block", "", []graphSectionJSON{{"E", 0, 10}}},
			{"B", "Block", "", []graphSectionJSON{{"E", 10, 20}}},
			{"C", "Block", "", []graphSectionJSON{{"E", 20, 30}}},
		},
	}
}

func writeCircuitGraph(t *testing.T, graph trackGraphJSON) string {
	t.Helper()
	directory := t.TempDir()
	files := map[string]any{
		"graph.json":    map[string]any{"schemaVersion": 1, "graphId": "test", "files": []string{"nodes.json", "edges.json", "circuits.json"}},
		"nodes.json":    trackGraphJSON{Nodes: graph.Nodes},
		"edges.json":    trackGraphJSON{Edges: graph.Edges, Connections: graph.Connections},
		"circuits.json": trackGraphJSON{Circuits: graph.Circuits},
	}
	for name, value := range files {
		data, err := json.Marshal(value)
		if err != nil {
			t.Fatal(err)
		}
		if err := os.WriteFile(filepath.Join(directory, name), data, 0600); err != nil {
			t.Fatal(err)
		}
	}
	return filepath.Join(directory, "graph.json")
}
